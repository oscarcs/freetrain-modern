using System.Runtime.InteropServices;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace FreeTrain.Modern;

public sealed class TerrainRenderer : IDisposable
{
    private const int LightX = 80;
    private const int LightY = -20;
    private const int LightZ = -80;
    private const int CliffTileWidth = 16;
    private const int CliffTileHeight = 24;
    private const int GroundTileWidth = 32;
    private const int GroundTileHeight = 16;

    private readonly Color[] mountainColors;
    private readonly Color[] waterColors;
    private readonly Bitmap flatLandBitmap;
    private readonly Bitmap waterSurfaceBitmap;
    private readonly Bitmap cliffBitmap;
    private readonly SKBitmap[] mountainMasks = CreateMountainMasks();
    private readonly Dictionary<MountainHalfKey, Bitmap> mountainHalfCache = new();
    private readonly Pen gridPen = new(new SolidColorBrush(Color.FromArgb(80, 48, 78, 92)), 1);

    private readonly record struct MountainHalfKey(
        int TopBottomDifference,
        int SourceIndex,
        bool FlipVertically,
        bool RightHalf,
        Color Fill,
        Color Outline);

    public TerrainRenderer(string palettePath, string cliffPath, string groundPath)
    {
        XDocument document = XDocument.Load(palettePath);
        XElement root = document.Root ?? throw new InvalidDataException("Missing mountain palette root.");
        mountainColors = LoadColors(root.Element("summer") ?? throw new InvalidDataException("Missing summer palette."));
        waterColors = LoadColors(root.Element("water") ?? throw new InvalidDataException("Missing water palette."));
        Color flatLandColor = mountainColors[Math.Clamp(5, 0, mountainColors.Length - 1)];
        Color flatOutlineColor = mountainColors[0];
        Color waterSurfaceColor = Color.FromRgb(0, 86, 145);
        flatLandBitmap = CreateRecoloredGroundTile(groundPath, flatLandColor, flatOutlineColor);
        waterSurfaceBitmap = CreateRecoloredGroundTile(groundPath, waterSurfaceColor, waterSurfaceColor);
        cliffBitmap = LegacyBitmap.LoadWithColorKey(cliffPath, includeTopLeftColor: false);
    }

    public Pen GridPen => gridPen;

    public void Dispose()
    {
        flatLandBitmap.Dispose();
        waterSurfaceBitmap.Dispose();
        cliffBitmap.Dispose();
        foreach (Bitmap bitmap in mountainHalfCache.Values)
        {
            bitmap.Dispose();
        }

        foreach (SKBitmap mask in mountainMasks)
        {
            mask.Dispose();
        }

        mountainHalfCache.Clear();
    }

    public void DrawTerrainTile(
        DrawingContext context,
        ModernWorld world,
        LegacySpriteSheet groundTiles,
        int h,
        int v,
        Point p,
        TerrainTilePreview terrain,
        bool showGrid)
    {
        if (terrain.IsFlat)
        {
            DrawFlatTerrainSurface(context, p);
            if (showGrid)
            {
                DrawDiamond(context, p, GridPen);
            }

            return;
        }

        bool fullyUnderwater = world.WaterLevel > 0
            && (terrain.BaseLevel * 4 + terrain.MaxCornerHeight) <= world.WaterLevel * 4;
        Color lit = SelectTerrainColor(terrain, fullyUnderwater);
        Color outline = fullyUnderwater ? Colors.Navy : mountainColors[0];
        DrawLegacyMountainSurface(context, p, terrain, lit, outline);
        DrawNeighborCliffs(context, world, h, v, p, terrain);
    }

    public void DrawWaterSurfaceTile(DrawingContext context, Point p)
    {
        context.DrawImage(waterSurfaceBitmap, new Rect(p, waterSurfaceBitmap.Size));
    }

    public void DrawDiamond(DrawingContext context, Point p, Pen pen)
    {
        Point top = new(p.X + 16, p.Y);
        Point right = new(p.X + 32, p.Y + 8);
        Point bottom = new(p.X + 16, p.Y + 16);
        Point left = new(p.X, p.Y + 8);
        FillOutline(context, pen, top, right, bottom, left);
    }

