using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace FreeTrain.Modern;

public sealed class PictureContributionPreview : Control, IDisposable
{
    private readonly PictureContribution picture;
    private Bitmap? bitmap;
    private string? error;

    public PictureContributionPreview(PictureContribution picture)
    {
        this.picture = picture;
        Width = 128;
        Height = 112;
        ClipToBounds = true;

        try
        {
            bitmap = LegacyBitmap.LoadWithColorKey(picture.ResolvedPath);
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

        if (bitmap is not null)
        {
            Size imageSize = bitmap.Size;
            double scale = Math.Min(1.0, Math.Min(104 / imageSize.Width, 56 / imageSize.Height));
            Size targetSize = new(Math.Max(1, imageSize.Width * scale), Math.Max(1, imageSize.Height * scale));
            Point targetPoint = new((Bounds.Width - targetSize.Width) / 2, 10 + (56 - targetSize.Height) / 2);
            context.DrawImage(bitmap, new Rect(targetPoint, targetSize));
        }
        else
        {
            FormattedText failed = Text("Image error", 12, Brushes.Firebrick, FontWeight.SemiBold, Bounds.Width - 16);
            context.DrawText(failed, new Point(8, 22));
        }

        FormattedText source = Text(Path.GetFileName(picture.Source), 11, Brushes.Black, FontWeight.SemiBold, Bounds.Width - 12);
        context.DrawText(source, new Point(6, 72));

        string subtitle = error ?? picture.PluginDirectoryName;
        FormattedText plugin = Text(subtitle, 10, Brushes.Gray, FontWeight.Normal, Bounds.Width - 12);
        context.DrawText(plugin, new Point(6, 90));
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
        bitmap?.Dispose();
        bitmap = null;
    }
}
