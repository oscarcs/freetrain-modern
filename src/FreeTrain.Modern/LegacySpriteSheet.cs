using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace FreeTrain.Modern;

public sealed class LegacySpriteSheet : IDisposable
{
    private readonly Bitmap bitmap;

    public LegacySpriteSheet(string path, int tileWidth, int tileHeight)
    {
        bitmap = LegacyBitmap.LoadWithColorKey(path, includeTopLeftColor: false);
        TileWidth = tileWidth;
        TileHeight = tileHeight;
    }

    public int TileWidth { get; }
    public int TileHeight { get; }
    public Size TileSize => new(TileWidth, TileHeight);

    public void DrawTile(DrawingContext context, int tileIndex, Point destination)
    {
        Rect source = new(tileIndex * TileWidth, 0, TileWidth, TileHeight);
        Rect target = new(destination, TileSize);
        context.DrawImage(bitmap, source, target);
    }

    public void Dispose()
    {
        bitmap.Dispose();
    }
}
