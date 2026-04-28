using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace FreeTrain.Modern;

internal abstract class PlacementPreviewControl : Control, IDisposable
{
    private readonly Dictionary<string, Bitmap> bitmaps = new(StringComparer.OrdinalIgnoreCase);

    protected PlacementPreviewControl()
    {
        Width = 124;
        Height = 64;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(248, 250, 251)), new Rect(Bounds.Size));
    }

    protected void DrawSpriteFrame(
        DrawingContext context,
        SpriteFrame frame,
        Point origin,
        double maxWidth,
        double maxHeight,
        int colorVariantIndex = 0)
    {
        if (!frame.IsLoadable)
        {
            return;
        }

        Bitmap bitmap = GetBitmap(frame, colorVariantIndex);
        Rect source = GetClampedSource(bitmap, frame);
        if (source.Width <= 0 || source.Height <= 0)
        {
            return;
        }

        double scale = Math.Min(1.0, Math.Min(maxWidth / source.Width, maxHeight / source.Height));
        Size targetSize = new(source.Width * scale, source.Height * scale);
        Point target = new(origin.X - frame.OffsetX * scale, origin.Y - frame.OffsetY * scale);
        context.DrawImage(bitmap, source, new Rect(target, targetSize));
    }

    protected void DrawSpriteSet2D(DrawingContext context, ModernSpriteSet2D spriteSet, int colorVariantIndex = 0)
    {
        Rect? sourceBounds = null;
        foreach (ModernSpriteVoxel2D voxel in spriteSet.InVoxelDrawOrder())
        {
            Rect frameBounds = GetVoxelDestination(voxel);
            sourceBounds = sourceBounds is null ? frameBounds : sourceBounds.Value.Union(frameBounds);
        }

        if (sourceBounds is null || sourceBounds.Value.Width <= 0 || sourceBounds.Value.Height <= 0)
        {
            return;
        }

        double scale = Math.Min(1.0, Math.Min((Bounds.Width - 12) / sourceBounds.Value.Width, (Bounds.Height - 10) / sourceBounds.Value.Height));
        Point origin = new(
            (Bounds.Width - sourceBounds.Value.Width * scale) / 2 - sourceBounds.Value.X * scale,
            6 + (Bounds.Height - 10 - sourceBounds.Value.Height * scale) / 2 - sourceBounds.Value.Y * scale);

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            foreach (ModernSpriteVoxel2D voxel in spriteSet.InVoxelDrawOrder())
            {
                if (!voxel.Frame.IsLoadable)
                {
                    continue;
                }

                Point voxelPoint = GetVoxelPoint(voxel.X, voxel.Y);
                DrawSpriteFrame(context, voxel.Frame, new Point(origin.X / scale + voxelPoint.X, origin.Y / scale + voxelPoint.Y), Bounds.Width / scale, Bounds.Height / scale, colorVariantIndex);
            }
        }
    }

    protected void DrawSpriteSet3D(DrawingContext context, ModernSpriteSet3D spriteSet, int colorVariantIndex = 0)
    {
        Rect? sourceBounds = null;
        foreach (ModernSpriteVoxel3D voxel in spriteSet.InVoxelDrawOrder())
        {
            Rect frameBounds = GetVoxelDestination(voxel);
            sourceBounds = sourceBounds is null ? frameBounds : sourceBounds.Value.Union(frameBounds);
        }

        if (sourceBounds is null || sourceBounds.Value.Width <= 0 || sourceBounds.Value.Height <= 0)
        {
            return;
        }

        double scale = Math.Min(1.0, Math.Min((Bounds.Width - 12) / sourceBounds.Value.Width, (Bounds.Height - 10) / sourceBounds.Value.Height));
        Point origin = new(
            (Bounds.Width - sourceBounds.Value.Width * scale) / 2 - sourceBounds.Value.X * scale,
            6 + (Bounds.Height - 10 - sourceBounds.Value.Height * scale) / 2 - sourceBounds.Value.Y * scale);

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            foreach (ModernSpriteVoxel3D voxel in spriteSet.InVoxelDrawOrder())
            {
                if (!voxel.Frame.IsLoadable)
                {
                    continue;
                }

                Point voxelPoint = GetVoxelPoint(voxel.X, voxel.Y, voxel.Z);
                DrawSpriteFrame(context, voxel.Frame, new Point(origin.X / scale + voxelPoint.X, origin.Y / scale + voxelPoint.Y), Bounds.Width / scale, Bounds.Height / scale, colorVariantIndex);
            }
        }
    }

    protected Bitmap GetBitmap(SpriteFrame frame, int colorVariantIndex = 0)
    {
        string key = BitmapCacheKey(frame, colorVariantIndex);
        if (!bitmaps.TryGetValue(key, out Bitmap? bitmap))
        {
            bitmap = LegacyBitmap.LoadWithColorKey(frame.ResolvedPath, frame.ColorMaps, colorVariantIndex);
            bitmaps[key] = bitmap;
        }

        return bitmap;
    }

    private static string BitmapCacheKey(SpriteFrame frame, int colorVariantIndex)
    {
        return frame.ColorMaps.Count == 0
            ? frame.ResolvedPath
            : $"{frame.ResolvedPath}|{colorVariantIndex}|{string.Join(";", frame.ColorMaps.Select(map => $"{map.From}>{map.To}"))}";
    }

    protected static Rect GetClampedSource(Bitmap bitmap, SpriteFrame frame)
    {
        Size imageSize = bitmap.Size;
        double sourceX = Math.Clamp(frame.SourceX, 0, Math.Max(0, imageSize.Width - 1));
        double sourceY = Math.Clamp(frame.SourceY, 0, Math.Max(0, imageSize.Height - 1));
        double sourceWidth = Math.Min(frame.SourceWidth, imageSize.Width - sourceX);
        double sourceHeight = Math.Min(frame.SourceHeight, imageSize.Height - sourceY);
        return new Rect(sourceX, sourceY, Math.Max(0, sourceWidth), Math.Max(0, sourceHeight));
    }

    private static Rect GetVoxelDestination(ModernSpriteVoxel2D voxel)
    {
        Point voxelPoint = GetVoxelPoint(voxel.X, voxel.Y);
        return new Rect(
            voxelPoint.X - voxel.Frame.OffsetX,
            voxelPoint.Y - voxel.Frame.OffsetY,
            Math.Max(0, voxel.Frame.SourceWidth),
            Math.Max(0, voxel.Frame.SourceHeight));
    }

    private static Rect GetVoxelDestination(ModernSpriteVoxel3D voxel)
    {
        Point voxelPoint = GetVoxelPoint(voxel.X, voxel.Y, voxel.Z);
        return new Rect(
            voxelPoint.X - voxel.Frame.OffsetX,
            voxelPoint.Y - voxel.Frame.OffsetY,
            Math.Max(0, voxel.Frame.SourceWidth),
            Math.Max(0, voxel.Frame.SourceHeight));
    }

    private static Point GetVoxelPoint(int x, int y)
    {
        return new Point((x + y) * 16, (-x + y) * 8);
    }

    private static Point GetVoxelPoint(int x, int y, int z)
    {
        return new Point((x + y) * 16, (-x + y) * 8 - z * 16);
    }

    public void Dispose()
    {
        foreach (Bitmap bitmap in bitmaps.Values)
        {
            bitmap.Dispose();
        }

        bitmaps.Clear();
    }
}

