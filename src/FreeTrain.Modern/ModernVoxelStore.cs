namespace FreeTrain.Modern;

public sealed class ModernSparseVoxelArray<T>
    where T : class
{
    private const int BlockH = 8;
    private const int BlockV = 8;

    private readonly T?[]?[] blocks;
    private readonly int blockColumns;
    private readonly int blockRows;

    public ModernSparseVoxelArray(int width, int height, int depth)
    {
        if (width <= 0 || height <= 0 || depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Voxel dimensions must be positive.");
        }

        Width = width;
        Height = height;
        Depth = depth;
        blockColumns = (width + BlockH - 1) / BlockH;
        blockRows = (height + BlockV - 1) / BlockV;
        blocks = new T?[]?[blockColumns * blockRows * depth];
    }

    public int Width { get; }
    public int Height { get; }
    public int Depth { get; }

    public T? this[ModernVoxelKey key]
    {
        get => TryGet(key, out T? value) ? value : null;
        set
        {
            if (value is null)
            {
                Remove(key);
                return;
            }

            Set(key, value);
        }
    }

    public bool IsInside(ModernVoxelKey key)
    {
        return key.H >= 0 && key.H < Width
            && key.V >= 0 && key.V < Height
            && key.Z >= 0 && key.Z < Depth;
    }

    public bool TryGet(ModernVoxelKey key, out T? value)
    {
        if (!IsInside(key))
        {
            value = null;
            return false;
        }

        T?[]? block = blocks[GetBlockIndex(key)];
        value = block is null ? null : block[GetCellIndex(key)];
        return value is not null;
    }

    public void Set(ModernVoxelKey key, T value)
    {
        if (!IsInside(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), "Voxel key is outside the world.");
        }

        int blockIndex = GetBlockIndex(key);
        T?[]? block = blocks[blockIndex];
        if (block is null)
        {
            block = new T?[BlockH * BlockV];
            blocks[blockIndex] = block;
        }

        block[GetCellIndex(key)] = value;
    }

    public bool Remove(ModernVoxelKey key)
    {
        if (!IsInside(key))
        {
            return false;
        }

        T?[]? block = blocks[GetBlockIndex(key)];
        int cellIndex = GetCellIndex(key);
        if (block is null || block[cellIndex] is null)
        {
            return false;
        }

        block[cellIndex] = null;
        return true;
    }

    public void Clear()
    {
        Array.Clear(blocks);
    }

    public IEnumerable<KeyValuePair<ModernVoxelKey, T>> Entries
    {
        get
        {
            for (int z = 0; z < Depth; z++)
            {
                for (int blockV = 0; blockV < blockRows; blockV++)
                {
                    for (int blockH = 0; blockH < blockColumns; blockH++)
                    {
                        T?[]? block = blocks[GetBlockIndex(blockH, blockV, z)];
                        if (block is null)
                        {
                            continue;
                        }

                        for (int v = 0; v < BlockV; v++)
                        {
                            int worldV = blockV * BlockV + v;
                            if (worldV >= Height)
                            {
                                continue;
                            }

                            for (int h = 0; h < BlockH; h++)
                            {
                                int worldH = blockH * BlockH + h;
                                if (worldH >= Width || block[v * BlockH + h] is not { } value)
                                {
                                    continue;
                                }

                                yield return new KeyValuePair<ModernVoxelKey, T>(new ModernVoxelKey(worldH, worldV, z), value);
                            }
                        }
                    }
                }
            }
        }
    }

    private int GetBlockIndex(ModernVoxelKey key)
    {
        return GetBlockIndex(key.H / BlockH, key.V / BlockV, key.Z);
    }

    private int GetBlockIndex(int blockH, int blockV, int z)
    {
        return (z * blockRows + blockV) * blockColumns + blockH;
    }

    private static int GetCellIndex(ModernVoxelKey key)
    {
        return (key.V % BlockV) * BlockH + key.H % BlockH;
    }
}

public sealed record ModernVoxelOccupancy(
    ModernVoxelKey Location,
    ModernVoxelKind Kind,
    string? EntityId,
    ModernTrafficVoxel? Traffic)
{
    public bool IsTraffic => Kind == ModernVoxelKind.Traffic;
    public bool HasEntity => !string.IsNullOrWhiteSpace(EntityId);
}
