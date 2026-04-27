namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
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

}