internal sealed class EmptyPlacementPreviewControl : PlacementPreviewControl
{
}

internal sealed class RailPlacementPreviewControl : PlacementPreviewControl
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        DrawTile(context, 40, 26);
        DrawTile(context, 58, 34);
        DrawTile(context, 76, 42);
        Pen railPen = new(new SolidColorBrush(Color.FromRgb(44, 50, 56)), 3);
        context.DrawLine(railPen, new Point(40, 31), new Point(91, 54));
        context.DrawLine(new Pen(Brushes.White, 1), new Point(42, 27), new Point(93, 50));
    }

    private static void DrawTile(DrawingContext context, double x, double y)
    {
        StreamGeometry geometry = new();
        using StreamGeometryContext path = geometry.Open();
        path.BeginFigure(new Point(x + 16, y), true);
        path.LineTo(new Point(x + 32, y + 8));
        path.LineTo(new Point(x + 16, y + 16));
        path.LineTo(new Point(x, y + 8));
        path.EndFigure(true);
        context.DrawGeometry(new SolidColorBrush(Color.FromRgb(218, 232, 214)), new Pen(new SolidColorBrush(Color.FromRgb(194, 207, 198)), 1), geometry);
    }
}

internal sealed class RoadPlacementPreviewControl : PlacementPreviewControl
{
    private readonly RoadContribution road;

    public RoadPlacementPreviewControl(RoadContribution road)
    {
        this.road = road;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        byte[] masks = { 1, 3, 5, 10, 12, 15 };
        for (int i = 0; i < masks.Length; i++)
        {
            if (!road.FramesByMask.TryGetValue(masks[i], out SpriteFrame? frame) || !frame.IsLoadable)
            {
                continue;
            }

            Bitmap bitmap = GetBitmap(frame);
            Rect source = GetClampedSource(bitmap, frame);
            if (source.Width <= 0 || source.Height <= 0)
            {
                continue;
            }

            Rect cell = new(12 + i % 3 * 34, 7 + i / 3 * 26, 30, 24);
            double scale = Math.Min(1.0, Math.Min(cell.Width / source.Width, cell.Height / source.Height));
            Size targetSize = new(source.Width * scale, source.Height * scale);
            Point target = new(cell.X + (cell.Width - targetSize.Width) / 2, cell.Y + (cell.Height - targetSize.Height) / 2);
            context.DrawImage(bitmap, source, new Rect(target, targetSize));
        }
    }
}

