namespace FreeTrain.Modern;

public enum ModernWorldChangeKind
{
    Terrain,
    Transport,
    Voxel,
    Entity,
    Clock,
    Economy,
    Reset
}

public enum ModernVoxelKind
{
    Traffic,
    Land,
    Structure
}

public enum ModernTransportKind
{
    Rail,
    Road
}

public enum ModernTrafficAccessoryKind
{
    RailRoadCrossing
}

public enum ModernRailRoadCrossingOrientation
{
    RailNorthSouth,
    RailEastWest
}

public readonly record struct ModernVoxelKey(int H, int V, int Z);

public sealed record ModernWorldChangedEventArgs(
    ModernWorldChangeKind Kind,
    ModernVoxelKey? Location = null,
    string? Description = null);

public sealed record ModernTrafficAccessory(
    ModernTrafficAccessoryKind Kind,
    ModernRailRoadCrossingOrientation? CrossingOrientation = null);

public sealed record ModernTrafficVoxel(
    ModernVoxelKey Location,
    byte RailDirectionMask,
    byte RoadDirectionMask,
    string? RoadContributionId,
    string? CarId = null,
    ModernTrafficAccessory? Accessory = null)
{
    public ModernVoxelKind Kind => ModernVoxelKind.Traffic;
    public bool HasRail => RailDirectionMask != 0;
    public bool HasRoad => RoadDirectionMask != 0;
    public bool IsEmpty => !HasRail && !HasRoad;
    public bool IsOccupied => !string.IsNullOrWhiteSpace(CarId);
    public bool HasRailRoadCrossing => Accessory?.Kind == ModernTrafficAccessoryKind.RailRoadCrossing;
}

