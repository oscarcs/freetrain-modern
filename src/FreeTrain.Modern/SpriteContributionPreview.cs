using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace FreeTrain.Modern;

public sealed class SpriteContributionPreview : Control, IDisposable
{
    private readonly SpriteContribution sprite;
    private readonly SpriteFrame? frame;
    private readonly Dictionary<string, Bitmap> bitmaps = new(StringComparer.OrdinalIgnoreCase);
    private string? error;

    public SpriteContributionPreview(SpriteContribution sprite)
    {
        this.sprite = sprite;
        frame = sprite.Frames.FirstOrDefault(candidate => candidate.IsLoadable);
        Width = 156;
        Height = 136;
        ClipToBounds = true;

        try
        {
            foreach (SpriteFrame loadableFrame in EnumeratePreviewFrames().Where(candidate => candidate.IsLoadable))
            {
                GetBitmap(loadableFrame);
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect bounds = new(Bounds.Size);
        context.FillRectangle(Brushes.White, bounds);
        context.DrawRectangle(new Pen(Brushes.Gainsboro), bounds.Deflate(0.5));

        if (sprite.SpriteSet3D is { IsLoadable: true } spriteSet)
        {
            DrawSpriteSet3D(context, spriteSet);
        }
        else if (frame is not null && frame.IsLoadable)
        {
            DrawSingleFrame(context, frame);
        }
        else
        {
            FormattedText failed = Text("Sprite error", 12, Brushes.Firebrick, FontWeight.SemiBold, Bounds.Width - 16);
            context.DrawText(failed, new Point(8, 28));
        }

        FormattedText name = Text(sprite.DisplayName, 11, Brushes.Black, FontWeight.SemiBold, Bounds.Width - 12);
        context.DrawText(name, new Point(6, 78));

        string subtitle = error ?? $"{sprite.Type} | {sprite.PluginDirectoryName}";
        FormattedText type = Text(subtitle, 10, Brushes.Gray, FontWeight.Normal, Bounds.Width - 12);
        context.DrawText(type, new Point(6, 96));

        string sourceLabel = frame is null ? sprite.Error ?? "" : Path.GetFileName(frame.Source);
        FormattedText file = Text(sourceLabel, 10, Brushes.Gray, FontWeight.Normal, Bounds.Width - 12);
        context.DrawText(file, new Point(6, 112));
    }

    private void DrawSingleFrame(DrawingContext context, SpriteFrame spriteFrame)
    {
        Bitmap bitmap = GetBitmap(spriteFrame);
        Rect sourceRect = GetClampedSource(bitmap, spriteFrame);
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return;
        }

        double scale = Math.Min(1.0, Math.Min(124 / sourceRect.Width, 64 / sourceRect.Height));
        Size targetSize = new(sourceRect.Width * scale, sourceRect.Height * scale);
        Point targetPoint = new((Bounds.Width - targetSize.Width) / 2, 8 + (64 - targetSize.Height) / 2);
        context.DrawImage(bitmap, sourceRect, new Rect(targetPoint, targetSize));
    }

    private void DrawSpriteSet3D(DrawingContext context, ModernSpriteSet3D spriteSet)
    {
        Rect? bounds = null;
        foreach (ModernSpriteVoxel3D voxel in spriteSet.InVoxelDrawOrder())
        {
            Rect spriteBounds = GetVoxelDestination(voxel);
            bounds = bounds is null ? spriteBounds : bounds.Value.Union(spriteBounds);
        }

        if (bounds is null || bounds.Value.Width <= 0 || bounds.Value.Height <= 0)
        {
            return;
        }

        double scale = Math.Min(1.0, Math.Min(132 / bounds.Value.Width, 72 / bounds.Value.Height));
        Point origin = new(
            (Bounds.Width - bounds.Value.Width * scale) / 2 - bounds.Value.X * scale,
            6 + (72 - bounds.Value.Height * scale) / 2 - bounds.Value.Y * scale);

        foreach (ModernSpriteVoxel3D voxel in spriteSet.InVoxelDrawOrder())
        {
            if (!voxel.Frame.IsLoadable)
            {
                continue;
            }

            Bitmap bitmap = GetBitmap(voxel.Frame);
            Rect sourceRect = GetClampedSource(bitmap, voxel.Frame);
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            {
                continue;
            }

            Point voxelPoint = GetVoxelPoint(voxel.X, voxel.Y, voxel.Z);
            Point targetPoint = new(
                origin.X + (voxelPoint.X - voxel.Frame.OffsetX) * scale,
                origin.Y + (voxelPoint.Y - voxel.Frame.OffsetY) * scale);
            Size targetSize = new(sourceRect.Width * scale, sourceRect.Height * scale);
            context.DrawImage(bitmap, sourceRect, new Rect(targetPoint, targetSize));
        }
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

    private static Point GetVoxelPoint(int x, int y, int z)
    {
        return new Point((x + y) * 16, (-x + y) * 8 - z * 16);
    }

    private static Rect GetClampedSource(Bitmap bitmap, SpriteFrame spriteFrame)
    {
        Size imageSize = bitmap.Size;
        double sourceX = Math.Clamp(spriteFrame.SourceX, 0, Math.Max(0, imageSize.Width - 1));
        double sourceY = Math.Clamp(spriteFrame.SourceY, 0, Math.Max(0, imageSize.Height - 1));
        double sourceWidth = Math.Min(spriteFrame.SourceWidth, imageSize.Width - sourceX);
        double sourceHeight = Math.Min(spriteFrame.SourceHeight, imageSize.Height - sourceY);
        return new Rect(sourceX, sourceY, Math.Max(0, sourceWidth), Math.Max(0, sourceHeight));
    }

    private IEnumerable<SpriteFrame> EnumeratePreviewFrames()
    {
        if (sprite.SpriteSet3D is { IsLoadable: true } spriteSet)
        {
            return spriteSet.InVoxelDrawOrder().Select(voxel => voxel.Frame);
        }

        return frame is null ? Array.Empty<SpriteFrame>() : new[] { frame };
    }

    private Bitmap GetBitmap(SpriteFrame frame)
    {
        string key = BitmapCacheKey(frame);
        if (!bitmaps.TryGetValue(key, out Bitmap? bitmap))
        {
            bitmap = LegacyBitmap.LoadWithColorKey(frame.ResolvedPath, frame.ColorMapsForVariant(0));
            bitmaps[key] = bitmap;
        }

        return bitmap;
    }

    private static string BitmapCacheKey(SpriteFrame frame)
    {
        IReadOnlyList<ColorMapEntry> colorMaps = frame.ColorMapsForVariant(0);
        return colorMaps.Count == 0
            ? frame.ResolvedPath
            : $"{frame.ResolvedPath}|{string.Join(";", colorMaps.Select(map => $"{map.From}>{map.To}"))}";
    }

    private static FormattedText Text(string value, double size, IBrush brush, FontWeight weight, double maxWidth)
    {
        return new FormattedText(
            value,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Arial", FontStyle.Normal, weight),
            size,
            brush)
        {
            MaxTextWidth = Math.Max(1, maxWidth),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
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
