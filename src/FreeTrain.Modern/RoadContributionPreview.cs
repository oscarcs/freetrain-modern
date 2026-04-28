using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace FreeTrain.Modern;

public sealed class RoadContributionPreview : Control, IDisposable
{
    private readonly RoadContribution road;
    private readonly Dictionary<string, Bitmap> bitmaps = new(StringComparer.OrdinalIgnoreCase);
    private string? error;

    public RoadContributionPreview(RoadContribution road)
    {
        this.road = road;
        Width = 164;
        Height = 144;
        ClipToBounds = true;

        try
        {
            foreach (SpriteFrame frame in road.FramesByMask.Values.Where(frame => frame.IsLoadable).DistinctBy(frame => (frame.ResolvedPath, frame.SourceX, frame.SourceY)))
            {
                GetBitmap(frame);
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

        DrawFrames(context);

        FormattedText name = Text(road.DisplayName, 11, Brushes.Black, FontWeight.SemiBold, Bounds.Width - 12);
        context.DrawText(name, new Point(6, 90));

        string style = road.Style.Lanes > 0
            ? $"{road.Kind} | {road.Style.MajorType} | {road.Style.Lanes} lanes"
            : $"{road.Kind} | {road.PluginDirectoryName}";
        FormattedText type = Text(error ?? style, 10, error is null ? Brushes.Gray : Brushes.Firebrick, FontWeight.Normal, Bounds.Width - 12);
        context.DrawText(type, new Point(6, 108));

        string description = string.IsNullOrWhiteSpace(road.Description) ? road.PluginDirectoryName : road.Description;
        FormattedText desc = Text(description, 10, Brushes.Gray, FontWeight.Normal, Bounds.Width - 12);
        context.DrawText(desc, new Point(6, 124));
    }

    private void DrawFrames(DrawingContext context)
    {
        if (!road.IsLoadable)
        {
            FormattedText failed = Text(road.Error ?? "Road error", 12, Brushes.Firebrick, FontWeight.SemiBold, Bounds.Width - 16);
            context.DrawText(failed, new Point(8, 28));
            return;
        }

        for (byte mask = 1; mask <= 15; mask++)
        {
            if (!road.FramesByMask.TryGetValue(mask, out SpriteFrame? frame) || !frame.IsLoadable)
            {
                continue;
            }

            Bitmap bitmap = GetBitmap(frame);
            Rect source = GetClampedSource(bitmap, frame);
            if (source.Width <= 0 || source.Height <= 0)
            {
                continue;
            }

            int index = mask - 1;
            Rect cell = new(8 + index % 5 * 30, 8 + index / 5 * 26, 28, 24);
            double scale = Math.Min(1.0, Math.Min(cell.Width / source.Width, cell.Height / source.Height));
            Size targetSize = new(source.Width * scale, source.Height * scale);
            Point target = new(
                cell.X + (cell.Width - targetSize.Width) / 2,
                cell.Y + (cell.Height - targetSize.Height) / 2);
            context.DrawImage(bitmap, source, new Rect(target, targetSize));
        }
    }

    private Bitmap GetBitmap(SpriteFrame frame)
    {
        string key = BitmapCacheKey(frame);
        if (!bitmaps.TryGetValue(key, out Bitmap? bitmap))
        {
            bitmap = LegacyBitmap.LoadWithColorKey(frame.ResolvedPath, frame.ColorMaps);
            bitmaps[key] = bitmap;
        }

        return bitmap;
    }

    private static string BitmapCacheKey(SpriteFrame frame)
    {
        return frame.ColorMaps.Count == 0
            ? frame.ResolvedPath
            : $"{frame.ResolvedPath}|{string.Join(";", frame.ColorMaps.Select(map => $"{map.From}>{map.To}"))}";
    }

    private static Rect GetClampedSource(Bitmap bitmap, SpriteFrame frame)
    {
        Size imageSize = bitmap.Size;
        double sourceX = Math.Clamp(frame.SourceX, 0, Math.Max(0, imageSize.Width - 1));
        double sourceY = Math.Clamp(frame.SourceY, 0, Math.Max(0, imageSize.Height - 1));
        double sourceWidth = Math.Min(frame.SourceWidth, imageSize.Width - sourceX);
        double sourceHeight = Math.Min(frame.SourceHeight, imageSize.Height - sourceY);
        return new Rect(sourceX, sourceY, Math.Max(0, sourceWidth), Math.Max(0, sourceHeight));
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
