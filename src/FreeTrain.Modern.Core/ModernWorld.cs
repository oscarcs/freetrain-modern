namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
    private const int MaxFineHeight = 7 * 4;
    private const int ExtraVoxelDepth = 64;
    private const int StationReachRange = 10;

    private readonly int[,] groundLevels;
    private readonly int[,,] mountainCornerHeights;
    private readonly int[,] fineHeights;
    private readonly ModernSparseVoxelArray<ModernVoxelOccupancy> voxels;
    private readonly Dictionary<ModernVoxelKey, ModernTrafficVoxel> trafficVoxels = new();
    private readonly Dictionary<string, ModernCar> cars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ModernVoxelKey, string> trafficCars = new();
    private readonly Dictionary<string, ModernPlacedEntity> entities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ModernVoxelKey, string> entityVoxels = new();
    private readonly Dictionary<string, ModernStation> stations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ModernVoxelKey, string> stationVoxels = new();
    private readonly Dictionary<string, ModernPlatform> platforms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ModernVoxelKey, string> platformVoxels = new();
    private readonly Dictionary<string, ModernTrain> trains = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int H, int V), int[]> tunnelTerrainBackups = new();
    private readonly HashSet<ModernVoxelKey> bridgePierVoxels = new();

    private readonly record struct PlatformStop(ModernPlatform Platform, int Index);

    private ModernWorld(
        string name,
        int width,
        int height,
        int waterLevel,
        int[,] fineHeights,
        ModernWorldClock clock,
        ModernAccountState account)
    {
        Name = name;
        Width = width;
        Height = height;
        WaterLevel = waterLevel;
        this.fineHeights = fineHeights;
        Clock = clock;
        Account = account;
        Depth = MaxHeightCutLevel + ExtraVoxelDepth;
        groundLevels = new int[width, height];
        mountainCornerHeights = new int[width, height, 4];
        voxels = new ModernSparseVoxelArray<ModernVoxelOccupancy>(width, height, Depth);
        RebuildTilesFromFineHeights();
    }

    public event EventHandler<ModernWorldChangedEventArgs>? Changed;

    public string Name { get; private set; }
    public int Width { get; }
    public int Height { get; }
    public int Depth { get; }
    public int WaterLevel { get; }
    public int MaxHeightCutLevel => MaxFineHeight / 4;
    public MapTransportState Transport { get; } = new();
    public ModernWorldClock Clock { get; private set; }
    public ModernAccountState Account { get; private set; }
    public IReadOnlyCollection<ModernTrafficVoxel> TrafficVoxels => trafficVoxels.Values;
    public IReadOnlyCollection<ModernCar> Cars => cars.Values;
    public IReadOnlyCollection<ModernStation> Stations => stations.Values;
    public IReadOnlyCollection<ModernPlatform> Platforms => platforms.Values;
    public IReadOnlyCollection<ModernTrain> Trains => trains.Values;
    public IReadOnlyCollection<ModernPlacedEntity> Entities => entities.Values;
    public IReadOnlyCollection<ModernVoxelKey> BridgePierVoxels => bridgePierVoxels;
    public int TotalStationPopulation => stations.Values.Sum(GetStationPopulation);
    public int TotalWaitingPassengers => stations.Values.Sum(station => station.Stats.WaitingPassengers(GetStationPopulation(station)));
    public int TotalLoadedPassengersToday => stations.Values.Sum(station => station.Stats.LoadedToday);
    public int TotalUnloadedPassengersToday => stations.Values.Sum(station => station.Stats.UnloadedToday);
    public int TotalTrainStopsToday => stations.Values.Sum(station => station.Stats.TrainsToday);
    public IReadOnlyList<ModernStationDevelopmentSignal> StationDevelopmentSignals => stations.Values
        .Select(CreateDevelopmentSignal)
        .OrderByDescending(signal => signal.Strength)
        .ThenBy(signal => signal.StationName, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    public IEnumerable<KeyValuePair<ModernVoxelKey, ModernVoxelOccupancy>> OccupiedVoxels => voxels.Entries;
    public IEnumerable<ModernPlacedEntity> LandEntities => entities.Values.Where(entity => entity.Kind == ModernEntityKind.Land);
    public IEnumerable<ModernPlacedEntity> StructureEntities => entities.Values.Where(entity => entity.Kind == ModernEntityKind.Structure);

    public bool IsInside(int h, int v)
    {
        return h >= 0 && h < Width && v >= 0 && v < Height;
    }

    public bool IsInside(ModernLocation location)
    {
        (int h, int v) = ToHv(location);
        return IsInside(h, v) && location.Z >= 0 && location.Z < Depth;
    }

    public bool IsInside(ModernVoxelKey key)
    {
        return voxels.IsInside(key);
    }

    public (int H, int V) ToHv(ModernLocation location)
    {
        int shiftedX = location.X - (Height - 1) / 2;
        int h = (shiftedX + location.Y) >> 1;
        int v = location.Y - shiftedX;
        return (h, v);
    }

    public ModernLocation ToLocation(int h, int v, int z)
    {
        int shiftedX = h - v / 2;
        return new ModernLocation(shiftedX + (Height - 1) / 2, h + (v + 1) / 2, z);
    }

    public ModernLocation ToLocation(ModernVoxelKey key)
    {
        return ToLocation(key.H, key.V, key.Z);
    }

    public ModernVoxelKey ToVoxelKey(ModernLocation location)
    {
        (int h, int v) = ToHv(location);
        return new ModernVoxelKey(h, v, location.Z);
    }

    public ModernVoxelOccupancy? GetVoxel(ModernVoxelKey key)
    {
        return voxels.TryGet(key, out ModernVoxelOccupancy? voxel) ? voxel : null;
    }

    public ModernPlacedEntity? GetEntityAt(ModernVoxelKey key)
    {
        return entityVoxels.TryGetValue(key, out string? entityId)
            && entities.TryGetValue(entityId, out ModernPlacedEntity? entity)
                ? entity
                : null;
    }

    public bool IsReusable(ModernVoxelKey key)
    {
        ModernVoxelOccupancy? occupancy = GetVoxel(key);
        if (occupancy is null)
        {
            return true;
        }

        return occupancy.EntityId is { } entityId
            && entities.TryGetValue(entityId, out ModernPlacedEntity? entity)
            && entity.IsSilentlyReclaimable;
    }

    private static ModernVoxelKey ToVoxelKey(TileLocation location)
    {
        return new ModernVoxelKey(location.H, location.V, location.Z);
    }

    private void Publish(ModernWorldChangeKind kind, ModernVoxelKey? location, string? description)
    {
        Changed?.Invoke(this, new ModernWorldChangedEventArgs(kind, location, description));
    }

    private static readonly (int H, int V)[] EightWayNeighbors =
    {
        (0, -1),
        (1, -1),
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1)
    };
}
