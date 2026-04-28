namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
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

    public int GetRailLevel(int h, int v)
    {
        return Transport.GetRailLevel(h, v, GetGroundLevel);
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

    public bool IsBuildableSurface(TileLocation location)
    {
        TerrainTilePreview terrain = GetTerrainTile(location.H, location.V);
        return terrain.IsFlat
            && terrain.SurfaceLevel == location.Z
            && IsDrySurfaceLevel(terrain.SurfaceLevel);
    }

    public bool IsTransportBuildableSurface(TileLocation location)
    {
        return IsBuildableSurface(location) && IsReusable(ToVoxelKey(location));
    }

    public bool RaiseCorner(int h, int v, TerrainCorner corner)
    {
        return AdjustCorner(h, v, corner, 1);
    }

    public bool LowerCorner(int h, int v, TerrainCorner corner)
    {
        return AdjustCorner(h, v, corner, -1);
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

    private bool IsDrySurfaceLevel(int surfaceLevel)
    {
        return WaterLevel <= 0 || surfaceLevel > WaterLevel;
    }

    private bool IsWaterSurfaceLevel(int surfaceLevel)
    {
        return WaterLevel > 0 && surfaceLevel <= WaterLevel;
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
        return Transport.HasRail(tile.H, tile.V, z)
            || Transport.HasRoad(tile.H, tile.V)
            || entityVoxels.ContainsKey(new ModernVoxelKey(tile.H, tile.V, z))
            || stationVoxels.ContainsKey(new ModernVoxelKey(tile.H, tile.V, z))
            || platformVoxels.ContainsKey(new ModernVoxelKey(tile.H, tile.V, z));
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

    private static int[,] BuildFlatFineHeights(int width, int height, int level = 0)
    {
        int fineWidth = width * 2 + 2;
        int fineHeight = height + 2;
        int[,] fineHeights = new int[fineWidth, fineHeight];
        int fineLevel = Math.Clamp(level * 4, 0, MaxFineHeight);
        for (int y = 0; y < fineHeight; y++)
        {
            for (int x = 0; x < fineWidth; x++)
            {
                fineHeights[x, y] = fineLevel;
            }
        }

        return fineHeights;
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
        double nx = width <= 0 ? 0 : h / width;
        double ny = height <= 0 ? 0 : v / height;
        double continent = FractalValueNoise(nx * 1.55 + 31.7, ny * 1.55 - 12.4, 4, 0.55);
        double hills = FractalValueNoise(nx * 4.8 - 10.3, ny * 4.8 + 44.1, 5, 0.5);
        double ridges = 1.0 - Math.Abs(FractalValueNoise(nx * 8.0 + 8.7, ny * 8.0 - 5.6, 3, 0.55));
        double basins = FractalValueNoise(nx * 2.8 - 71.0, ny * 2.8 + 18.5, 3, 0.5);
        double level = waterLevel + 1.15
            + continent * 2.35
            + hills * 1.15
            + ridges * 0.55
            + basins * 0.65;
        double terraced = TerraceLevel(Math.Clamp(level, 0, 7));
        return Math.Clamp((int)Math.Round(terraced * 4), 0, MaxFineHeight);
    }

    private static double TerraceLevel(double level)
    {
        double lower = Math.Floor(level);
        double fraction = level - lower;
        const double flatBand = 0.36;
        if (fraction <= flatBand)
        {
            return lower;
        }

        if (fraction >= 1 - flatBand)
        {
            return lower + 1;
        }

        double t = (fraction - flatBand) / (1 - flatBand * 2);
        return lower + SmoothStep(t);
    }

    private static double SmoothStep(double value)
    {
        double t = Math.Clamp(value, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double FractalValueNoise(double x, double y, int octaves, double persistence)
    {
        double sum = 0;
        double amplitude = 1;
        double frequency = 1;
        double amplitudeSum = 0;
        for (int octave = 0; octave < octaves; octave++)
        {
            sum += ValueNoise(x * frequency, y * frequency, octave * 1013 + 97) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }

        return amplitudeSum <= 0 ? 0 : sum / amplitudeSum;
    }

    private static double ValueNoise(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        double tx = SmoothStep(x - x0);
        double ty = SmoothStep(y - y0);
        double a = RandomUnit(x0, y0, seed);
        double b = RandomUnit(x0 + 1, y0, seed);
        double c = RandomUnit(x0, y0 + 1, seed);
        double d = RandomUnit(x0 + 1, y0 + 1, seed);
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    private static double RandomUnit(int x, int y, int seed)
    {
        unchecked
        {
            uint hash = (uint)seed;
            hash ^= (uint)x * 0x9E3779B9u;
            hash = (hash << 13) | (hash >> 19);
            hash ^= (uint)y * 0x85EBCA6Bu;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return hash / (double)uint.MaxValue * 2.0 - 1.0;
        }
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
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

}
