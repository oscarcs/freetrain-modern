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

    private readonly int[,] groundLevels;
    private readonly int[,,] mountainCornerHeights;
    private readonly int[,] fineHeights;
    private readonly ModernSparseVoxelArray<ModernVoxelOccupancy> voxels;
    private readonly Dictionary<ModernVoxelKey, ModernTrafficVoxel> trafficVoxels = new();
    private readonly Dictionary<string, ModernCar> cars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ModernVoxelKey, string> trafficCars = new();
    private readonly Dictionary<string, ModernPlacedEntity> entities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ModernVoxelKey, string> entityVoxels = new();

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
    public IReadOnlyCollection<ModernPlacedEntity> Entities => entities.Values;
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

    public bool CanPlaceEntity(ModernPlacedEntity entity)
    {
        foreach (ModernVoxelKey voxel in entity.OccupiedVoxels)
        {
            if (!IsInside(voxel)
                || !IsReusable(voxel)
                || Transport.HasRail(voxel.H, voxel.V)
                || Transport.HasRoad(voxel.H, voxel.V))
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

    public bool AddEntity(ModernPlacedEntity entity)
    {
        if (entities.ContainsKey(entity.EntityId) || !CanPlaceEntity(entity))
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

        ModernDayNight previousDayNight = Clock.DayOrNight;
        ModernSeason previousSeason = Clock.Season;
        Clock = Clock.AdvanceMinutes(minutes);
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
        if (!IsTransportBuildableSurface(location))
        {
            return false;
        }

        RemoveReusableEntityAt(ToVoxelKey(location));
        bool changed = Transport.AddRailTile(location.H, location.V);
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
        if (!CanBuildRailLine(from, to))
        {
            return 0;
        }

        ReclaimTransportLine(from, to);
        int changed = Transport.AddRailLine((from.H, from.V), (to.H, to.V));
        if (changed > 0)
        {
            SynchronizeTrafficVoxelsAlong(from, to);
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(to), $"Rail line changed {changed} tile(s).");
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
        bool changed = Transport.RemoveAt(location.H, location.V);
        if (changed)
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(location), "Transport removed.");
        }

        return changed;
    }

    public bool CanBuildRailLine(TileLocation from, TileLocation to)
    {
        int deltaH = to.H - from.H;
        int deltaV = to.V - from.V;
        if (deltaH == 0 && deltaV == 0)
        {
            return IsBuildableSurface(to);
        }

        if (deltaH != 0 && deltaV != 0 && Math.Abs(deltaH) != Math.Abs(deltaV))
        {
            return false;
        }

        return EnumerateLine(from, to).All(IsTransportBuildableSurface);
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
        return Transport.CreateRailObjects();
    }

    public IReadOnlyList<ModernRailRoad> CreateRailRoads()
    {
        return Transport.CreateRailRoads(GetGroundLevel);
    }

    public IReadOnlyList<MapRoadObject> CreateRoadObjects()
    {
        return Transport.CreateRoadObjects();
    }

    public IReadOnlyList<ModernRoadSegment> CreateRoadSegments()
    {
        return Transport.CreateRoadSegments(GetGroundLevel);
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
            .Select(tile => new ModernRailSnapshot(tile.H, tile.V))
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
            entitySnapshots);
    }

    public static ModernWorld FromSnapshot(
        ModernWorldSnapshot snapshot,
        IReadOnlyDictionary<string, RoadContribution> roadContributions,
        IReadOnlyDictionary<string, LandContribution> landContributions,
        IReadOnlyDictionary<string, SpriteContribution> spriteContributions)
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

        foreach (ModernRailSnapshot rail in snapshot.Rails ?? Array.Empty<ModernRailSnapshot>())
        {
            world.Transport.AddRailTile(rail.H, rail.V);
        }

        foreach (ModernRoadSnapshot road in snapshot.Roads ?? Array.Empty<ModernRoadSnapshot>())
        {
            if (roadContributions.TryGetValue(road.RoadContributionId, out RoadContribution? contribution))
            {
                world.Transport.AddRoadTile(road.H, road.V, contribution);
            }
        }

        foreach (ModernEntitySnapshot entity in snapshot.Entities ?? Array.Empty<ModernEntitySnapshot>())
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

        foreach (ModernCarSnapshot carSnapshot in snapshot.Cars ?? Array.Empty<ModernCarSnapshot>())
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

        world.RebuildTrafficVoxels();
        world.Publish(ModernWorldChangeKind.Reset, null, "World loaded from snapshot.");
        return world;
    }

    public static ModernWorld CreateSample(IReadOnlyList<RoadContribution> roads, int width = 34, int height = 34, int waterLevel = 2)
    {
        ModernWorld world = new(
            "Sample modern world",
            width,
            height,
            waterLevel,
            BuildRepresentableFineHeights(width, height, waterLevel),
            ModernWorldClock.Default,
            ModernAccountState.Default);

        world.Transport.AddRailLine((5, 10), (17, 10));
        world.Transport.AddRailLine((17, 10), (25, 18));
        world.Transport.AddRailLine((25, 18), (17, 26));
        world.Transport.AddRailLine((17, 26), (5, 26));
        world.Transport.AddRailLine((5, 26), (5, 10));

        RoadContribution? road = SelectInitialRoad(roads);
        if (road is not null)
        {
            world.Transport.AddRoadLine((8, 15), (22, 15), road);
            world.Transport.AddRoadLine((15, 9), (15, 23), road);
            world.Transport.AddRoadLine((22, 15), (22, 22), road);
            world.Transport.AddRoadLine((22, 22), (27, 22), road);
        }

        world.RebuildTrafficVoxels();
        return world;
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

        ModernVoxelKey key = new(h, v, GetGroundLevel(h, v));
        byte railMask = Transport.GetRailMask(h, v);
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
            || entityVoxels.ContainsKey(new ModernVoxelKey(tile.H, tile.V, z));
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