internal sealed class StationPlacementPreviewControl : PlacementPreviewControl
{
    private readonly StationContribution station;

    public StationPlacementPreviewControl(StationContribution station)
    {
        this.station = station;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (station.SpriteSet2D is { IsLoadable: true } spriteSet)
        {
            DrawSpriteSet2D(context, spriteSet);
            return;
        }

        if (station.Frame is { IsLoadable: true } frame)
        {
            DrawSpriteFrame(context, frame, new Point(62, 42), Bounds.Width - 12, Bounds.Height - 10);
        }
    }
}

internal sealed class StructurePlacementPreviewControl : PlacementPreviewControl
{
    private readonly SpriteContribution structure;
    private readonly int colorVariantIndex;

    public StructurePlacementPreviewControl(SpriteContribution structure, int colorVariantIndex = 0)
    {
        this.structure = structure;
        this.colorVariantIndex = colorVariantIndex;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (structure.SpriteSet3D is { IsLoadable: true } spriteSet)
        {
            DrawSpriteSet3D(context, spriteSet, colorVariantIndex);
            return;
        }

        if (structure.Frames.FirstOrDefault(frame => frame.IsLoadable) is { } frame)
        {
            DrawSpriteFrame(context, frame, new Point(62, 44), Bounds.Width - 12, Bounds.Height - 10, colorVariantIndex);
        }
    }
}

internal sealed class PlatformPlacementPreviewControl : PlacementPreviewControl
{
    private readonly string description;

    public PlatformPlacementPreviewControl(string description)
    {
        this.description = description;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        bool wide = description.Contains("wide", StringComparison.OrdinalIgnoreCase);
        bool roof = description.Contains("roof", StringComparison.OrdinalIgnoreCase) && !description.Contains("no roof", StringComparison.OrdinalIgnoreCase);
        IBrush baseBrush = wide
            ? new SolidColorBrush(Color.FromRgb(183, 178, 158))
            : new SolidColorBrush(Color.FromRgb(204, 199, 181));
        Pen outline = new(new SolidColorBrush(Color.FromRgb(87, 87, 78)), 1);

        for (int i = 0; i < 4; i++)
        {
            double x = 26 + i * 18;
            double y = 16 + i * 8;
            StreamGeometry geometry = new();
            using (StreamGeometryContext path = geometry.Open())
            {
                path.BeginFigure(new Point(x + 16, y), true);
                path.LineTo(new Point(x + 32, y + 8));
                path.LineTo(new Point(x + 16, y + 16));
                path.LineTo(new Point(x, y + 8));
                path.EndFigure(true);
            }

            context.DrawGeometry(baseBrush, outline, geometry);
            if (roof && i is 1 or 2)
            {
                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(73, 92, 110)), 3), new Point(x + 9, y + 7), new Point(x + 23, y + 1));
            }
        }
    }
}

internal sealed class TrainPlacementPreviewControl : PlacementPreviewControl
{
    private readonly TrainContribution train;
    private readonly IReadOnlyDictionary<string, TrainCarContribution> trainCars;

    public TrainPlacementPreviewControl(TrainContribution train, IReadOnlyDictionary<string, TrainCarContribution> trainCars)
    {
        this.train = train;
        this.trainCars = trainCars;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        List<(Bitmap Bitmap, Rect Source)> frames = new();
        foreach (string carId in train.CreateCarIds(3))
        {
            if (!trainCars.TryGetValue(carId, out TrainCarContribution? car)
                || car.FrameForAngle(6) is not { IsLoadable: true } frame)
            {
                continue;
            }

            Bitmap bitmap = GetBitmap(frame);
            Rect source = GetClampedSource(bitmap, frame);
            if (source.Width > 0 && source.Height > 0)
            {
                frames.Add((bitmap, source));
            }
        }

        if (frames.Count == 0)
        {
            return;
        }

        double totalWidth = frames.Sum(frame => frame.Source.Width);
        double maxHeight = frames.Max(frame => frame.Source.Height);
        double scale = Math.Min(1.0, Math.Min((Bounds.Width - 14) / totalWidth, (Bounds.Height - 14) / maxHeight));
        double x = (Bounds.Width - totalWidth * scale) / 2;
        double y = (Bounds.Height - maxHeight * scale) / 2;
        foreach ((Bitmap bitmap, Rect source) in frames)
        {
            Size size = new(source.Width * scale, source.Height * scale);
            context.DrawImage(bitmap, source, new Rect(new Point(x, y + (maxHeight * scale - size.Height) / 2), size));
            x += size.Width;
        }
    }
}