    private void DrawNeighborCliffs(DrawingContext context, ModernWorld world, int h, int v, Point p, TerrainTilePreview terrain)
    {
        (int westH, int westV) = OffsetLocation(h, v, -1, 0, world.Height);
        (int southH, int southV) = OffsetLocation(h, v, 0, 1, world.Height);
        TerrainTilePreview west = world.GetTerrainTile(westH, westV);
        TerrainTilePreview south = world.GetTerrainTile(southH, southV);

        DrawMountainCliffs(context, p, terrain, west, south);
    }

    private void DrawFlatTerrainSurface(DrawingContext context, Point p)
    {
        context.DrawImage(flatLandBitmap, new Rect(p, flatLandBitmap.Size));
    }

    private void DrawMountainCliffs(DrawingContext context, Point p, TerrainTilePreview terrain, TerrainTilePreview west, TerrainTilePreview south)
    {
        if (west.IsFlat && terrain.Left + terrain.Bottom > 0 && west.BaseLevel <= terrain.BaseLevel)
        {
            DrawCliffSprite(context, side: 0, leftHeight: terrain.Left, rightHeight: terrain.Bottom, new Point(p.X, p.Y));
        }

        if (south.IsFlat && terrain.Bottom + terrain.Right > 0 && south.BaseLevel <= terrain.BaseLevel)
        {
            DrawCliffSprite(context, side: 1, leftHeight: terrain.Bottom, rightHeight: terrain.Right, new Point(p.X + 16, p.Y));
        }
    }

    private void DrawCliffSprite(DrawingContext context, int side, int leftHeight, int rightHeight, Point basePoint)
    {
        double sourceX = side * CliffTileWidth + rightHeight * 32;
        double sourceY = leftHeight * CliffTileHeight;
        Rect source = new(sourceX, sourceY, CliffTileWidth, CliffTileHeight);
        Rect target = new(basePoint.X, basePoint.Y - 8, CliffTileWidth, CliffTileHeight);
        context.DrawImage(cliffBitmap, source, target);
    }

    private void DrawLegacyMountainSurface(DrawingContext context, Point p, TerrainTilePreview terrain, Color fill, Color outline)
    {
        int top = terrain.Top;
        int right = terrain.Right;
        int bottom = terrain.Bottom;
        int left = terrain.Left;
        int topBottomDifference = top - bottom + 4;
        int maxU = Math.Min(topBottomDifference + 2, 6);
        Point basePoint = new(p.X, p.Y - top * 4);

        int leftDifference = top - left + 2;
        bool flipLeft = false;
        if (leftDifference < topBottomDifference - leftDifference)
        {
            flipLeft = true;
            leftDifference = topBottomDifference - leftDifference;
        }

        DrawLegacyMountainHalf(
            context,
            new Point(basePoint.X, basePoint.Y - (flipLeft ? 8 : 0)),
            topBottomDifference,
            maxU - leftDifference,
            flipLeft,
            rightHalf: false,
            fill,
            outline);

        int rightDifference = top - right + 2;
        bool flipRight = false;
        if (rightDifference < topBottomDifference - rightDifference)
        {
            flipRight = true;
            rightDifference = topBottomDifference - rightDifference;
        }

        DrawLegacyMountainHalf(
            context,
            new Point(basePoint.X + 16, basePoint.Y - (flipRight ? 8 : 0)),
            topBottomDifference,
            maxU - rightDifference,
            flipRight,
            rightHalf: true,
            fill,
            outline);
    }

    private void DrawLegacyMountainHalf(
        DrawingContext context,
        Point destination,
        int topBottomDifference,
        int sourceIndex,
        bool flipVertically,
        bool rightHalf,
        Color fill,
        Color outline)
    {
        Bitmap bitmap = GetMountainHalfBitmap(topBottomDifference, sourceIndex, flipVertically, rightHalf, fill, outline);
        context.DrawImage(bitmap, new Rect(destination, bitmap.Size));
    }

