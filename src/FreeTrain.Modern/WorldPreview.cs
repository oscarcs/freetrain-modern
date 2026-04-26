namespace FreeTrain.Modern;

public readonly record struct TileLocation(int H, int V, int Z, TerrainCorner Corner = TerrainCorner.Top);

public enum TerrainCorner
{
    Top,
    Right,
    Bottom,
    Left
}

public readonly record struct TerrainTilePreview(int BaseLevel, int SurfaceLevel, int Top, int Right, int Bottom, int Left)
{
    public bool IsFlat => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;
    public int MaxCornerHeight => Math.Max(Math.Max(Top, Right), Math.Max(Bottom, Left));
    public bool HasMountain => !IsFlat;
}

public sealed class WorldPreview
{
    private const int MaxFineHeight = 7 * 4;

    private readonly int[,] groundLevels;
    private readonly int[,,] mountainCornerHeights;
    private readonly int[,] fineHeights;

    private WorldPreview(int width, int height, int waterLevel, int[,] fineHeights)
    {
        Width = width;
        Height = height;
        WaterLevel = waterLevel;
        this.fineHeights = fineHeights;
        groundLevels = new int[width, height];
        mountainCornerHeights = new int[width, height, 4];
        RebuildTilesFromFineHeights();
    }

    public int Width { get; }
    public int Height { get; }
    public int WaterLevel { get; }
    public int MaxHeightCutLevel => MaxFineHeight / 4;

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
        if (h < 0 || h >= Width || v < 0 || v >= Height)
        {
            return 0;
        }

        return GetTerrainTile(h, v).SurfaceLevel;
    }

    public TerrainTilePreview GetTerrainTile(int h, int v)
    {
        if (h < 0 || h >= Width || v < 0 || v >= Height)
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

    public bool RaiseCorner(int h, int v, TerrainCorner corner)
    {
        return AdjustCorner(h, v, corner, 1);
    }

    public bool LowerCorner(int h, int v, TerrainCorner corner)
    {
        return AdjustCorner(h, v, corner, -1);
    }

    public static WorldPreview CreateSample(int width = 34, int height = 34, int waterLevel = 2)
    {
        int[,] fineHeights = BuildRepresentableFineHeights(width, height, waterLevel);

        return new WorldPreview(width, height, waterLevel, fineHeights);
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

    private bool AdjustCorner(int h, int v, TerrainCorner corner, int delta)
    {
        if (h < 0 || h >= Width || v < 0 || v >= Height)
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
        return true;
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

    private static (int X, int Y) GetCornerVertex(int h, int v, TerrainCorner corner)
    {
        int projectedX = 2 * h + (v & 1);
        return corner switch
        {
            TerrainCorner.Top => (projectedX + 1, v),
            TerrainCorner.Right => (projectedX + 2, v + 1),
            TerrainCorner.Bottom => (projectedX + 1, v + 2),
            TerrainCorner.Left => (projectedX, v + 1),
            _ => throw new ArgumentOutOfRangeException(nameof(corner), corner, null)
        };
    }
}
