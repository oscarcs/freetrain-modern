namespace FreeTrain.Modern;

public readonly record struct ModernDistance(int X, int Y, int Z)
{
    public int Volume => X * Y * Z;

    public static ModernDistance operator /(ModernDistance distance, int divisor)
    {
        return new ModernDistance(distance.X / divisor, distance.Y / divisor, distance.Z / divisor);
    }
}

public static class ModernFootprint
{
    public static ModernVoxelKey ToVoxelKey(int h, int v, int z, int localX, int localY, int localZ = 0)
    {
        (int isoX, int isoY) = ToIso(h, v);
        return FromIso(isoX + localX, isoY + localY, z + localZ);
    }

    public static IEnumerable<ModernVoxelKey> EnumerateVoxels(
        int h,
        int v,
        int z,
        int footprintH,
        int footprintV,
        int footprintZ)
    {
        int sizeH = Math.Max(1, footprintH);
        int sizeV = Math.Max(1, footprintV);
        int sizeZ = Math.Max(1, footprintZ);
        for (int localZ = 0; localZ < sizeZ; localZ++)
        {
            for (int localV = 0; localV < sizeV; localV++)
            {
                for (int localH = 0; localH < sizeH; localH++)
                {
                    yield return ToVoxelKey(h, v, z, localH, localV, localZ);
                }
            }
        }
    }

    private static (int X, int Y) ToIso(int h, int v)
    {
        return (h - FloorDiv(v, 2), h + FloorDiv(v + 1, 2));
    }

    private static ModernVoxelKey FromIso(int isoX, int isoY, int z)
    {
        int v = isoY - isoX;
        int parity = PositiveModulo(v, 2);
        int h = (isoX + isoY - parity) / 2;
        return new ModernVoxelKey(h, v, z);
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        return remainder != 0 && (remainder < 0) != (divisor < 0)
            ? quotient - 1
            : quotient;
    }