    private Bitmap GetMountainHalfBitmap(int topBottomDifference, int sourceIndex, bool flipVertically, bool rightHalf, Color fill, Color outline)
    {
        MountainHalfKey key = new(topBottomDifference, sourceIndex, flipVertically, rightHalf, fill, outline);
        if (mountainHalfCache.TryGetValue(key, out Bitmap? cached))
        {
            return cached;
        }

        int sourceHeight = topBottomDifference * 4 + 9;
        int sourceX = sourceIndex * 32 + (rightHalf ? 16 : 0);
        WriteableBitmap bitmap = new(new PixelSize(16, sourceHeight), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        SKBitmap mask = mountainMasks[topBottomDifference];

        using (ILockedFramebuffer framebuffer = bitmap.Lock())
        {
            byte[] row = new byte[framebuffer.RowBytes];
            for (int y = 0; y < sourceHeight; y++)
            {
                Array.Clear(row);
                int maskY = flipVertically ? sourceHeight - y - 1 : y;
                for (int x = 0; x < 16; x++)
                {
                    SKColor pixel = mask.GetPixel(sourceX + x, maskY);
                    Color? target = null;
                    if (pixel.Red == 255 && pixel.Green == 255 && pixel.Blue == 255)
                    {
                        target = fill;
                    }
                    else if (pixel.Red == 0 && pixel.Green == 0 && pixel.Blue == 0)
                    {
                        target = outline;
                    }

                    if (target is { } color)
                    {
                        int index = x * 4;
                        row[index] = color.B;
                        row[index + 1] = color.G;
                        row[index + 2] = color.R;
                        row[index + 3] = color.A;
                    }
                }

                Marshal.Copy(row, 0, IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), framebuffer.RowBytes);
            }
        }

        mountainHalfCache[key] = bitmap;
        return bitmap;
    }

    private static SKBitmap[] CreateMountainMasks()
    {
        SKBitmap[] masks = new SKBitmap[9];
        using SKPaint fill = new()
        {
            Color = SKColors.White,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };
        using SKPaint outline = new()
        {
            Color = SKColors.Black,
            IsAntialias = false,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke
        };
        using SKPaint transparent = new()
        {
            Color = SKColors.Magenta,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        for (int topBottomDifference = 0; topBottomDifference <= 8; topBottomDifference++)
        {
            int u = Math.Min(topBottomDifference + 2, 6);
            int width = 32 * (5 - (Math.Abs(topBottomDifference - 4) + 1) / 2);
            int height = topBottomDifference * 4 + 9;
            SKBitmap bitmap = new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKCanvas canvas = new(bitmap);
            canvas.Clear(SKColors.Magenta);

            int index = 0;
            for (; u >= topBottomDifference - u; u--, index++)
            {
                int offset = index * 32;
                using SKPath path = new();
                path.MoveTo(offset + 16, 0);
                path.LineTo(offset + 32, u * 4);
                path.LineTo(offset + 16, topBottomDifference * 4);
                path.LineTo(offset + 15, topBottomDifference * 4);
                path.LineTo(offset, u * 4);
                path.LineTo(offset + 15, 0);
                path.Close();

                canvas.DrawRect(new SKRect(offset, 0, offset + 32, height), transparent);
                canvas.DrawPath(path, fill);
                canvas.DrawPath(path, outline);
            }

            masks[topBottomDifference] = bitmap;
        }

        return masks;
    }

    private static Bitmap CreateRecoloredGroundTile(string groundPath, Color fill, Color outline)
    {
        using SKBitmap source = SKBitmap.Decode(groundPath)
            ?? throw new InvalidDataException($"Could not load ground sprite sheet '{groundPath}'.");
        WriteableBitmap bitmap = new(new PixelSize(GroundTileWidth, GroundTileHeight), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);

        using (ILockedFramebuffer framebuffer = bitmap.Lock())
        {
            byte[] row = new byte[framebuffer.RowBytes];
            for (int y = 0; y < GroundTileHeight; y++)
            {
                Array.Clear(row);
                for (int x = 0; x < GroundTileWidth; x++)
                {
                    SKColor sourcePixel = source.GetPixel(x, y);
                    if (sourcePixel.Alpha == 0 || sourcePixel.Red == 255 && sourcePixel.Green == 0 && sourcePixel.Blue == 255)
                    {
                        continue;
                    }

                    double luminance = (sourcePixel.Red * 0.2126 + sourcePixel.Green * 0.7152 + sourcePixel.Blue * 0.0722) / 255.0;
                    double blend = SmoothStep((luminance - 0.42) / 0.42);
                    byte blue = BlendColor(outline.B, fill.B, blend);
                    byte green = BlendColor(outline.G, fill.G, blend);
                    byte red = BlendColor(outline.R, fill.R, blend);
                    double shade = 0.82 + luminance * 0.28;
                    int index = x * 4;
                    row[index] = ScaleColor(blue, shade);
                    row[index + 1] = ScaleColor(green, shade);
                    row[index + 2] = ScaleColor(red, shade);
                    row[index + 3] = sourcePixel.Alpha;
                }

                Marshal.Copy(row, 0, IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), framebuffer.RowBytes);
            }
        }

        return bitmap;
    }

