namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
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
                || (!allowTransportOverlap && Transport.HasRail(voxel.H, voxel.V, voxel.Z))
                || (!allowTransportOverlap && Transport.HasRoad(voxel.H, voxel.V)))
            {
                return false;
            }

            TerrainTilePreview terrain = GetTerrainTile(voxel.H, voxel.V);
            if (!terrain.IsFlat || terrain.SurfaceLevel != entity.Z || !IsDrySurfaceLevel(terrain.SurfaceLevel))
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
                || Transport.HasRail(voxel.H, voxel.V, voxel.Z)
                || Transport.HasRoad(voxel.H, voxel.V)
                || stationVoxels.ContainsKey(voxel)
                || platformVoxels.ContainsKey(voxel)
                || !terrain.IsFlat
                || terrain.SurfaceLevel != station.Z
                || !IsDrySurfaceLevel(terrain.SurfaceLevel))
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
                || !Transport.HasRail(voxel.H, voxel.V, voxel.Z)
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

    public ModernStation? GetStationAt(TileLocation location)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        return stationVoxels.TryGetValue(key, out string? stationId)
            && stations.TryGetValue(stationId, out ModernStation? station)
                ? station
                : null;
    }

    public ModernPlatform? GetPlatformAt(TileLocation location)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        return platformVoxels.TryGetValue(key, out string? platformId)
            && platforms.TryGetValue(platformId, out ModernPlatform? platform)
                ? platform
                : null;
    }

    public ModernTrain? GetTrainAt(TileLocation location)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        return trains.Values.FirstOrDefault(train =>
            train.Cars.Any(car => car.Location == key)
            || train.GarageLocation == key);
    }

    public ModernRailwayTileInspection InspectRailwayAt(TileLocation location)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        ModernRailRoad? railRoad = Transport.HasRail(location.H, location.V, location.Z)
            ? CreateRailRoad(location.H, location.V, location.Z)
            : null;
        ModernSpecialRailKind specialKind = Transport.GetSpecialRailKind(location.H, location.V, location.Z);
        int railLevel = Transport.HasRail(location.H, location.V, location.Z) ? location.Z : GetRailLevel(location.H, location.V);

        return new ModernRailwayTileInspection(
            key,
            GetStationAt(location),
            GetPlatformAt(location),
            GetTrainAt(location),
            railRoad,
            specialKind,
            railLevel);
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

    public IReadOnlyList<ModernVoxelKey> GetPlatformVoxels(ModernPlatform platform)
    {
        return EnumeratePlatformVoxels(platform).ToArray();
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
        ModernRailRoad? railRoad = CreateRailRoad(h, v, GetGroundLevel(h, v));
        return railRoad is not null
            && railRoad.Kind == ModernRailRoadKind.Single
            && railRoad.HasRail(direction)
            && railRoad.HasRail(direction.Opposite);
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

}
