namespace FreeTrain.Modern;

public sealed class MapTransportState
{
    private readonly HashSet<(int H, int V)> railTiles = new();
    private readonly Dictionary<(int H, int V), RoadContribution> roadTiles = new();

    public bool HasRail(int h, int v) => railTiles.Contains((h, v));

    public bool HasRoad(int h, int v) => roadTiles.ContainsKey((h, v));

    public IReadOnlyCollection<(int H, int V)> RailTiles => railTiles;

    public IReadOnlyCollection<KeyValuePair<(int H, int V), RoadContribution>> RoadTiles => roadTiles;

    public bool AddRailTile(int h, int v)
    {
        return railTiles.Add((h, v));
    }

    public bool AddRoadTile(int h, int v, RoadContribution contribution)
    {
        if (roadTiles.TryGetValue((h, v), out RoadContribution? existing) && existing == contribution)
        {
            return false;
        }

        roadTiles[(h, v)] = contribution;
        return true;
    }

    public bool RemoveAt(int h, int v)
    {
        bool removedRail = railTiles.Remove((h, v));
        bool removedRoad = roadTiles.Remove((h, v));
        return removedRail || removedRoad;
    }

    public int AddRailLine((int H, int V) from, (int H, int V) to)
    {
        return AddLine(from, to, railTiles.Add);
    }

    public int AddRoadLine((int H, int V) from, (int H, int V) to, RoadContribution contribution)
    {
        return AddLine(from, to, point =>
        {
            bool changed = !roadTiles.TryGetValue(point, out RoadContribution? existing) || existing != contribution;
            roadTiles[point] = contribution;
            return changed;
        });
    }

    public IReadOnlyList<MapRailObject> CreateRailObjects()
    {
        return railTiles
            .Select(tile => CreateRailObject(tile.H, tile.V))
            .Where(rail => rail is not null)
            .Cast<MapRailObject>()
            .ToArray();
    }

    public IReadOnlyList<ModernRailRoad> CreateRailRoads(Func<int, int, int> getGroundLevel)
    {
        return railTiles
            .Select(tile => CreateRailRoad(tile.H, tile.V, getGroundLevel(tile.H, tile.V)))
            .Where(rail => rail is not null)
            .Cast<ModernRailRoad>()
            .ToArray();
    }

    public IReadOnlyList<MapRoadObject> CreateRoadObjects()
    {
        return roadTiles
            .Select(entry => new MapRoadObject(entry.Key.H, entry.Key.V, entry.Value, GetRoadRenderMask(entry.Key.H, entry.Key.V)))
            .Where(road => road.Frame?.IsLoadable == true)
            .ToArray();
    }

    public IReadOnlyList<ModernRoadSegment> CreateRoadSegments(Func<int, int, int> getGroundLevel)
    {
        return roadTiles
            .Select(entry => new ModernRoadSegment(
                new ModernVoxelKey(entry.Key.H, entry.Key.V, getGroundLevel(entry.Key.H, entry.Key.V)),
                entry.Value,
                GetRoadRenderMask(entry.Key.H, entry.Key.V)))
            .ToArray();
    }

    private MapRailObject? CreateRailObject(int h, int v)
    {
        byte mask = GetNormalizedRailMask(h, v);
        return ModernRailPattern.FromDirectionMask(mask) is { } pattern
            ? new MapRailObject(h, v, pattern)
            : null;
    }

    private ModernRailRoad? CreateRailRoad(int h, int v, int z)
    {
        byte mask = GetNormalizedRailMask(h, v);
        RailPatternDefinition? pattern = ModernRailPattern.FromDirectionMask(mask);
        ModernRailRoadKind kind = ModernRailPattern.KindFromDirectionMask(mask);
        return kind == ModernRailRoadKind.Unsupported
            ? null
            : new ModernRailRoad(new ModernVoxelKey(h, v, z), mask, kind, pattern);
    }

    private byte GetNormalizedRailMask(int h, int v)
    {
        byte mask = GetRailMask(h, v);
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

    public byte GetRailMask(int h, int v)
    {
        byte mask = 0;
        foreach ((int dh, int dv) in EightWayNeighbors)
        {
            if (!railTiles.Contains((h + dh, v + dv)))
            {
                continue;
            }

            int direction = ModernRailPattern.DirectionFromDelta(dh, dv);
            mask |= (byte)(1 << direction);
        }

        return mask;
    }

    public byte GetRoadMask(int h, int v)
    {
        byte mask = 0;
        foreach ((int dh, int dv, byte bit) in FourWayNeighbors)
        {
            if (roadTiles.ContainsKey((h + dh, v + dv)))
            {
                mask |= bit;
            }
        }

        return mask;
    }

    private byte GetRoadRenderMask(int h, int v)
    {
        byte mask = GetRoadMask(h, v);
        return mask == 0 ? ModernRoadPattern.South : mask;
    }

    private static int AddLine((int H, int V) from, (int H, int V) to, Func<(int H, int V), bool> add)
    {
        int changed = 0;
        int h = from.H;
        int v = from.V;
        if (add((h, v)))
        {
            changed++;
        }

        while (h != to.H || v != to.V)
        {
            h += Math.Sign(to.H - h);
            v += Math.Sign(to.V - v);
            if (add((h, v)))
            {
                changed++;
            }
        }

        return changed;
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

    private static readonly (int H, int V, byte Bit)[] FourWayNeighbors =
    {
        (0, -1, ModernRoadPattern.North),
        (1, 0, ModernRoadPattern.East),
        (0, 1, ModernRoadPattern.South),
        (-1, 0, ModernRoadPattern.West)
    };
}