    private static int PositiveModulo(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}

public readonly record struct ModernLocation(int X, int Y, int Z)
{
    public static ModernLocation Unplaced => new(int.MinValue, int.MinValue, int.MinValue);

    public ModernLocation Toward(ModernLocation to)
    {
        return this + GetDirectionTo(to);
    }

    public ModernDirection GetDirectionTo(ModernLocation to)
    {
        if (Z != to.Z || this == to)
        {
            throw new ArgumentException("Locations must be distinct and on the same Z level.", nameof(to));
        }

        return ModernDirection.FromOffset(Math.Sign(to.X - X), Math.Sign(to.Y - Y));
    }

    public int DistanceTo(ModernLocation location)
    {
        int dx = location.X - X;
        int dy = location.Y - Y;
        int dz = location.Z - Z;
        return (int)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public bool InBetween(ModernLocation first, ModernLocation second)
    {
        return InBetween(X, first.X, second.X)
            && InBetween(Y, first.Y, second.Y)
            && InBetween(Z, first.Z, second.Z);
    }

    public ModernLocation Align4To(ModernLocation anchor)
    {
        if (Z != anchor.Z)
        {
            throw new ArgumentException("Locations must be on the same Z level.", nameof(anchor));
        }

        int xDistance = Math.Abs(X - anchor.X);
        int yDistance = Math.Abs(Y - anchor.Y);
        return xDistance >= yDistance
            ? new ModernLocation(X, anchor.Y, Z)
            : new ModernLocation(anchor.X, Y, Z);
    }

    public ModernLocation Align8To(ModernLocation anchor)
    {
        if (Z != anchor.Z)
        {
            throw new ArgumentException("Locations must be on the same Z level.", nameof(anchor));
        }

        int xDistance = Math.Abs(X - anchor.X);
        int yDistance = Math.Abs(Y - anchor.Y);
        if (yDistance / 2 > xDistance)
        {
            return new ModernLocation(anchor.X, Y, Z);
        }

        if (xDistance / 2 > yDistance)
        {
            return new ModernLocation(X, anchor.Y, Z);
        }

        return new ModernLocation(X, anchor.Y + xDistance * Math.Sign(Y - anchor.Y), Z);
    }

    public static ModernLocation Min(ModernLocation first, ModernLocation second)
    {
        if (first.X <= second.X && first.Y <= second.Y && first.Z <= second.Z)
        {
            return first;
        }

        if (second.X <= first.X && second.Y <= first.Y && second.Z <= first.Z)
        {
            return second;
        }

        throw new ArgumentException("Locations are not comparable component-wise.", nameof(second));
    }

    public static ModernLocation Max(ModernLocation first, ModernLocation second)
    {
        if (first.X <= second.X && first.Y <= second.Y && first.Z <= second.Z)
        {
            return second;
        }

        if (second.X <= first.X && second.Y <= first.Y && second.Z <= first.Z)
        {
            return first;
        }

        throw new ArgumentException("Locations are not comparable component-wise.", nameof(second));
    }

    public static ModernLocation operator +(ModernLocation location, ModernDirection direction)
    {
        return new ModernLocation(location.X + direction.OffsetX, location.Y + direction.OffsetY, location.Z);
    }

    public static ModernLocation operator -(ModernLocation location, ModernDirection direction)
    {
        return new ModernLocation(location.X - direction.OffsetX, location.Y - direction.OffsetY, location.Z);
    }

    public static ModernLocation operator +(ModernLocation location, ModernDistance distance)
    {
        return new ModernLocation(location.X + distance.X, location.Y + distance.Y, location.Z + distance.Z);
    }

    public static ModernLocation operator -(ModernLocation location, ModernDistance distance)
    {
        return new ModernLocation(location.X - distance.X, location.Y - distance.Y, location.Z - distance.Z);
    }

    public static ModernDistance operator -(ModernLocation first, ModernLocation second)
    {
        return new ModernDistance(first.X - second.X, first.Y - second.Y, first.Z - second.Z);
    }

    private static bool InBetween(int value, int first, int second)
    {
        return first <= value && value <= second
            || second <= value && value <= first;
    }
}

public sealed class ModernDirection
{
    private ModernDirection(int offsetX, int offsetY, string englishName, string japaneseName, int index)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        EnglishName = englishName;
        JapaneseName = japaneseName;
        Index = index;
    }

    public int OffsetX { get; }
    public int OffsetY { get; }
    public string EnglishName { get; }
    public string JapaneseName { get; }
    public int Index { get; }
    public bool IsSharp => Index % 2 == 0;
    public bool IsParallelToX => Index % 4 == 2;
    public bool IsParallelToY => Index % 4 == 0;
    public ModernDirection Left => Directions[(Index + 7) % 8];
    public ModernDirection Left90 => Directions[(Index + 6) % 8];
    public ModernDirection Right => Directions[(Index + 1) % 8];
    public ModernDirection Right90 => Directions[(Index + 2) % 8];
    public ModernDirection Opposite => Directions[(Index + 4) % 8];

    public static ModernDirection North => Directions[0];
    public static ModernDirection NorthEast => Directions[1];
    public static ModernDirection East => Directions[2];
    public static ModernDirection SouthEast => Directions[3];
    public static ModernDirection South => Directions[4];
    public static ModernDirection SouthWest => Directions[5];
    public static ModernDirection West => Directions[6];
    public static ModernDirection NorthWest => Directions[7];

    public static IReadOnlyList<ModernDirection> All => Directions;

    public static ModernDirection FromIndex(int index)
    {
        return Directions[index];
    }

    public static ModernDirection FromOffset(int offsetX, int offsetY)
    {
        foreach (ModernDirection direction in Directions)
        {
            if (direction.OffsetX == offsetX && direction.OffsetY == offsetY)
            {
                return direction;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(offsetX), "Direction offsets must identify one of the eight neighboring cells.");
    }

    public static int Angle(ModernDirection first, ModernDirection second)
    {
        int difference = Math.Abs(first.Index - second.Index);
        return difference > 4 ? 8 - difference : difference;
    }

    public override string ToString()
    {
        return EnglishName;
    }

    private static readonly ModernDirection[] Directions =
    {
        new(0, -1, "North", "北", 0),
        new(1, -1, "North-east", "北東", 1),
        new(1, 0, "East", "東", 2),
        new(1, 1, "South-east", "南東", 3),
        new(0, 1, "South", "南", 4),
        new(-1, 1, "South-west", "南西", 5),
        new(-1, 0, "West", "西", 6),
        new(-1, -1, "North-west", "北西", 7)
    };
}

public readonly record struct ModernCube(ModernLocation Corner, int SizeX, int SizeY, int SizeZ)
{
    public int X1 => Corner.X;
    public int Y1 => Corner.Y;
    public int Z1 => Corner.Z;
    public int X2 => Corner.X + SizeX;
    public int Y2 => Corner.Y + SizeY;
    public int Z2 => Corner.Z + SizeZ;
    public ModernDistance Size => new(SizeX, SizeY, SizeZ);
    public int Volume => SizeX * SizeY * SizeZ;

    public static ModernCube CreateExclusive(ModernLocation first, ModernLocation second)
    {
        int x = first.X <= second.X ? first.X : second.X + 1;
        int y = first.Y <= second.Y ? first.Y : second.Y + 1;
        int z = first.Z <= second.Z ? first.Z : second.Z + 1;
        return new ModernCube(
            new ModernLocation(x, y, z),
            Math.Abs(second.X - first.X),
            Math.Abs(second.Y - first.Y),
            Math.Abs(second.Z - first.Z));
    }

    public static ModernCube CreateExclusive(ModernLocation location, ModernDistance distance)
    {
        return CreateExclusive(location, location + distance);
    }

    public static ModernCube CreateInclusive(ModernLocation first, ModernLocation second)
    {
        if (first == ModernLocation.Unplaced || second == ModernLocation.Unplaced)
        {
            throw new ArgumentException("Cube endpoints must be placed locations.", nameof(second));
        }

        return new ModernCube(
            new ModernLocation(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Min(first.Z, second.Z)),
            Math.Abs(second.X - first.X) + 1,
            Math.Abs(second.Y - first.Y) + 1,
            Math.Abs(second.Z - first.Z) + 1);
    }

    public bool Contains(ModernLocation location)
    {
        return X1 <= location.X && location.X < X2
            && Y1 <= location.Y && location.Y < Y2
            && Z1 <= location.Z && location.Z < Z2;
    }

    public IEnumerable<ModernLocation> Locations
    {
        get
        {
            for (int z = Z1; z < Z2; z++)
            {
                for (int y = Y1; y < Y2; y++)
                {
                    for (int x = X1; x < X2; x++)
                    {
                        yield return new ModernLocation(x, y, z);
                    }
                }
            }
        }
    }
}
