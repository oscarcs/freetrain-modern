namespace FreeTrain.Modern;

public sealed class ModernSpriteSet2D
{
    private readonly SpriteFrame?[,] frames;

    public ModernSpriteSet2D(int sizeX, int sizeY)
    {
        SizeX = sizeX;
        SizeY = sizeY;
        frames = new SpriteFrame?[sizeX, sizeY];
    }

    public int SizeX { get; }
    public int SizeY { get; }
    public bool IsLoadable => InVoxelDrawOrder().Any(voxel => voxel.Frame.IsLoadable);

    public SpriteFrame? this[int x, int y]
    {
        get => frames[x, y];
        set => frames[x, y] = value;
    }

    public IEnumerable<ModernSpriteVoxel2D> InVoxelDrawOrder()
    {
        for (int y = 0; y < SizeY; y++)
        {
            for (int x = 0; x < SizeX; x++)
            {
                if (frames[x, y] is { } frame)
                {
                    yield return new ModernSpriteVoxel2D(x, y, frame);
                }
            }
        }
    }
}

public sealed class ModernSpriteSet3D
{
    private readonly SpriteFrame?[,,] frames;

    public ModernSpriteSet3D(int sizeX, int sizeY, int sizeZ)
    {
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        frames = new SpriteFrame?[sizeX, sizeY, sizeZ];
    }

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public bool IsLoadable => InVoxelDrawOrder().Any(voxel => voxel.Frame.IsLoadable);

    public SpriteFrame? this[int x, int y, int z]
    {
        get => frames[x, y, z];
        set => frames[x, y, z] = value;
    }

    public IEnumerable<ModernSpriteVoxel3D> InVoxelDrawOrder()
    {
        for (int z = 0; z < SizeZ; z++)
        {
            for (int y = 0; y < SizeY; y++)
            {
                for (int x = 0; x < SizeX; x++)
                {
                    if (frames[x, y, z] is { } frame)
                    {
                        yield return new ModernSpriteVoxel3D(x, y, z, frame);
                    }
                }
            }
        }
    }
}

public readonly record struct ModernSpriteVoxel2D(int X, int Y, SpriteFrame Frame);
public readonly record struct ModernSpriteVoxel3D(int X, int Y, int Z, SpriteFrame Frame);