    private static byte ScaleColor(byte value, double shade)
    {
        return (byte)Math.Clamp((int)Math.Round(value * shade), 0, 255);
    }

    private static byte BlendColor(byte from, byte to, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(from + (to - from) * Math.Clamp(amount, 0, 1)), 0, 255);
    }

    private static double SmoothStep(double value)
    {
        double t = Math.Clamp(value, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static (int H, int V) OffsetLocation(int h, int v, int dx, int dy, int worldHeight)
    {
        int originX = (worldHeight - 1) / 2;
        int x = h - v / 2 + originX;
        int y = h + (v + 1) / 2;
        int shiftedX = x + dx;
        int shiftedY = y + dy;
        int shiftedOriginX = shiftedX - originX;
        return ((shiftedOriginX + shiftedY) >> 1, shiftedY - shiftedOriginX);
    }

    private Color SelectTerrainColor(TerrainTilePreview terrain, bool underwater)
    {
        Color[] colors = underwater ? waterColors : mountainColors;
        int dV = terrain.Bottom - terrain.Top;
        int dH = terrain.Right - terrain.Left;
        double numerator = Math.Abs(LightX * (dH - dV) + LightY * (dH + dV) + LightZ * -32);
        double denominator = Math.Sqrt((dH * dH + dV * dV + 32 * 16) * 2.0 * (LightX * LightX + LightY * LightY + LightZ * LightZ));
        int index = (int)(colors.Length * numerator / denominator);
        return colors[Math.Clamp(index, 0, colors.Length - 1)];
    }

    private static Color[] LoadColors(XElement palette)
    {
        return palette.Elements("color")
            .Select(element =>
            {
                string[] parts = element.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                int x = int.Parse(parts[0]);
                int y = int.Parse(parts[1]);
                int z = int.Parse(parts[2]);
                return string.Equals((string?)element.Attribute("type"), "hsv", StringComparison.OrdinalIgnoreCase)
                    ? FromHsv(x, y, z)
                    : Color.FromRgb((byte)x, (byte)y, (byte)z);
            })
            .ToArray();
    }

    private static Color FromHsv(int h, int s, int v)
    {
        float hue = h / 40.0f;
        float saturation = s / 240.0f;
        float value = v / 240.0f;
        int sector = h / 40;
        float fraction = hue - sector;
        if ((sector & 1) == 0)
        {
            fraction = 1 - fraction;
        }

        byte m = ClampColor(256 * value * (1 - saturation));
        byte n = ClampColor(256 * value * (1 - saturation * fraction));
        byte o = ClampColor(256 * value);

        return sector switch
        {
            0 or 6 => Color.FromRgb(o, n, m),
            1 => Color.FromRgb(n, o, m),
            2 => Color.FromRgb(m, o, n),
            3 => Color.FromRgb(m, n, o),
            4 => Color.FromRgb(n, m, o),
            5 => Color.FromRgb(o, m, n),
            _ => Colors.Transparent
        };
    }

    private static byte ClampColor(float value)
    {
        return (byte)Math.Clamp((int)value, 0, 255);
    }

    private static void FillPolygon(DrawingContext context, IBrush brush, params Point[] points)
    {
        if (points.Length < 3)
        {
            return;
        }

        StreamGeometry geometry = new();
        using (StreamGeometryContext g = geometry.Open())
        {
            g.BeginFigure(points[0], true);
            for (int i = 1; i < points.Length; i++)
            {
                g.LineTo(points[i]);
            }

            g.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }

    private static void FillPolygon(DrawingContext context, IBrush brush, Pen? pen, params Point[] points)
    {
        if (points.Length < 3)
        {
            return;
        }

        StreamGeometry geometry = new();
        using (StreamGeometryContext g = geometry.Open())
        {
            g.BeginFigure(points[0], true);
            for (int i = 1; i < points.Length; i++)
            {
                g.LineTo(points[i]);
            }

            g.EndFigure(true);
        }

        context.DrawGeometry(brush, pen, geometry);
    }

    private static void FillOutline(DrawingContext context, Pen pen, params Point[] points)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext g = geometry.Open())
        {
            g.BeginFigure(points[0], false);
            for (int i = 1; i < points.Length; i++)
            {
                g.LineTo(points[i]);
            }

            g.EndFigure(true);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
