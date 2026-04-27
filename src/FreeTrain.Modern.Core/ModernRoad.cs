namespace FreeTrain.Modern;

public enum RoadContributionKind
{
    Standard,
    A3,
    Unsupported
}

public sealed record RoadStyle(
    string MajorType,
    string Sidewalk,
    int Lanes);

public sealed record RoadContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string Description,
    RoadContributionKind Kind,
    RoadStyle Style,
    IReadOnlyDictionary<byte, SpriteFrame> FramesByMask,
    string? Error)
{
    public bool IsLoadable => Error is null && FramesByMask.Values.Any(frame => frame.IsLoadable);
    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : string.IsNullOrWhiteSpace(PluginTitle)
            ? PluginDirectoryName
            : PluginTitle;
}

public sealed record MapRoadObject(int H, int V, RoadContribution Contribution, byte DirectionMask)
{
    public SpriteFrame? Frame => Contribution.FramesByMask.TryGetValue(DirectionMask, out SpriteFrame? frame)
        ? frame
        : null;
}

public sealed record ModernRoadSegment(
    ModernVoxelKey Location,
    RoadContribution Contribution,
    byte DirectionMask)
{
    public bool HasRoad(ModernDirection direction)
    {
        byte bit = DirectionBit(direction);
        return bit != 0 && (DirectionMask & bit) != 0;
    }

    public bool IsStraight => DirectionMask == (ModernRoadPattern.North | ModernRoadPattern.South)
        || DirectionMask == (ModernRoadPattern.East | ModernRoadPattern.West);

    public bool IsIntersection => CountBits(DirectionMask) >= 3;

    private static byte DirectionBit(ModernDirection direction)
    {
        return direction.Index switch
        {
            0 => ModernRoadPattern.North,
            2 => ModernRoadPattern.East,
            4 => ModernRoadPattern.South,
            6 => ModernRoadPattern.West,
            _ => 0
        };
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
}

public static class ModernRoadPattern
{
    public const byte North = 1;
    public const byte East = 2;
    public const byte South = 4;
    public const byte West = 8;

    public static int DirectionFromDelta(int deltaH, int deltaV)
    {
        return (Math.Sign(deltaH), Math.Sign(deltaV)) switch
        {
            (0, -1) => 0,
            (1, 0) => 2,
            (0, 1) => 4,
            (-1, 0) => 6,
            _ => throw new ArgumentOutOfRangeException(nameof(deltaH), "Road deltas must be adjacent 4-way steps.")
        };
    }

    public static byte DirectionBitFromDelta(int deltaH, int deltaV)
    {
        return DirectionFromDelta(deltaH, deltaV) switch
        {
            0 => North,
            2 => East,
            4 => South,
            6 => West,
            _ => 0
        };
    }

    public static bool IsFourWayNeighbor(int deltaH, int deltaV)
    {
        return Math.Abs(deltaH) + Math.Abs(deltaV) == 1;
    }
}
