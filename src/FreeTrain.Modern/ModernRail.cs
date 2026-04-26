using Avalonia;

namespace FreeTrain.Modern;

public sealed record MapRailObject(int H, int V, RailPatternDefinition Pattern);

public readonly record struct RailPatternDefinition(int SourceX, int SourceY, int SourceWidth, int SourceHeight, int OffsetY, byte DirectionMask)
{
    public Rect SourceRect => new(SourceX, SourceY, SourceWidth, SourceHeight);
}

public static class ModernRailPattern
{
    private static readonly RailPatternDefinition[] SinglePatterns =
    {
        CreateNormal(0, 1, 4),
        CreateNormal(1, 1, 5),
        CreateNormal(2, 1, 6),
        CreateNormal(3, 2, 5),
        CreateNormal(4, 2, 6),
        CreateNormal(5, 2, 7),
        CreateNormal(6, 3, 6),
        CreateNormal(7, 3, 7),
        CreateNormal(8, 3, 0),
        CreateNormal(9, 4, 7),
        CreateNormal(10, 4, 0),
        CreateNormal(11, 5, 0)
    };

    private static readonly RailPatternDefinition[] JunctionPatterns =
    {
        CreateJunction(0, 1, 5, -1),
        CreateJunction(1, 1, 5, +1),
        CreateJunction(2, 2, 6, -1),
        CreateJunction(3, 2, 6, +1),
        CreateJunction(4, 3, 7, -1),
        CreateJunction(5, 3, 7, +1),
        CreateJunction(6, 4, 0, -1),
        CreateJunction(7, 4, 0, +1),
        CreateJunction(8, 5, 1, -1),
        CreateJunction(9, 5, 1, +1),
        CreateJunction(10, 6, 2, -1),
        CreateJunction(11, 6, 2, +1),
        CreateJunction(12, 7, 3, -1),
        CreateJunction(13, 7, 3, +1),
        CreateJunction(14, 0, 4, -1),
        CreateJunction(15, 0, 4, +1)
    };

    public static RailPatternDefinition? FromDirectionMask(byte directionMask)
    {
        int rails = CountBits(directionMask);
        return rails switch
        {
            2 => SinglePatterns.FirstOrDefault(pattern => pattern.DirectionMask == directionMask),
            3 => JunctionPatterns.FirstOrDefault(pattern => pattern.DirectionMask == directionMask),
            _ => null
        };
    }

    public static byte DirectionMask(params int[] directions)
    {
        byte mask = 0;
        foreach (int direction in directions)
        {
            mask |= (byte)(1 << direction);
        }

        return mask;
    }

    public static int DirectionFromDelta(int deltaH, int deltaV)
    {
        return (Math.Sign(deltaH), Math.Sign(deltaV)) switch
        {
            (0, -1) => 0,
            (1, -1) => 1,
            (1, 0) => 2,
            (1, 1) => 3,
            (0, 1) => 4,
            (-1, 1) => 5,
            (-1, 0) => 6,
            (-1, -1) => 7,
            _ => throw new ArgumentOutOfRangeException(nameof(deltaH), "Rail deltas must be adjacent 8-way steps.")
        };
    }

    private static RailPatternDefinition CreateNormal(int imageIndexX, int directionA, int directionB)
    {
        return new RailPatternDefinition(imageIndexX * 32, 0, 32, 16, 0, DirectionMask(directionA, directionB));
    }

    private static RailPatternDefinition CreateJunction(int imageIndexX, int directionA, int directionB, int directionCOffset)
    {
        int directionC = (directionB + directionCOffset + 8) % 8;
        return new RailPatternDefinition((12 + imageIndexX) * 32, 16, 32, 16, 0, DirectionMask(directionA, directionB, directionC));
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