public sealed class ModernWorld
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

    public int MaxGroundLevel
    {
        get
        {
            int max = 0;
            for (int v = 0; v < Height; v++)
            {
                for (int h = 0; h < Width; h++)
                {
                    max = Math.Max(max, GetTerrainTile(h, v).SurfaceLevel);
                }
            }

            return max;
        }
    }

    public int GetGroundLevel(int h, int v)
    {
        if (!IsInside(h, v))
        {
            return 0;
        }

        return GetTerrainTile(h, v).SurfaceLevel;
    }

    public int GetGroundLevel(ModernLocation location)
    {
        (int h, int v) = ToHv(location);
        return GetGroundLevel(h, v);
    }

    public int GetRailLevel(int h, int v)
    {
        return Transport.GetRailLevel(h, v, GetGroundLevel);
    }

    public TerrainTilePreview GetTerrainTile(int h, int v)
    {
        if (!IsInside(h, v))
        {
            return new TerrainTilePreview(0, 0, 0, 0, 0, 0);
        }

        int top = mountainCornerHeights[h, v, 0];
        int right = mountainCornerHeights[h, v, 1];
        int bottom = mountainCornerHeights[h, v, 2];
        int left = mountainCornerHeights[h, v, 3];
        int baseLevel = groundLevels[h, v];
        return new TerrainTilePreview(baseLevel, baseLevel, top, right, bottom, left);
    }

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

    public bool IsBuildableSurface(TileLocation location)
    {
        TerrainTilePreview terrain = GetTerrainTile(location.H, location.V);
        return terrain.IsFlat
            && terrain.SurfaceLevel == location.Z
            && terrain.SurfaceLevel >= WaterLevel;
    }

    public bool IsTransportBuildableSurface(TileLocation location)
    {
        return IsBuildableSurface(location) && IsReusable(ToVoxelKey(location));
    }

    public bool CanPlaceEntity(ModernPlacedEntity entity, bool allowTransportOverlap = false)
    {
        foreach (ModernVoxelKey voxel in entity.OccupiedVoxels)
        {
            ModernVoxelOccupancy? occupancy = GetVoxel(voxel);
            bool canReuseVoxel = occupancy is null
                || IsReusable(voxel)
                || allowTransportOverlap && occupancy.IsTraffic;
            if (!IsInside(voxel)
                || !canReuseVoxel
                || (!allowTransportOverlap && Transport.HasRail(voxel.H, voxel.V))
                || (!allowTransportOverlap && Transport.HasRoad(voxel.H, voxel.V)))
            {
                return false;
            }

            TerrainTilePreview terrain = GetTerrainTile(voxel.H, voxel.V);
            if (!terrain.IsFlat || terrain.SurfaceLevel != entity.Z || terrain.SurfaceLevel < WaterLevel)
            {
                return false;
            }
        }

        return true;
    }

    public bool CanPlaceStation(ModernStation station)
    {
        foreach (ModernVoxelKey voxel in station.OccupiedVoxels)
        {
            TerrainTilePreview terrain = GetTerrainTile(voxel.H, voxel.V);
            if (!IsInside(voxel)
                || !IsReusable(voxel)
                || Transport.HasRail(voxel.H, voxel.V)
                || Transport.HasRoad(voxel.H, voxel.V)
                || stationVoxels.ContainsKey(voxel)
                || platformVoxels.ContainsKey(voxel)
                || !terrain.IsFlat
                || terrain.SurfaceLevel != station.Z
                || terrain.SurfaceLevel < WaterLevel)
            {
                return false;
            }
        }

        return true;
    }

    public bool AddStation(ModernStation station)
    {
        if (stations.ContainsKey(station.StationId) || !CanPlaceStation(station))
        {
            return false;
        }

        stations[station.StationId] = station;
        foreach (ModernVoxelKey voxel in station.OccupiedVoxels)
        {
            RemoveReusableEntityAt(voxel);
            stationVoxels[voxel] = station.StationId;
            voxels[voxel] = new ModernVoxelOccupancy(voxel, ModernVoxelKind.Structure, station.StationId, null);
        }

        Publish(ModernWorldChangeKind.Entity, new ModernVoxelKey(station.H, station.V, station.Z), "Station built.");
        return true;
    }

    public bool CanPlacePlatform(ModernPlatform platform)
    {
        if (!platform.Direction.IsSharp)
        {
            return false;
        }

        foreach (ModernVoxelKey voxel in EnumeratePlatformVoxels(platform))
        {
            if (!IsInside(voxel)
                || platformVoxels.ContainsKey(voxel)
                || stationVoxels.ContainsKey(voxel)
                || !Transport.HasRail(voxel.H, voxel.V)
                || GetGroundLevel(voxel.H, voxel.V) != platform.Z
                || !IsStraightRailAlong(voxel.H, voxel.V, platform.Direction))
            {
                return false;
            }
        }

        return true;
    }

    public bool AddPlatform(ModernPlatform platform)
    {
        if (platforms.ContainsKey(platform.PlatformId) || !CanPlacePlatform(platform))
        {
            return false;
        }

        ModernPlatform attached = platform with { StationId = platform.StationId ?? FindNearestStationId(platform) };
        platforms[attached.PlatformId] = attached;
        foreach (ModernVoxelKey voxel in EnumeratePlatformVoxels(attached))
        {
            platformVoxels[voxel] = attached.PlatformId;
        }

        Publish(ModernWorldChangeKind.Entity, new ModernVoxelKey(attached.H, attached.V, attached.Z), "Platform built.");
        return true;
    }

    public bool RemoveRailServiceAt(TileLocation location)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        if (platformVoxels.TryGetValue(key, out string? platformId))
        {
            ModernPlatform platform = platforms[platformId];
            platforms.Remove(platformId);
            foreach (ModernVoxelKey voxel in EnumeratePlatformVoxels(platform))
            {
                platformVoxels.Remove(voxel);
            }

            Publish(ModernWorldChangeKind.Entity, key, "Platform removed.");
            return true;
        }

        if (stationVoxels.TryGetValue(key, out string? stationId))
        {
            ModernStation station = stations[stationId];
            stations.Remove(stationId);
            foreach (ModernVoxelKey voxel in station.OccupiedVoxels)
            {
                stationVoxels.Remove(voxel);
                if (voxels[voxel]?.EntityId == station.StationId)
                {
                    voxels.Remove(voxel);
                }
            }

            foreach (ModernPlatform platform in platforms.Values.Where(platform => platform.StationId == stationId).ToArray())
            {
                platforms[platform.PlatformId] = platform with { StationId = null };
            }

            Publish(ModernWorldChangeKind.Entity, key, "Station removed.");
            return true;
        }

        return false;
    }

    public bool AddEntity(ModernPlacedEntity entity, bool allowTransportOverlap = false)
    {
        if (entities.ContainsKey(entity.EntityId) || !CanPlaceEntity(entity, allowTransportOverlap))
        {
            return false;
        }

        entities[entity.EntityId] = entity;
        foreach (ModernVoxelKey voxel in entity.OccupiedVoxels)
        {
            RemoveReusableEntityAt(voxel);
            entityVoxels[voxel] = entity.EntityId;
            voxels[voxel] = new ModernVoxelOccupancy(voxel, entity.Kind.ToVoxelKind(), entity.EntityId, null);
        }

        Publish(ModernWorldChangeKind.Entity, new ModernVoxelKey(entity.H, entity.V, entity.Z), "Entity added.");
        return true;
    }

    public bool RemoveEntity(string entityId)
    {
        if (!entities.Remove(entityId, out ModernPlacedEntity? entity))
        {
            return false;
        }

        foreach (ModernVoxelKey voxel in entity.OccupiedVoxels)
        {
            entityVoxels.Remove(voxel);
            if (voxels[voxel]?.EntityId == entity.EntityId)
            {
                voxels.Remove(voxel);
            }
        }

        entity.PublishRemoved();
        Publish(ModernWorldChangeKind.Entity, new ModernVoxelKey(entity.H, entity.V, entity.Z), "Entity removed.");
        return true;
    }

    public bool RaiseCorner(int h, int v, TerrainCorner corner)
    {
        return AdjustCorner(h, v, corner, 1);
    }

    public bool LowerCorner(int h, int v, TerrainCorner corner)
    {
        return AdjustCorner(h, v, corner, -1);
    }

    public void AdvanceClock(long minutes = 1)
    {
        if (minutes <= 0)
        {
            return;
        }

        long previousDay = Clock.AbsoluteMinute / (24 * 60);
        long previousHour = Clock.AbsoluteMinute / 60;
        ModernDayNight previousDayNight = Clock.DayOrNight;
        ModernSeason previousSeason = Clock.Season;
        Clock = Clock.AdvanceMinutes(minutes);
        long currentDay = Clock.AbsoluteMinute / (24 * 60);
        long currentHour = Clock.AbsoluteMinute / 60;
        if (currentHour > previousHour)
        {
            ApplyStationHourlyDecay(currentHour - previousHour);
        }

        if (currentDay > previousDay)
        {
            ApplyStationDailyReset(currentDay - previousDay);
        }

        AdvanceTrains(minutes);
        Publish(ModernWorldChangeKind.Clock, null, "Clock advanced.");

        if (Clock.DayOrNight != previousDayNight || Clock.Season != previousSeason)
        {
            Publish(ModernWorldChangeKind.Reset, null, "Clock changed visual period.");
        }
    }

    public bool Spend(long amount, ModernAccountGenre genre, string description)
    {
        if (amount <= 0)
        {
            return false;
        }

        Account = Account.Spend(amount, genre, Clock, description);
        Publish(ModernWorldChangeKind.Economy, null, description);
        return true;
    }

    public bool Earn(long amount, ModernAccountGenre genre, string description)
    {
        if (amount <= 0)
        {
            return false;
        }

        Account = Account.Earn(amount, genre, Clock, description);
        Publish(ModernWorldChangeKind.Economy, null, description);
        return true;
    }

    public bool AddCar(ModernCar car)
    {
        if (cars.ContainsKey(car.CarId))
        {
            return false;
        }

        cars[car.CarId] = car;
        Publish(ModernWorldChangeKind.Entity, car.State.Location, "Car added.");
        return true;
    }

    public bool AddTrain(ModernTrain train)
    {
        if (trains.ContainsKey(train.TrainId))
        {
            return false;
        }

        trains[train.TrainId] = train;
        Publish(ModernWorldChangeKind.Entity, train.Head?.Location, "Train added.");
        return true;
    }

    public bool PlaceTrain(string trainId, ModernVoxelKey location, ModernDirection direction, IReadOnlyDictionary<string, TrainCarContribution> trainCars)
    {
        if (!trains.TryGetValue(trainId, out ModernTrain? train) || !Transport.HasRail(location.H, location.V))
        {
            return false;
        }

        IReadOnlyList<string> carIds = train.Contribution.CreateCarIds(3);
        ModernVoxelKey[] locations = new ModernVoxelKey[carIds.Count];
        ModernDirection[] directions = new ModernDirection[carIds.Count];
        ModernVoxelKey current = location;
        ModernDirection? currentDirection = null;

        for (int index = carIds.Count - 1; index >= 0; index--)
        {
            ModernRailRoad? railRoad = CreateRailRoad(current.H, current.V);
            if (!trainCars.ContainsKey(carIds[index])
                || railRoad is null
                || IsTrainOccupying(current))
            {
                return false;
            }

            currentDirection ??= railRoad.Dir1;
            locations[index] = current;
            directions[index] = currentDirection;
            currentDirection = railRoad.Guide(currentDirection);

            if (index > 0 && !TryStepOnRail(current, currentDirection, out current))
            {
                return false;
            }
        }

        for (int i = 0; i < locations.Length - 1; i++)
        {
            for (int j = i + 1; j < locations.Length; j++)
            {
                if (locations[i] == locations[j])
                {
                    return false;
                }
            }
        }

        ModernTrainCarPlacement[] placements = carIds
            .Select((carId, index) => new ModernTrainCarPlacement(carId, locations[index], directions[index].Index))
            .ToArray();
        int passengerCapacity = carIds.Sum(carId => trainCars.TryGetValue(carId, out TrainCarContribution? car) ? Math.Max(0, car.Capacity) : 0);
        int passengerSeatedCapacity = carIds.Sum(carId => trainCars.TryGetValue(carId, out TrainCarContribution? car) ? Math.Max(0, car.SeatedCapacity) : 0);

        trains[trainId] = train with
        {
            Cars = placements,
            MinuteAccumulator = 0,
            State = ModernTrainState.Moving,
            StopRemainingMinutes = 0,
            PassengerCapacity = passengerCapacity,
            PassengerSeatedCapacity = passengerSeatedCapacity,
            PassengerCount = 0,
            PassengerSourceLocation = null,
            CurrentStopPlatformId = null,
            LastStoppedPlatformId = null,
            GarageLocation = null,
            GarageDirectionIndex = null
        };
        Publish(ModernWorldChangeKind.Entity, location, "Train placed.");
        return true;
    }

    public bool StoreTrainInGarage(string trainId)
    {
        if (!trains.TryGetValue(trainId, out ModernTrain? train)
            || train.Head is not { } head
            || !IsGarageRail(head.Location.H, head.Location.V)
            || !train.Cars.All(car => IsGarageRail(car.Location.H, car.Location.V)))
        {
            return false;
        }

        trains[trainId] = train with
        {
            Cars = Array.Empty<ModernTrainCarPlacement>(),
            State = ModernTrainState.InGarage,
            MinuteAccumulator = 0,
            StopRemainingMinutes = 0,
            CurrentStopPlatformId = null,
            GarageLocation = head.Location,
            GarageDirectionIndex = head.DirectionIndex
        };
        Publish(ModernWorldChangeKind.Entity, head.Location, "Train stored in garage.");
        return true;
    }

    public bool DispatchTrainFromGarage(string trainId, IReadOnlyDictionary<string, TrainCarContribution> trainCars)
    {
        if (!trains.TryGetValue(trainId, out ModernTrain? train)
            || train.State != ModernTrainState.InGarage
            || train.GarageLocation is not { } garageLocation)
        {
            return false;
        }

        return PlaceTrain(
            trainId,
            garageLocation,
            ModernDirection.FromIndex(train.GarageDirectionIndex ?? 2),
            trainCars);
    }

    public bool PlaceCar(string carId, ModernVoxelKey location, ModernDirection direction)
    {
        if (!cars.TryGetValue(carId, out ModernCar? car))
        {
            return false;
        }

        if (!CanPlaceCar(car.Kind, location))
        {
            return false;
        }

        RemoveCarFromTraffic(car);
        ModernCar placed = car.Place(location, direction);
        cars[carId] = placed;
        trafficCars[location] = carId;
        SynchronizeTrafficVoxel(location.H, location.V);
        Publish(ModernWorldChangeKind.Entity, location, "Car placed.");
        return true;
    }

    public bool RemoveCar(string carId)
    {
        if (!cars.TryGetValue(carId, out ModernCar? car))
        {
            return false;
        }

        ModernVoxelKey? previousLocation = car.State.Location;
        RemoveCarFromTraffic(car);
        cars[carId] = car.Remove();
        if (previousLocation is { } location)
        {
            SynchronizeTrafficVoxel(location.H, location.V);
        }

        Publish(ModernWorldChangeKind.Entity, previousLocation, "Car removed.");
        return true;
    }

    public bool AddRailTile(TileLocation location)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        if (!IsBuildableSurface(location)
            || !IsReusable(key)
            || Transport.HasRoad(location.H, location.V))
        {
            return false;
        }

        RemoveReusableEntityAt(ToVoxelKey(location));
        bool changed = Transport.AddRailTile(location.H, location.V, location.Z);
        if (changed)
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(location), "Rail tile added.");
        }

        return changed;
    }

    public bool AddRoadTile(TileLocation location, RoadContribution contribution)
    {
        if (!IsTransportBuildableSurface(location))
        {
            return false;
        }

        RemoveReusableEntityAt(ToVoxelKey(location));
        bool changed = Transport.AddRoadTile(location.H, location.V, contribution);
        if (changed)
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(location), "Road tile added.");
        }

        return changed;
    }

    public int AddRailLine(TileLocation from, TileLocation to)
    {
        return AddRailLine(from, to, ModernSpecialRailKind.Normal);
    }

    public int AddRailLine(TileLocation from, TileLocation to, ModernSpecialRailKind specialKind)
    {
        if (!TryCreateRailRoute(from, to, out IReadOnlyList<TileLocation> route)
            || !CanApplyRailRoute(route, specialKind, out IReadOnlyDictionary<(int H, int V), byte> railMasks))
        {
            return 0;
        }

        ReclaimTransportRoute(route);
        foreach (TileLocation location in route)
        {
            ApplySpecialRailBuildEffects(location, specialKind);
        }

        int changed = 0;
        foreach (TileLocation location in route)
        {
            if (Transport.AddRailTile(location.H, location.V, location.Z, specialKind, railMasks[(location.H, location.V)]))
            {
                changed++;
            }
            else if (Transport.SetRailDirectionMask(location.H, location.V, railMasks[(location.H, location.V)]))
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            SynchronizeTrafficVoxelsAlong(route);
            Spend(CalculateRailBuildCost(route, specialKind), ModernAccountGenre.Railway, $"Built {KindNameForCost(specialKind)}.");
            string kindName = specialKind == ModernSpecialRailKind.Normal ? "Rail" : $"{specialKind} rail";
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(to), $"{kindName} line changed {changed} tile(s).");
        }

        return changed;
    }

    public int AddRoadLine(TileLocation from, TileLocation to, RoadContribution contribution)
    {
        if (!CanBuildRoadLine(from, to))
        {
            return 0;
        }

        ReclaimTransportLine(from, to);
        int changed = Transport.AddRoadLine((from.H, from.V), (to.H, to.V), contribution);
        if (changed > 0)
        {
            SynchronizeTrafficVoxelsAlong(from, to);
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(to), $"Road line changed {changed} tile(s).");
        }

        return changed;
    }

    public bool RemoveTransportAt(TileLocation location)
    {
        if (RemoveRailServiceAt(location))
        {
            return true;
        }

        ModernVoxelKey key = ToVoxelKey(location);
        if (GetEntityAt(key) is { } entity && RemoveEntity(entity.EntityId))
        {
            return true;
        }

        bool hadRail = Transport.HasRail(location.H, location.V);
        TileLocation railLocation = hadRail ? location with { Z = GetRailLevel(location.H, location.V) } : location;
        ModernSpecialRailKind removedRailKind = Transport.SpecialRailTiles.GetValueOrDefault((location.H, location.V), ModernSpecialRailKind.Normal);
        if (hadRail && IsTrainOccupyingRailTile(location.H, location.V))
        {
            return false;
        }

        if (hadRail)
        {
            ApplySpecialRailRemoveEffects(railLocation, removedRailKind);
            DetachRailFromNeighbors(railLocation);
        }

        bool changed = Transport.RemoveAt(location.H, location.V);
        if (changed)
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
            if (hadRail)
            {
                Spend(CalculateRailDestroyCost(railLocation, removedRailKind), ModernAccountGenre.Railway, $"Removed {KindNameForCost(removedRailKind)}.");
            }

            Publish(ModernWorldChangeKind.Transport, key, "Transport removed.");
        }

        return changed;
    }

    private void DetachRailFromNeighbors(TileLocation location)
    {
        ModernLocation railLocation = ToLocation(location.H, location.V, location.Z);
        foreach (ModernDirection direction in ModernDirection.All)
        {
            ModernLocation neighborLocation = railLocation + direction;
            (int h, int v) = ToHv(neighborLocation);
            if (!IsInside(h, v) || !Transport.HasRail(h, v))
            {
                continue;
            }

            byte mask = GetRawRailMask(h, v);
            byte next = (byte)(mask & ~(1 << direction.Opposite.Index));
            if (next != mask)
            {
                Transport.SetRailDirectionMask(h, v, next);
            }
        }
    }

    public bool CanBuildRailLine(TileLocation from, TileLocation to)
    {
        return CanBuildRailLine(from, to, ModernSpecialRailKind.Normal);
    }

    public bool CanBuildRailLine(TileLocation from, TileLocation to, ModernSpecialRailKind specialKind)
    {
        return TryCreateRailRoute(from, to, out IReadOnlyList<TileLocation> route)
            && CanApplyRailRoute(route, specialKind, out _);
    }

    public IReadOnlyList<TileLocation> PreviewRailLine(TileLocation from, TileLocation to)
    {
        return TryCreateRailRoute(from, to, out IReadOnlyList<TileLocation> route)
            ? route
            : Array.Empty<TileLocation>();
    }

    private bool TryCreateRailRoute(TileLocation from, TileLocation to, out IReadOnlyList<TileLocation> route)
    {
        List<TileLocation> locations = new();
        route = locations;
        if (!IsInside(from.H, from.V) || !IsInside(to.H, to.V) || from.Z < 0 || from.Z >= Depth)
        {
            return false;
        }

        ModernLocation current = ToLocation(from.H, from.V, from.Z);
        ModernLocation target = ToLocation(to.H, to.V, from.Z);
        int guard = Width * Height * 2;
        while (true)
        {
            (int h, int v) = ToHv(current);
            if (!IsInside(h, v))
            {
                return false;
            }

            locations.Add(new TileLocation(h, v, from.Z));
            if (current == target)
            {
                return true;
            }

            if (guard-- <= 0)
            {
                locations.Clear();
                return false;
            }

            current = current.Toward(target);
        }
    }

    private bool CanApplyRailRoute(
        IReadOnlyList<TileLocation> route,
        ModernSpecialRailKind specialKind,
        out IReadOnlyDictionary<(int H, int V), byte> railMasks)
    {
        Dictionary<(int H, int V), byte> masks = new();
        railMasks = masks;
        if (route.Count == 0)
        {
            return false;
        }

        foreach (TileLocation location in route)
        {
            if (!CanBuildRailTile(location, specialKind))
            {
                return false;
            }
        }

        for (int i = 0; i < route.Count; i++)
        {
            TileLocation location = route[i];
            byte routeMask = RouteDirectionMask(route, i);
            byte existingMask = Transport.HasRail(location.H, location.V)
                ? GetRawRailMask(location.H, location.V)
                : (byte)0;
            if (!TryMergeRailMask(location, existingMask, routeMask, out byte candidate))
            {
                return false;
            }

            if (candidate == 0)
            {
                candidate = ModernRailPattern.DirectionMask(0, 4);
            }

            byte renderMask = NormalizeRailMask(candidate);
            if (CountBits(renderMask) > 3 || ModernRailPattern.FromDirectionMask(renderMask) is null)
            {
                return false;
            }

            masks[(location.H, location.V)] = candidate;
        }

        return true;
    }

    private bool TryMergeRailMask(TileLocation location, byte existingMask, byte routeMask, out byte candidate)
    {
        candidate = (byte)(existingMask | routeMask);
        if (existingMask == 0 || routeMask == 0 || (routeMask & ~existingMask) == 0)
        {
            return true;
        }

        byte newBits = (byte)(routeMask & ~existingMask);
        if (CountBits(routeMask) > 1)
        {
            return false;
        }

        if (CountBits(newBits) != 1)
        {
            return false;
        }

        int newDirection = FirstSetDirection(newBits);
        if (CountBits(existingMask) == 2 && !IsRailWellConnected(location.H, location.V, existingMask))
        {
            foreach (ModernDirection existingDirection in ModernDirection.All)
            {
                if ((existingMask & (1 << existingDirection.Index)) == 0)
                {
                    continue;
                }

                ModernDirection addedDirection = ModernDirection.FromIndex(newDirection);
                if (ModernDirection.Angle(existingDirection, addedDirection) >= 3)
                {
                    candidate = ModernRailPattern.DirectionMask(existingDirection.Index, addedDirection.Index);
                    return true;
                }
            }

            return false;
        }

        return true;
    }

    private bool IsRailWellConnected(int h, int v, byte mask)
    {
        ModernLocation location = ToLocation(h, v, GetRailLevel(h, v));
        int connected = 0;
        foreach (ModernDirection direction in ModernDirection.All)
        {
            if ((mask & (1 << direction.Index)) == 0)
            {
                continue;
            }

            ModernLocation neighbor = location + direction;
            (int neighborH, int neighborV) = ToHv(neighbor);
            if (!IsInside(neighborH, neighborV) || !Transport.HasRail(neighborH, neighborV))
            {
                continue;
            }

            byte neighborMask = GetRawRailMask(neighborH, neighborV);
            if ((neighborMask & (1 << direction.Opposite.Index)) != 0)
            {
                connected++;
            }
        }

        return connected >= 2;
    }

    private byte RouteDirectionMask(IReadOnlyList<TileLocation> route, int index)
    {
        TileLocation location = route[index];
        ModernLocation current = ToLocation(location.H, location.V, location.Z);
        byte mask = 0;
        if (index > 0)
        {
            TileLocation previous = route[index - 1];
            ModernDirection direction = current.GetDirectionTo(ToLocation(previous.H, previous.V, location.Z));
            mask |= (byte)(1 << direction.Index);
        }

        if (index < route.Count - 1)
        {
            TileLocation next = route[index + 1];
            ModernDirection direction = current.GetDirectionTo(ToLocation(next.H, next.V, location.Z));
            mask |= (byte)(1 << direction.Index);
        }

        if (mask == 0)
        {
            return ModernRailPattern.DirectionMask(0, 4);
        }

        if (CountBits(mask) == 1 && !Transport.HasRail(location.H, location.V))
        {
            int direction = FirstSetDirection(mask);
            mask |= (byte)(1 << ((direction + 4) % 8));
        }

        return mask;
    }

    private bool CanBuildRailTile(TileLocation location, ModernSpecialRailKind specialKind)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        bool canReuseVoxel = IsReusable(key) || Transport.HasRail(location.H, location.V);
        ModernSpecialRailKind existingKind = Transport.SpecialRailTiles.GetValueOrDefault((location.H, location.V), ModernSpecialRailKind.Normal);
        bool specialPurposeConflict = existingKind != ModernSpecialRailKind.Normal && existingKind != specialKind;
        if (!IsInside(location.H, location.V)
            || location.Z < 0
            || location.Z >= Depth
            || !canReuseVoxel
            || Transport.HasRoad(location.H, location.V)
            || specialPurposeConflict)
        {
            return false;
        }

        TerrainTilePreview terrain = GetTerrainTile(location.H, location.V);
        bool onSurface = terrain.IsFlat && terrain.SurfaceLevel == location.Z;
        bool overWaterBridge = specialKind == ModernSpecialRailKind.Bridge
            && terrain.IsFlat
            && terrain.SurfaceLevel < WaterLevel
            && location.Z >= WaterLevel;
        bool elevatedSpecialRail = specialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported
            && terrain.IsFlat
            && location.Z > terrain.SurfaceLevel;
        bool tunnelCut = specialKind == ModernSpecialRailKind.Tunnel
            && terrain.SurfaceLevel == location.Z
            && terrain.SurfaceLevel >= WaterLevel;
        if (!onSurface && !overWaterBridge && !elevatedSpecialRail && !tunnelCut)
        {
            return false;
        }

        return specialKind switch
        {
            ModernSpecialRailKind.Bridge => (terrain.SurfaceLevel <= WaterLevel || location.Z > terrain.SurfaceLevel)
                && CanBuildBridgePiers(location, everyOtherTile: true),
            ModernSpecialRailKind.SteelSupported => location.Z > terrain.SurfaceLevel
                && CanBuildBridgePiers(location, everyOtherTile: false),
            ModernSpecialRailKind.Tunnel => terrain.SurfaceLevel >= WaterLevel,
            ModernSpecialRailKind.Garage => terrain.IsFlat && terrain.SurfaceLevel == location.Z,
            ModernSpecialRailKind.Unsupported => false,
            _ => terrain.SurfaceLevel >= WaterLevel
        };
    }

    private void ApplySpecialRailBuildEffects(TileLocation location, ModernSpecialRailKind specialKind)
    {
        ModernSpecialRailKind existingKind = Transport.SpecialRailTiles.GetValueOrDefault((location.H, location.V), ModernSpecialRailKind.Normal);
        if (existingKind == ModernSpecialRailKind.Tunnel && specialKind != ModernSpecialRailKind.Tunnel)
        {
            ApplySpecialRailRemoveEffects(location, existingKind);
        }
        else if (existingKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported
            && existingKind != specialKind)
        {
            ApplySpecialRailRemoveEffects(location, existingKind);
        }

        if (specialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported)
        {
            BuildBridgePiers(location, specialKind == ModernSpecialRailKind.Bridge);
            return;
        }

        if (specialKind != ModernSpecialRailKind.Tunnel || tunnelTerrainBackups.ContainsKey((location.H, location.V)))
        {
            return;
        }

        TerrainTilePreview terrain = GetTerrainTile(location.H, location.V);
        if (terrain.IsFlat)
        {
            return;
        }

        tunnelTerrainBackups[(location.H, location.V)] = GetTileFineHeights(location.H, location.V);
        int flatFineHeight = terrain.SurfaceLevel * 4;
        SetTileFineHeights(location.H, location.V, flatFineHeight, flatFineHeight, flatFineHeight, flatFineHeight);
        RebuildTilesFromFineHeights();
        RebuildTrafficVoxels();
    }

    private void ApplySpecialRailRemoveEffects(TileLocation location, ModernSpecialRailKind specialKind)
    {
        if (specialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported)
        {
            RemoveBridgePiers(location);
            return;
        }

        if (specialKind != ModernSpecialRailKind.Tunnel
            || !tunnelTerrainBackups.Remove((location.H, location.V), out int[]? backup)
            || backup.Length != 4)
        {
            return;
        }

        SetTileFineHeights(location.H, location.V, backup[0], backup[1], backup[2], backup[3]);
        RebuildTilesFromFineHeights();
        RebuildTrafficVoxels();
    }

    private bool CanBuildBridgePiers(TileLocation location, bool everyOtherTile)
    {
        if (everyOtherTile && ((location.H + location.V) & 1) != 0)
        {
            return true;
        }

        int ground = GetGroundLevel(location.H, location.V);
        if (location.Z <= ground)
        {
            return true;
        }

        for (int z = ground; z < location.Z; z++)
        {
            ModernVoxelKey key = new(location.H, location.V, z);
            ModernVoxelOccupancy? occupancy = GetVoxel(key);
            if (occupancy is not null && !bridgePierVoxels.Contains(key))
            {
                return false;
            }
        }

        return true;
    }

    private void BuildBridgePiers(TileLocation location, bool everyOtherTile)
    {
        if (!CanBuildBridgePiers(location, everyOtherTile) || everyOtherTile && ((location.H + location.V) & 1) != 0)
        {
            return;
        }

        int ground = GetGroundLevel(location.H, location.V);
        for (int z = ground; z < location.Z; z++)
        {
            ModernVoxelKey key = new(location.H, location.V, z);
            bridgePierVoxels.Add(key);
            voxels[key] = new ModernVoxelOccupancy(key, ModernVoxelKind.Structure, $"BridgePier:{location.H}:{location.V}:{z}", null);
        }
    }

    private void RemoveBridgePiers(TileLocation location)
    {
        foreach (ModernVoxelKey key in bridgePierVoxels
            .Where(key => key.H == location.H && key.V == location.V && key.Z < location.Z)
            .ToArray())
        {
            bridgePierVoxels.Remove(key);
            if (voxels[key]?.EntityId == $"BridgePier:{key.H}:{key.V}:{key.Z}")
            {
                voxels.Remove(key);
            }
        }
    }

    private int[] GetTileFineHeights(int h, int v)
    {
        (int topX, int topY) = GetCornerVertex(h, v, TerrainCorner.Top);
        (int rightX, int rightY) = GetCornerVertex(h, v, TerrainCorner.Right);
        (int bottomX, int bottomY) = GetCornerVertex(h, v, TerrainCorner.Bottom);
        (int leftX, int leftY) = GetCornerVertex(h, v, TerrainCorner.Left);
        return
        [
            fineHeights[topX, topY],
            fineHeights[rightX, rightY],
            fineHeights[bottomX, bottomY],
            fineHeights[leftX, leftY]
        ];
    }

    private void SetTileFineHeights(int h, int v, int top, int right, int bottom, int left)
    {
        (int topX, int topY) = GetCornerVertex(h, v, TerrainCorner.Top);
        (int rightX, int rightY) = GetCornerVertex(h, v, TerrainCorner.Right);
        (int bottomX, int bottomY) = GetCornerVertex(h, v, TerrainCorner.Bottom);
        (int leftX, int leftY) = GetCornerVertex(h, v, TerrainCorner.Left);
        fineHeights[topX, topY] = Math.Clamp(top, 0, MaxFineHeight);
        fineHeights[rightX, rightY] = Math.Clamp(right, 0, MaxFineHeight);
        fineHeights[bottomX, bottomY] = Math.Clamp(bottom, 0, MaxFineHeight);
        fineHeights[leftX, leftY] = Math.Clamp(left, 0, MaxFineHeight);
    }

    private long CalculateRailBuildCost(TileLocation from, TileLocation to, ModernSpecialRailKind specialKind)
    {
        return TryCreateRailRoute(from, to, out IReadOnlyList<TileLocation> route)
            ? CalculateRailBuildCost(route, specialKind)
            : 0;
    }

    private long CalculateRailBuildCost(IEnumerable<TileLocation> route, ModernSpecialRailKind specialKind)
    {
        long unit = specialKind == ModernSpecialRailKind.Garage ? 6_500_000L : 6_000_000L;
        return route.Sum(location => unit * RailCostMultiplier(location, specialKind));
    }

    private long CalculateRailDestroyCost(TileLocation location, ModernSpecialRailKind specialKind)
    {
        long unit = specialKind == ModernSpecialRailKind.Garage ? 200_000L : 2_000_000L;
        return unit * RailCostMultiplier(location, specialKind);
    }

    private int RailCostMultiplier(TileLocation location, ModernSpecialRailKind specialKind)
    {
        if (specialKind == ModernSpecialRailKind.Tunnel)
        {
            return 2;
        }

        int height = location.Z - GetGroundLevel(location.H, location.V);
        return height < 0 ? height * -2 : height + 1;
    }

    private static string KindNameForCost(ModernSpecialRailKind specialKind)
    {
        return specialKind switch
        {
            ModernSpecialRailKind.Bridge => "bridge rail",
            ModernSpecialRailKind.SteelSupported => "steel-supported rail",
            ModernSpecialRailKind.Tunnel => "tunnel rail",
            ModernSpecialRailKind.Garage => "train garage rail",
            _ => "rail"
        };
    }

    public bool CanBuildRoadLine(TileLocation from, TileLocation to)
    {
        if (from.H != to.H && from.V != to.V)
        {
            return false;
        }

        return EnumerateLine(from, to).All(IsTransportBuildableSurface);
    }

    public IReadOnlyList<MapRailObject> CreateRailObjects()
    {
        return Transport.RailTiles
            .Select(tile => CreateRailObject(tile.H, tile.V))
            .Where(rail => rail is not null)
            .Cast<MapRailObject>()
            .ToArray();
    }

    public IReadOnlyList<ModernRailRoad> CreateRailRoads()
    {
        return Transport.RailTiles
            .Select(tile => CreateRailRoad(tile.H, tile.V))
            .Where(rail => rail is not null)
            .Cast<ModernRailRoad>()
            .ToArray();
    }

    private MapRailObject? CreateRailObject(int h, int v)
    {
        byte mask = GetLegacyRailMask(h, v);
        return ModernRailPattern.FromDirectionMask(mask) is { } pattern
            ? new MapRailObject(
                h,
                v,
                GetRailLevel(h, v),
                pattern,
                Transport.SpecialRailTiles.GetValueOrDefault((h, v), ModernSpecialRailKind.Normal))
            : null;
    }

    private ModernRailRoad? CreateRailRoad(int h, int v)
    {
        byte mask = GetLegacyRailMask(h, v);
        RailPatternDefinition? pattern = ModernRailPattern.FromDirectionMask(mask);
        ModernRailRoadKind kind = ModernRailPattern.KindFromDirectionMask(mask);
        return kind == ModernRailRoadKind.Unsupported
            ? null
            : new ModernRailRoad(new ModernVoxelKey(h, v, GetRailLevel(h, v)), mask, kind, pattern);
    }

    public IReadOnlyList<MapRoadObject> CreateRoadObjects()
    {
        return Transport.CreateRoadObjects();
    }

    public IReadOnlyList<ModernRoadSegment> CreateRoadSegments()
    {
        return Transport.CreateRoadSegments(GetGroundLevel);
    }

    public IReadOnlyList<ModernVoxelKey> GetPlatformVoxels(ModernPlatform platform)
    {
        return EnumeratePlatformVoxels(platform).ToArray();
    }

    public ModernTrainCarRenderPose GetTrainCarRenderPose(ModernTrainCarPlacement car)
    {
        ModernRailRoad? railRoad = CreateRailRoad(car.Location.H, car.Location.V);
        if (railRoad is null)
        {
            return new ModernTrainCarRenderPose((car.DirectionIndex * 2) & 15, 0, 0);
        }

        int d1 = car.DirectionIndex;
        int d2 = railRoad.Guide(car.Direction).Index;
        if (d1 == d2)
        {
            return new ModernTrainCarRenderPose((d1 * 2) & 15, 0, 0);
        }

        int diff = (d2 - d1) & 7;
        if (diff == 7)
        {
            diff = -1;
        }

        int dd = (d2 * 2 + diff * 3) & 15;
        int offsetX = 2 < dd && dd < 10 ? 3 : -3;
        int offsetY = 6 < dd && dd <= 14 ? 2 : -2;
        int angle = (d1 * 2 + diff) & 15;
        return new ModernTrainCarRenderPose(angle, offsetX, offsetY);
    }

    public ModernWorldSnapshot ToSnapshot()
    {
        int fineWidth = fineHeights.GetLength(0);
        int fineHeight = fineHeights.GetLength(1);
        int[] serializedFineHeights = new int[fineWidth * fineHeight];
        for (int y = 0; y < fineHeight; y++)
        {
            for (int x = 0; x < fineWidth; x++)
            {
                serializedFineHeights[y * fineWidth + x] = fineHeights[x, y];
            }
        }

        ModernRoadSnapshot[] roads = Transport.RoadTiles
            .Select(entry => new ModernRoadSnapshot(entry.Key.H, entry.Key.V, entry.Value.Id))
            .ToArray();

        ModernRailSnapshot[] rails = Transport.RailTiles
            .Select(tile => new ModernRailSnapshot(
                tile.H,
                tile.V,
                Transport.SpecialRailTiles.GetValueOrDefault(tile, ModernSpecialRailKind.Normal),
                tunnelTerrainBackups.TryGetValue(tile, out int[]? backup) ? backup : null,
                GetRailLevel(tile.H, tile.V),
                GetRawRailMask(tile.H, tile.V)))
            .ToArray();

        ModernEntitySnapshot[] entitySnapshots = entities.Values
            .Select(entity => new ModernEntitySnapshot(
                entity.EntityId,
                entity.Kind,
                entity.H,
                entity.V,
                entity.Z,
                entity.FootprintH,
                entity.FootprintV,
                entity.FootprintZ,
                entity.ContributionId,
                entity.ResolvedContributionId,
                entity.IsOwned,
                entity.IsSilentlyReclaimable,
                entity.EntityValue))
            .ToArray();

        ModernCarSnapshot[] carSnapshots = cars.Values
            .Select(car => new ModernCarSnapshot(
                car.CarId,
                car.Kind,
                car.State.Placement,
                car.State.Location?.H,
                car.State.Location?.V,
                car.State.Location?.Z,
                car.State.DirectionIndex))
            .ToArray();

        ModernStationSnapshot[] stationSnapshots = stations.Values
            .Select(station => new ModernStationSnapshot(
                station.StationId,
                station.H,
                station.V,
                station.Z,
                station.Contribution.Id,
                station.Name,
                station.Stats))
            .ToArray();

        ModernPlatformSnapshot[] platformSnapshots = platforms.Values
            .Select(platform => new ModernPlatformSnapshot(
                platform.PlatformId,
                platform.H,
                platform.V,
                platform.Z,
                platform.DirectionIndex,
                platform.Length,
                platform.Style,
                platform.StationId))
            .ToArray();

        ModernTrainSnapshot[] trainSnapshots = trains.Values
            .Select(train => new ModernTrainSnapshot(
                train.TrainId,
                train.Contribution.Id,
                train.Cars.Select(car => new ModernTrainCarPlacementSnapshot(
                    car.CarContributionId,
                    car.Location.H,
                    car.Location.V,
                    car.Location.Z,
                    car.DirectionIndex)).ToArray(),
                train.MinuteAccumulator,
                train.State,
                train.StopRemainingMinutes,
                train.MoveCount,
                train.PassengerCapacity,
                train.PassengerSeatedCapacity,
                train.PassengerCount,
                train.PassengerSourceLocation,
                train.CurrentStopPlatformId,
                train.LastStoppedPlatformId,
                train.GarageLocation,
                train.GarageDirectionIndex))
            .ToArray();

        return new ModernWorldSnapshot(
            Name,
            Width,
            Height,
            WaterLevel,
            Clock,
            Account,
            fineWidth,
            fineHeight,
            serializedFineHeights,
            rails,
            roads,
            carSnapshots,
            entitySnapshots,
            stationSnapshots,
            platformSnapshots,
            trainSnapshots);
    }

    public static ModernWorld FromSnapshot(
        ModernWorldSnapshot snapshot,
        IReadOnlyDictionary<string, RoadContribution> roadContributions,
        IReadOnlyDictionary<string, LandContribution> landContributions,
        IReadOnlyDictionary<string, SpriteContribution> spriteContributions,
        IReadOnlyDictionary<string, StationContribution> stationContributions,
        IReadOnlyDictionary<string, TrainContribution> trainContributions)
    {
        int[,] fineHeights = new int[snapshot.FineHeightWidth, snapshot.FineHeightHeight];
        for (int y = 0; y < snapshot.FineHeightHeight; y++)
        {
            for (int x = 0; x < snapshot.FineHeightWidth; x++)
            {
                int index = y * snapshot.FineHeightWidth + x;
                fineHeights[x, y] = index < snapshot.FineHeights.Length
                    ? Math.Clamp(snapshot.FineHeights[index], 0, MaxFineHeight)
                    : 0;
            }
        }

        ModernWorld world = new(
            snapshot.Name,
            snapshot.Width,
            snapshot.Height,
            snapshot.WaterLevel,
            fineHeights,
            snapshot.Clock,
            snapshot.Account);

        foreach (ModernRailSnapshot rail in snapshot.Rails)
        {
            int z = rail.Z ?? world.GetGroundLevel(rail.H, rail.V);
            world.Transport.AddRailTile(rail.H, rail.V, z, rail.SpecialKind, rail.DirectionMask);
            if (rail.SpecialKind == ModernSpecialRailKind.Tunnel && rail.TerrainFineHeights is { Count: 4 })
            {
                world.tunnelTerrainBackups[(rail.H, rail.V)] = rail.TerrainFineHeights.ToArray();
            }
            else if (rail.SpecialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported)
            {
                world.BuildBridgePiers(new TileLocation(rail.H, rail.V, z), rail.SpecialKind == ModernSpecialRailKind.Bridge);
            }
        }

        foreach (ModernRoadSnapshot road in snapshot.Roads)
        {
            if (roadContributions.TryGetValue(road.RoadContributionId, out RoadContribution? contribution))
            {
                world.Transport.AddRoadTile(road.H, road.V, contribution);
            }
        }

        foreach (ModernEntitySnapshot entity in snapshot.Entities)
        {
            ModernPlacedEntity? placed = entity.Kind switch
            {
                ModernEntityKind.Land => RestoreLandEntity(entity, landContributions),
                ModernEntityKind.Structure => RestoreStructureEntity(entity, spriteContributions),
                _ => null
            };

            if (placed is not null)
            {
                world.AddEntity(placed);
            }
        }

        foreach (ModernCarSnapshot carSnapshot in snapshot.Cars)
        {
            ModernCar car = new(carSnapshot.CarId, carSnapshot.Kind, ModernCarState.Unplaced);
            if (world.AddCar(car)
                && carSnapshot.Placement == ModernCarPlacementKind.InsideMap
                && carSnapshot.H is { } h
                && carSnapshot.V is { } v
                && carSnapshot.Z is { } z
                && carSnapshot.DirectionIndex is { } directionIndex)
            {
                world.PlaceCar(car.CarId, new ModernVoxelKey(h, v, z), ModernDirection.FromIndex(directionIndex));
            }
        }

        foreach (ModernStationSnapshot stationSnapshot in snapshot.Stations)
        {
            if (stationContributions.TryGetValue(stationSnapshot.ContributionId, out StationContribution? contribution))
            {
                world.AddStation(new ModernStation(
                    stationSnapshot.StationId,
                    stationSnapshot.H,
                    stationSnapshot.V,
                    stationSnapshot.Z,
                    contribution,
                    stationSnapshot.Stats)
                {
                    Name = stationSnapshot.Name
                });
            }
        }

        foreach (ModernPlatformSnapshot platformSnapshot in snapshot.Platforms)
        {
            world.AddPlatform(new ModernPlatform(
                platformSnapshot.PlatformId,
                platformSnapshot.H,
                platformSnapshot.V,
                platformSnapshot.Z,
                platformSnapshot.DirectionIndex,
                platformSnapshot.Length,
                platformSnapshot.Style,
                platformSnapshot.StationId));
        }

        foreach (ModernTrainSnapshot trainSnapshot in snapshot.Trains)
        {
            if (trainContributions.TryGetValue(trainSnapshot.ContributionId, out TrainContribution? contribution))
            {
                ModernTrainCarPlacement[] placements = trainSnapshot.Cars
                    .Select(car => new ModernTrainCarPlacement(
                        car.CarContributionId,
                        new ModernVoxelKey(car.H, car.V, car.Z),
                        car.DirectionIndex))
                    .ToArray();
                world.AddTrain(new ModernTrain(
                    trainSnapshot.TrainId,
                    contribution,
                    placements,
                    trainSnapshot.MinuteAccumulator,
                    trainSnapshot.State,
                    trainSnapshot.StopRemainingMinutes,
                    trainSnapshot.MoveCount,
                    trainSnapshot.PassengerCapacity,
                    trainSnapshot.PassengerSeatedCapacity,
                    trainSnapshot.PassengerCount,
                    trainSnapshot.PassengerSourceLocation,
                    trainSnapshot.CurrentStopPlatformId,
                    trainSnapshot.LastStoppedPlatformId,
                    trainSnapshot.GarageLocation,
                    trainSnapshot.GarageDirectionIndex));
            }
        }

        world.RebuildTrafficVoxels();
        world.Publish(ModernWorldChangeKind.Reset, null, "World loaded from snapshot.");
        return world;
    }

    public static ModernWorld CreateSample(IReadOnlyList<RoadContribution> roads, int width = 34, int height = 34, int waterLevel = 0)
    {
        ModernWorld world = new(
            "Flat rail loop test world",
            width,
            height,
            waterLevel,
            BuildFlatFineHeights(width, height),
            ModernWorldClock.Default,
            ModernAccountState.Default);

        world.AddFlatRailLoop();

        world.RebuildTrafficVoxels();
        return world;
    }

    public static ModernWorld CreateNew(ModernWorldCreationOptions options)
    {
        ModernWorldCreationOptions normalized = options.Normalize();
        int[,] heights = normalized.TerrainKind switch
        {
            ModernWorldTerrainKind.Flat => BuildFlatFineHeights(normalized.Width, normalized.Height),
            _ => BuildRepresentableFineHeights(normalized.Width, normalized.Height, normalized.WaterLevel)
        };

        ModernWorld world = new(
            normalized.Name,
            normalized.Width,
            normalized.Height,
            normalized.WaterLevel,
            heights,
            normalized.Clock ?? ModernWorldClock.Default,
            new ModernAccountState(normalized.InitialCash));

        world.RebuildTrafficVoxels();
        world.Publish(ModernWorldChangeKind.Reset, null, "New world created.");
        return world;
    }

    private void AddFlatRailLoop()
    {
        ModernLocation[] loop =
        {
            new(20, 18, 0),
            new(30, 18, 0),
            new(34, 22, 0),
            new(34, 27, 0),
            new(30, 31, 0),
            new(20, 31, 0),
            new(16, 27, 0),
            new(16, 22, 0)
        };

        for (int i = 0; i < loop.Length; i++)
        {
            AddRailLocationLine(loop[i], loop[(i + 1) % loop.Length]);
        }
    }

    private void AddRailLocationLine(ModernLocation from, ModernLocation to)
    {
        ModernLocation current = from;
        AddRailAtLocation(current);
        int guard = 0;
        while (current != to && guard++ < 512)
        {
            current = current.Toward(to);
            AddRailAtLocation(current);
        }
    }

    private void AddRailAtLocation(ModernLocation location)
    {
        (int h, int v) = ToHv(location);
        if (IsInside(h, v))
        {
            Transport.AddRailTile(h, v);
        }
    }

    private static ModernPlacedEntity? RestoreLandEntity(
        ModernEntitySnapshot snapshot,
        IReadOnlyDictionary<string, LandContribution> landContributions)
    {
        if (!landContributions.TryGetValue(snapshot.ContributionId, out LandContribution? contribution))
        {
            return null;
        }

        LandContribution? resolved = null;
        if (!string.IsNullOrWhiteSpace(snapshot.ResolvedContributionId))
        {
            landContributions.TryGetValue(snapshot.ResolvedContributionId, out resolved);
        }

        return ModernPlacedEntity.Land(snapshot.H, snapshot.V, snapshot.Z, contribution, resolved);
    }

    private static ModernPlacedEntity? RestoreStructureEntity(
        ModernEntitySnapshot snapshot,
        IReadOnlyDictionary<string, SpriteContribution> spriteContributions)
    {
        if (!spriteContributions.TryGetValue(snapshot.ContributionId, out SpriteContribution? contribution))
        {
            return null;
        }

        SpriteFrame? frame = contribution.Frames.FirstOrDefault(candidate => candidate.IsLoadable);
        return frame is null ? null : ModernPlacedEntity.Structure(snapshot.H, snapshot.V, snapshot.Z, contribution, frame);
    }

    private int GetStationPopulation(ModernStation station)
    {
        ModernVoxelKey stationLocation = new(station.H, station.V, station.Z);
        return entities.Values.Sum(entity => EstimatePopulationContribution(stationLocation, entity));
    }

    private static int EstimatePopulationContribution(ModernVoxelKey stationLocation, ModernPlacedEntity entity)
    {
        int distance = DistanceToEntity(stationLocation, entity);
        if (distance >= StationReachRange)
        {
            return 0;
        }

        int reachWeight = StationReachRange - distance;
        int footprint = Math.Max(1, entity.FootprintH) * Math.Max(1, entity.FootprintV);
        if (entity.Kind == ModernEntityKind.Land)
        {
            return reachWeight * 3;
        }

        int height = Math.Max(1, entity.FootprintZ);
        int valueWeight = (int)Math.Clamp(entity.EntityValue / 20_000_000L, 1, 250);
        return reachWeight * footprint * height * valueWeight;
    }

    private static int DistanceToEntity(ModernVoxelKey location, ModernPlacedEntity entity)
    {
        int minH = entity.H;
        int maxH = entity.H + Math.Max(1, entity.FootprintH) - 1;
        int minV = entity.V;
        int maxV = entity.V + Math.Max(1, entity.FootprintV) - 1;
        int minZ = entity.Z;
        int maxZ = entity.Z + Math.Max(1, entity.FootprintZ) - 1;
        int dh = location.H < minH ? minH - location.H : location.H > maxH ? location.H - maxH : 0;
        int dv = location.V < minV ? minV - location.V : location.V > maxV ? location.V - maxV : 0;
        int dz = location.Z < minZ ? minZ - location.Z : location.Z > maxZ ? location.Z - maxZ : 0;
        return dh + dv + dz;
    }

    private ModernStationDevelopmentSignal CreateDevelopmentSignal(ModernStation station)
    {
        int population = GetStationPopulation(station);
        return new ModernStationDevelopmentSignal(
            station.StationId,
            station.Name,
            new ModernVoxelKey(station.H, station.V, station.Z),
            population,
            station.Stats.WaitingPassengers(population),
            station.Stats.ScoreImported,
            station.Stats.ScoreExported,
            station.Stats.ScoreTrains,
            station.Stats.DevelopmentStrength,
            station.Stats.DevelopmentQuantity);
    }

    private string? FindNearestStationId(ModernPlatform platform)
    {
        ModernVoxelKey[] platformVoxels = EnumeratePlatformVoxels(platform).ToArray();
        return stations.Values
            .Select(station => new
            {
                Station = station,
                Distance = station.OccupiedVoxels
                    .SelectMany(stationVoxel => platformVoxels.Select(platformVoxel =>
                        Math.Abs(stationVoxel.H - platformVoxel.H) + Math.Abs(stationVoxel.V - platformVoxel.V) + Math.Abs(stationVoxel.Z - platformVoxel.Z)))
                    .DefaultIfEmpty(int.MaxValue)
                    .Min()
            })
            .Where(candidate => candidate.Distance <= 3)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Station.StationId)
            .FirstOrDefault();
    }

    private IEnumerable<ModernVoxelKey> EnumeratePlatformVoxels(ModernPlatform platform)
    {
        ModernLocation current = ToLocation(new ModernVoxelKey(platform.H, platform.V, platform.Z));
        for (int i = 0; i < Math.Max(1, platform.Length); i++)
        {
            (int h, int v) = ToHv(current);
            yield return new ModernVoxelKey(h, v, current.Z);
            current += platform.Direction;
        }
    }

    private bool IsStraightRailAlong(int h, int v, ModernDirection direction)
    {
        ModernRailRoad? railRoad = CreateRailRoad(h, v);
        return railRoad is not null
            && railRoad.Kind == ModernRailRoadKind.Single
            && railRoad.HasRail(direction)
            && railRoad.HasRail(direction.Opposite);
    }

    private void AdvanceTrains(long minutes)
    {
        if (minutes <= 0 || trains.Count == 0)
        {
            return;
        }

        foreach (ModernTrain train in trains.Values.ToArray())
        {
            if (!train.IsPlaced)
            {
                continue;
            }

            long accumulator = train.MinuteAccumulator + minutes;
            ModernTrain current = train;
            int guard = 0;
            while (accumulator > 0 && guard++ < 256)
            {
                if (current.State == ModernTrainState.StoppingAtStation)
                {
                    long consumed = Math.Min(accumulator, Math.Max(1, current.StopRemainingMinutes));
                    accumulator -= consumed;
                    current = current with { StopRemainingMinutes = Math.Max(0, current.StopRemainingMinutes - consumed) };
                    if (current.StopRemainingMinutes > 0)
                    {
                        break;
                    }

                    current = LoadPassengersAndResume(current);
                    continue;
                }

                int stepMinutes = Math.Max(1, current.Contribution.MinutesPerVoxel);
                if (accumulator < stepMinutes)
                {
                    break;
                }

                ModernTrain? stopping = TryBeginStationStop(current);
                if (stopping is not null)
                {
                    current = stopping;
                    continue;
                }

                accumulator -= stepMinutes;
                current = MoveTrainOneTile(current);
            }

            trains[current.TrainId] = current with { MinuteAccumulator = accumulator };
        }
    }

    private ModernTrain MoveTrainOneTile(ModernTrain train)
    {
        if (train.Head is not { } head)
        {
            return train;
        }

        ModernRailRoad? railRoad = CreateRailRoad(head.Location.H, head.Location.V);
        if (railRoad is null)
        {
            return train with { State = ModernTrainState.EmergencyStopping };
        }

        ModernDirection direction = railRoad.Guide(head.Direction);
        if (!TryStepOnRail(head.Location, direction, out ModernVoxelKey next))
        {
            return ReverseTrain(train) with { State = ModernTrainState.EmergencyStopping };
        }
        if (IsTrainOccupying(next, train.TrainId))
        {
            return train with { State = ModernTrainState.EmergencyStopping };
        }

        List<ModernTrainCarPlacement> moved = new()
        {
            new ModernTrainCarPlacement(head.CarContributionId, next, direction.Index)
        };

        for (int i = 1; i < train.Cars.Count; i++)
        {
            ModernTrainCarPlacement previous = train.Cars[i - 1];
            moved.Add(new ModernTrainCarPlacement(
                train.Cars[i].CarContributionId,
                previous.Location,
                previous.DirectionIndex));
        }

        int moveCount = train.MoveCount + 1;
        if ((train.MoveCount & 3) == 0)
        {
            Spend((train.Length * 20L + train.PassengerCount / 20L) * 2_000L, ModernAccountGenre.Railway, "Train running cost.");
        }

        string? lastStoppedPlatformId = GetPlatformIdAt(next) == train.LastStoppedPlatformId
            ? train.LastStoppedPlatformId
            : null;

        if (moved.All(car => IsGarageRail(car.Location.H, car.Location.V)))
        {
            return train with
            {
                Cars = Array.Empty<ModernTrainCarPlacement>(),
                State = ModernTrainState.InGarage,
                MinuteAccumulator = 0,
                StopRemainingMinutes = 0,
                MoveCount = moveCount,
                CurrentStopPlatformId = null,
                LastStoppedPlatformId = lastStoppedPlatformId,
                GarageLocation = next,
                GarageDirectionIndex = direction.Index
            };
        }

        return train with
        {
            Cars = moved,
            State = ModernTrainState.Moving,
            MoveCount = moveCount,
            LastStoppedPlatformId = lastStoppedPlatformId,
            GarageLocation = null,
            GarageDirectionIndex = null
        };
    }

    private ModernTrain ReverseTrain(ModernTrain train)
    {
        ModernTrainCarPlacement[] reversed = train.Cars
            .Reverse()
            .Select(car => car with { DirectionIndex = car.Direction.Opposite.Index })
            .ToArray();
        return train with { Cars = reversed };
    }

    private ModernTrain? TryBeginStationStop(ModernTrain train)
    {
        if (train.Head is not { } head)
        {
            return null;
        }

        PlatformStop? stop = FindPlatformStop(train, head);
        if (stop is null || stop.Value.Platform.StationId is null || stop.Value.Platform.PlatformId == train.LastStoppedPlatformId)
        {
            return null;
        }

        return train with
        {
            State = ModernTrainState.StoppingAtStation,
            StopRemainingMinutes = 30,
            CurrentStopPlatformId = stop.Value.Platform.PlatformId,
            LastStoppedPlatformId = stop.Value.Platform.PlatformId,
            PassengerCount = UnloadPassengers(train, stop.Value.Platform)
        };
    }

    private ModernTrain LoadPassengersAndResume(ModernTrain train)
    {
        int passengers = 0;
        ModernVoxelKey? sourceLocation = null;
        if (train.CurrentStopPlatformId is { } platformId
            && platforms.TryGetValue(platformId, out ModernPlatform? platform)
            && platform.StationId is { } stationId
            && stations.TryGetValue(stationId, out ModernStation? station)
            && train.Head is { } head)
        {
            passengers = LoadPassengers(station, train, GetTrainPassengerPackingCapacity(train));
            sourceLocation = passengers > 0 ? head.Location : null;
        }

        return train with
        {
            State = ModernTrainState.Moving,
            StopRemainingMinutes = 0,
            PassengerCount = passengers,
            PassengerSourceLocation = sourceLocation,
            CurrentStopPlatformId = null
        };
    }

    private int UnloadPassengers(ModernTrain train, ModernPlatform platform)
    {
        if (platform.StationId is not { } stationId
            || !stations.TryGetValue(stationId, out ModernStation? station))
        {
            return train.PassengerCount;
        }

        int unloaded = Math.Max(0, train.PassengerCount);
        double developmentQuantity = unloaded;
        if (train.PassengerSourceLocation is { } source && train.Head is { } head)
        {
            int distance = Math.Max(1, Math.Abs(source.H - head.Location.H) + Math.Abs(source.V - head.Location.V) + Math.Abs(source.Z - head.Location.Z));
            Earn(unloaded * train.Contribution.Fare * distance * 2L, ModernAccountGenre.Railway, "Passenger fare income.");
            developmentQuantity = Math.Min(station.Stats.ScoreImported / 24.0, unloaded);
        }

        stations[stationId] = station with { Stats = station.Stats.RecordArrival(unloaded, developmentQuantity) };
        return 0;
    }

    private int LoadPassengers(ModernStation station, ModernTrain train, int passengerPackingCapacity)
    {
        int population = GetStationPopulation(station);
        ModernStationStats stats = station.Stats;
        int passengerCount = 0;
        if (population > 0)
        {
            int available = stats.WaitingPassengers(population);
            passengerCount = Math.Min(
                Math.Max(0, passengerPackingCapacity),
                (int)(available * Math.Max(0, train.Contribution.Amenity) * 0.01f * 0.3f));
        }

        stations[station.StationId] = station with { Stats = stats.RecordDeparture(passengerCount) };
        return passengerCount;
    }

    private int GetTrainPassengerPackingCapacity(ModernTrain train)
    {
        int capacity = train.EffectivePassengerCapacity;
        int seatedCapacity = train.EffectivePassengerSeatedCapacity;
        int seated = seatedCapacity > capacity ? capacity : Math.Max(0, seatedCapacity);
        return seated + (capacity - seated) * 2;
    }

    private PlatformStop? FindPlatformStop(ModernTrain train, ModernTrainCarPlacement head)
    {
        if (!platformVoxels.TryGetValue(head.Location, out string? platformId)
            || !platforms.TryGetValue(platformId, out ModernPlatform? platform)
            || platform.StationId is null)
        {
            return null;
        }

        ModernVoxelKey[] platformVoxelsForPlatform = EnumeratePlatformVoxels(platform).ToArray();
        int index = Array.IndexOf(platformVoxelsForPlatform, head.Location);
        if (index < 0)
        {
            return null;
        }

        if (platform.Direction != head.Direction && platform.Direction != head.Direction.Opposite)
        {
            return null;
        }

        int stopIndex = platform.Direction == head.Direction
            ? (platform.Length + train.Length) / 2 - 1
            : (platform.Length - train.Length) / 2;

        return stopIndex == index && stopIndex >= 0 && stopIndex < platform.Length
            ? new PlatformStop(platform, index)
            : null;
    }

    private string? GetPlatformIdAt(ModernVoxelKey key)
    {
        return platformVoxels.TryGetValue(key, out string? platformId)
            ? platformId
            : null;
    }

    private bool IsTrainOccupying(ModernVoxelKey key)
    {
        return trains.Values
            .SelectMany(train => train.Cars)
            .Any(car => car.Location == key);
    }

    private bool IsTrainOccupying(ModernVoxelKey key, string exceptTrainId)
    {
        return trains.Values
            .Where(train => !string.Equals(train.TrainId, exceptTrainId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(train => train.Cars)
            .Any(car => car.Location == key);
    }

    private bool IsTrainOccupyingRailTile(int h, int v)
    {
        return trains.Values.Any(train =>
            train.Cars.Any(car => car.Location.H == h && car.Location.V == v)
            || train.GarageLocation is { } garageLocation && garageLocation.H == h && garageLocation.V == v);
    }

    private bool IsGarageRail(int h, int v)
    {
        return Transport.SpecialRailTiles.GetValueOrDefault((h, v), ModernSpecialRailKind.Normal) == ModernSpecialRailKind.Garage;
    }

    private void ApplyStationHourlyDecay(long hours)
    {
        int count = (int)Math.Min(hours, 24 * 31);
        if (count <= 0 || stations.Count == 0)
        {
            return;
        }

        foreach (ModernStation station in stations.Values.ToArray())
        {
            ModernStationStats stats = station.Stats;
            for (int i = 0; i < count; i++)
            {
                stats = stats.HourlyDecay();
            }

            stations[station.StationId] = station with { Stats = stats };
        }
    }

    private void ApplyStationDailyReset(long days)
    {
        long stationCosts = stations.Values.Sum(station => Math.Max(0, station.OperationCost));
        long platformCosts = platforms.Values.Sum(platform => Math.Max(1, platform.Length) * 180_000L);
        long total = (stationCosts + platformCosts) * Math.Max(1, days);
        if (total > 0)
        {
            Spend(total, ModernAccountGenre.Railway, "Daily rail service costs.");
        }

        int count = (int)Math.Min(days, 365);
        for (int day = 0; day < count; day++)
        {
            int dayOfWeek = (Clock.DayOfWeek - count + day + 1) % 7;
            if (dayOfWeek < 0)
            {
                dayOfWeek += 7;
            }

            foreach (ModernStation station in stations.Values.ToArray())
            {
                stations[station.StationId] = station with { Stats = station.Stats.DailyReset(dayOfWeek) };
            }
        }
    }

    private bool TryStepOnRail(ModernVoxelKey location, ModernDirection direction, out ModernVoxelKey next)
    {
        ModernLocation current = ToLocation(location);
        ModernLocation nextLocation = current + direction;
        (int h, int v) = ToHv(nextLocation);
        next = new ModernVoxelKey(h, v, IsInside(h, v) ? GetRailLevel(h, v) : location.Z);
        if (!IsInside(h, v) || !Transport.HasRail(location.H, location.V) || !Transport.HasRail(h, v))
        {
            return false;
        }

        byte currentMask = GetRawRailMask(location.H, location.V);
        byte nextMask = GetRawRailMask(h, v);
        return (currentMask & (1 << direction.Index)) != 0
            && (nextMask & (1 << direction.Opposite.Index)) != 0;
    }

    private byte GetLegacyRailMask(int h, int v)
    {
        if (!IsInside(h, v) || !Transport.HasRail(h, v))
        {
            return 0;
        }

        return NormalizeRailMask(GetRawRailMask(h, v));
    }

    private byte GetRawRailMask(int h, int v)
    {
        if (Transport.RailDirectionMasks.TryGetValue((h, v), out byte explicitMask))
        {
            return explicitMask;
        }

        ModernLocation location = ToLocation(h, v, GetRailLevel(h, v));
        byte mask = 0;
        foreach (ModernDirection direction in ModernDirection.All)
        {
            ModernLocation neighbor = location + direction;
            (int neighborH, int neighborV) = ToHv(neighbor);
            if (IsInside(neighborH, neighborV) && Transport.HasRail(neighborH, neighborV))
            {
                mask |= (byte)(1 << direction.Index);
            }
        }

        return mask;
    }

    private static byte NormalizeRailMask(byte mask)
    {
        if (mask == 0)
        {
            mask = ModernRailPattern.DirectionMask(0, 4);
        }

        if (CountBits(mask) == 1)
        {
            int direction = FirstSetDirection(mask);
            mask |= (byte)(1 << ((direction + 4) % 8));
        }
        else if (CountBits(mask) > 3)
        {
            mask = KeepFirstDirections(mask, 3);
        }

        return mask;
    }

    private void RebuildTilesFromFineHeights()
    {
        for (int v = 0; v < Height; v++)
        {
            for (int h = 0; h < Width; h++)
            {
                (int topX, int topY) = GetCornerVertex(h, v, TerrainCorner.Top);
                (int rightX, int rightY) = GetCornerVertex(h, v, TerrainCorner.Right);
                (int bottomX, int bottomY) = GetCornerVertex(h, v, TerrainCorner.Bottom);
                (int leftX, int leftY) = GetCornerVertex(h, v, TerrainCorner.Left);
                int top = fineHeights[topX, topY];
                int right = fineHeights[rightX, rightY];
                int bottom = fineHeights[bottomX, bottomY];
                int left = fineHeights[leftX, leftY];
                int baseLevel = Math.Min(Math.Min(top, right), Math.Min(bottom, left)) / 4;

                mountainCornerHeights[h, v, 0] = top - baseLevel * 4;
                mountainCornerHeights[h, v, 1] = right - baseLevel * 4;
                mountainCornerHeights[h, v, 2] = bottom - baseLevel * 4;
                mountainCornerHeights[h, v, 3] = left - baseLevel * 4;
                if (mountainCornerHeights[h, v, 0] == 4
                    && mountainCornerHeights[h, v, 1] == 4
                    && mountainCornerHeights[h, v, 2] == 4
                    && mountainCornerHeights[h, v, 3] == 4)
                {
                    baseLevel++;
                    mountainCornerHeights[h, v, 0] = 0;
                    mountainCornerHeights[h, v, 1] = 0;
                    mountainCornerHeights[h, v, 2] = 0;
                    mountainCornerHeights[h, v, 3] = 0;
                }

                groundLevels[h, v] = Math.Clamp(baseLevel, 0, MaxHeightCutLevel);
            }
        }
    }

    private bool AdjustCorner(int h, int v, TerrainCorner corner, int delta)
    {
        if (!IsInside(h, v))
        {
            return false;
        }

        if (TilesSharingCorner(h, v, corner).Any(TileHasSurfaceOccupancy))
        {
            return false;
        }

        (int x, int y) = GetCornerVertex(h, v, corner);
        int next = fineHeights[x, y] + delta;
        if (next < 0 || next > MaxFineHeight || !CanSetFineHeight(x, y, next))
        {
            return false;
        }

        fineHeights[x, y] = next;
        RebuildTilesFromFineHeights();
        RebuildTrafficVoxels();
        Publish(ModernWorldChangeKind.Terrain, new ModernVoxelKey(h, v, GetGroundLevel(h, v)), "Terrain corner adjusted.");
        return true;
    }

    private void RebuildTrafficVoxels()
    {
        ClearTrafficVoxelOccupancy();
        trafficVoxels.Clear();
        HashSet<(int H, int V)> touched = new(Transport.RailTiles);
        foreach (KeyValuePair<(int H, int V), RoadContribution> road in Transport.RoadTiles)
        {
            touched.Add(road.Key);
        }

        foreach ((int h, int v) in touched)
        {
            SynchronizeTrafficVoxel(h, v);
        }
    }

    private void SynchronizeTrafficVoxelsAlong(TileLocation from, TileLocation to)
    {
        foreach (TileLocation location in EnumerateLine(from, to))
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
        }
    }

    private void SynchronizeTrafficVoxelsAlong(IEnumerable<TileLocation> route)
    {
        foreach (TileLocation location in route)
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
        }
    }

    private void SynchronizeTrafficVoxelsAround(int h, int v)
    {
        SynchronizeTrafficVoxel(h, v);
        foreach ((int dh, int dv) in EightWayNeighbors)
        {
            SynchronizeTrafficVoxel(h + dh, v + dv);
        }
    }

    private void SynchronizeTrafficVoxel(int h, int v)
    {
        if (!IsInside(h, v))
        {
            return;
        }

        ModernVoxelKey key = new(h, v, Transport.HasRail(h, v) ? GetRailLevel(h, v) : GetGroundLevel(h, v));
        byte railMask = GetLegacyRailMask(h, v);
        byte roadMask = Transport.GetRoadMask(h, v);
        string? roadId = Transport.RoadTiles
            .FirstOrDefault(entry => entry.Key == (h, v))
            .Value?
            .Id;

        if (railMask == 0 && roadMask == 0)
        {
            trafficVoxels.Remove(key);
            if (voxels[key]?.IsTraffic == true)
            {
                voxels.Remove(key);
            }
            return;
        }

        trafficCars.TryGetValue(key, out string? carId);
        ModernTrafficVoxel trafficVoxel = new(key, railMask, roadMask, roadId, carId, CreateTrafficAccessory(railMask, roadMask));
        trafficVoxels[key] = trafficVoxel;
        voxels[key] = new ModernVoxelOccupancy(key, ModernVoxelKind.Traffic, null, trafficVoxel);
        Publish(ModernWorldChangeKind.Voxel, key, "Traffic voxel synchronized.");
    }

    private static ModernTrafficAccessory? CreateTrafficAccessory(byte railMask, byte roadMask)
    {
        if (CountBits(railMask) != 2)
        {
            return null;
        }

        bool railNorthSouth = HasDirection(railMask, 0) && HasDirection(railMask, 4);
        bool railEastWest = HasDirection(railMask, 2) && HasDirection(railMask, 6);
        if (railNorthSouth && (roadMask & (ModernRoadPattern.East | ModernRoadPattern.West)) == (ModernRoadPattern.East | ModernRoadPattern.West))
        {
            return new ModernTrafficAccessory(ModernTrafficAccessoryKind.RailRoadCrossing, ModernRailRoadCrossingOrientation.RailNorthSouth);
        }

        if (railEastWest && (roadMask & (ModernRoadPattern.North | ModernRoadPattern.South)) == (ModernRoadPattern.North | ModernRoadPattern.South))
        {
            return new ModernTrafficAccessory(ModernTrafficAccessoryKind.RailRoadCrossing, ModernRailRoadCrossingOrientation.RailEastWest);
        }

        return null;
    }

    private static bool HasDirection(byte mask, int direction)
    {
        return (mask & (1 << direction)) != 0;
    }

    private static int CountBits(byte value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= (byte)(value - 1);
            count++;
        }

        return count;
    }

    private static int FirstSetDirection(byte mask)
    {
        for (int i = 0; i < 8; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                return i;
            }
        }

        return 0;
    }

    private static byte KeepFirstDirections(byte mask, int count)
    {
        byte kept = 0;
        for (int i = 0; i < 8 && count > 0; i++)
        {
            if ((mask & (1 << i)) == 0)
            {
                continue;
            }

            kept |= (byte)(1 << i);
            count--;
        }

        return kept;
    }

    private void ClearTrafficVoxelOccupancy()
    {
        foreach (ModernVoxelKey key in trafficVoxels.Keys.ToArray())
        {
            if (voxels[key]?.IsTraffic == true)
            {
                voxels.Remove(key);
            }
        }
    }

    private bool RemoveReusableEntityAt(ModernVoxelKey key)
    {
        ModernPlacedEntity? entity = GetEntityAt(key);
        return entity?.IsSilentlyReclaimable == true && RemoveEntity(entity.EntityId);
    }

    private bool CanPlaceCar(ModernCarKind kind, ModernVoxelKey location)
    {
        if (trafficCars.ContainsKey(location))
        {
            return false;
        }

        SynchronizeTrafficVoxel(location.H, location.V);
        if (!trafficVoxels.TryGetValue(location, out ModernTrafficVoxel? traffic))
        {
            return false;
        }

        return kind switch
        {
            ModernCarKind.TrainCar => traffic.HasRail,
            ModernCarKind.Bus => traffic.HasRoad,
            _ => false
        };
    }

    private void RemoveCarFromTraffic(ModernCar car)
    {
        if (car.State.Location is { } location && trafficCars.TryGetValue(location, out string? carId) && carId == car.CarId)
        {
            trafficCars.Remove(location);
        }
    }

    private void ReclaimTransportLine(TileLocation from, TileLocation to)
    {
        foreach (TileLocation location in EnumerateLine(from, to))
        {
            RemoveReusableEntityAt(ToVoxelKey(location));
        }
    }

    private void ReclaimTransportRoute(IEnumerable<TileLocation> route)
    {
        foreach (TileLocation location in route)
        {
            RemoveReusableEntityAt(ToVoxelKey(location));
        }
    }

    private bool CanSetFineHeight(int vertexX, int vertexY, int value)
    {
        for (int v = 0; v < Height; v++)
        {
            for (int h = 0; h < Width; h++)
            {
                if (!TileUsesVertex(h, v, vertexX, vertexY))
                {
                    continue;
                }

                int top = FineHeightForCorner(h, v, TerrainCorner.Top, vertexX, vertexY, value);
                int right = FineHeightForCorner(h, v, TerrainCorner.Right, vertexX, vertexY, value);
                int bottom = FineHeightForCorner(h, v, TerrainCorner.Bottom, vertexX, vertexY, value);
                int left = FineHeightForCorner(h, v, TerrainCorner.Left, vertexX, vertexY, value);
                int min = Math.Min(Math.Min(top, right), Math.Min(bottom, left));
                int max = Math.Max(Math.Max(top, right), Math.Max(bottom, left));
                if (max - min > 4)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private IEnumerable<(int H, int V)> TilesSharingCorner(int h, int v, TerrainCorner corner)
    {
        (int vertexX, int vertexY) = GetCornerVertex(h, v, corner);
        for (int tileV = Math.Max(0, v - 2); tileV <= Math.Min(Height - 1, v + 2); tileV++)
        {
            for (int tileH = Math.Max(0, h - 2); tileH <= Math.Min(Width - 1, h + 2); tileH++)
            {
                if (TileUsesVertex(tileH, tileV, vertexX, vertexY))
                {
                    yield return (tileH, tileV);
                }
            }
        }
    }

    private bool TileHasSurfaceOccupancy((int H, int V) tile)
    {
        int z = GetGroundLevel(tile.H, tile.V);
        return Transport.HasRail(tile.H, tile.V)
            || Transport.HasRoad(tile.H, tile.V)
            || entityVoxels.ContainsKey(new ModernVoxelKey(tile.H, tile.V, z))
            || stationVoxels.ContainsKey(new ModernVoxelKey(tile.H, tile.V, z))
            || platformVoxels.ContainsKey(new ModernVoxelKey(tile.H, tile.V, z));
    }

    private bool TileUsesVertex(int h, int v, int vertexX, int vertexY)
    {
        return GetCornerVertex(h, v, TerrainCorner.Top) == (vertexX, vertexY)
            || GetCornerVertex(h, v, TerrainCorner.Right) == (vertexX, vertexY)
            || GetCornerVertex(h, v, TerrainCorner.Bottom) == (vertexX, vertexY)
            || GetCornerVertex(h, v, TerrainCorner.Left) == (vertexX, vertexY);
    }

    private int FineHeightForCorner(int h, int v, TerrainCorner corner, int replacementX, int replacementY, int replacementValue)
    {
        (int x, int y) = GetCornerVertex(h, v, corner);
        return x == replacementX && y == replacementY ? replacementValue : fineHeights[x, y];
    }

    private static int[,] BuildFlatFineHeights(int width, int height)
    {
        return new int[width * 2 + 2, height + 2];
    }

    private static int[,] BuildRepresentableFineHeights(int width, int height, int waterLevel)
    {
        int fineWidth = width * 2 + 2;
        int fineHeight = height + 2;
        int[,] fineHeights = new int[fineWidth, fineHeight];
        double centerX = width;
        double centerY = height / 2.0;

        for (int y = 0; y < fineHeight; y++)
        {
            for (int x = 0; x < fineWidth; x++)
            {
                fineHeights[x, y] = SampleFineHeight(x, y, centerX, centerY, width, height, waterLevel);
            }
        }

        bool changed;
        do
        {
            changed = false;
            for (int v = 0; v < height; v++)
            {
                for (int h = 0; h < width; h++)
                {
                    int projectedX = 2 * h + (v & 1);
                    int top = fineHeights[projectedX + 1, v];
                    int right = fineHeights[projectedX + 2, v + 1];
                    int bottom = fineHeights[projectedX + 1, v + 2];
                    int left = fineHeights[projectedX, v + 1];
                    int maxAllowed = Math.Min(Math.Min(top, right), Math.Min(bottom, left)) + 4;

                    changed |= ClampFineHeight(fineHeights, projectedX + 1, v, maxAllowed);
                    changed |= ClampFineHeight(fineHeights, projectedX + 2, v + 1, maxAllowed);
                    changed |= ClampFineHeight(fineHeights, projectedX + 1, v + 2, maxAllowed);
                    changed |= ClampFineHeight(fineHeights, projectedX, v + 1, maxAllowed);
                }
            }
        }
        while (changed);

        return fineHeights;
    }

    private static bool ClampFineHeight(int[,] fineHeights, int x, int y, int maxAllowed)
    {
        if (fineHeights[x, y] <= maxAllowed)
        {
            return false;
        }

        fineHeights[x, y] = maxAllowed;
        return true;
    }

    private static int SampleFineHeight(double x, double y, double centerX, double centerY, int width, int height, int waterLevel)
    {
        double h = x / 2.0;
        double v = y;
        double distance = Math.Sqrt(Math.Pow((x - centerX) / width * 1.35, 2) + Math.Pow((y - centerY) / height * 2.2, 2));
        double ridge = Math.Sin(h * 0.22) * 0.28 + Math.Cos(v * 0.16) * 0.24;
        double island = Math.Max(0, 1.16 - distance) * 3.35;
        double level = waterLevel - 1 + island + ridge;
        return Math.Clamp((int)Math.Round(level * 4), 0, MaxFineHeight);
    }

    private static IEnumerable<TileLocation> EnumerateLine(TileLocation from, TileLocation to)
    {
        int h = from.H;
        int v = from.V;
        while (true)
        {
            yield return new TileLocation(h, v, from.Z);
            if (h == to.H && v == to.V)
            {
                yield break;
            }

            h += Math.Sign(to.H - h);
            v += Math.Sign(to.V - v);
        }
    }

    private static (int X, int Y) GetCornerVertex(int h, int v, TerrainCorner corner)
    {
        int projectedX = 2 * h + (v & 1);
        return corner switch
        {
            TerrainCorner.Top => (projectedX + 1, v),
            TerrainCorner.Right => (projectedX + 2, v + 1),
            TerrainCorner.Bottom => (projectedX + 1, v + 2),
            TerrainCorner.Left => (projectedX, v + 1),
            _ => (projectedX + 1, v)
        };
    }

    private static ModernVoxelKey ToVoxelKey(TileLocation location)
    {
        return new ModernVoxelKey(location.H, location.V, location.Z);
    }

    private static RoadContribution? SelectInitialRoad(IReadOnlyList<RoadContribution> roads)
    {
        return roads
            .Where(road => road.IsLoadable)
            .OrderByDescending(road => road.Kind == RoadContributionKind.Standard)
            .ThenByDescending(road => road.Style.MajorType.Equals("street", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(road => road.Style.Lanes)
            .ThenBy(road => road.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(road => road.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
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
