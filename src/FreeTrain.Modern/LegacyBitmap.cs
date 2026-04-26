using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FreeTrain.Modern;

public static class LegacyBitmap
{
    public static Bitmap LoadWithColorKey(string path, bool includeTopLeftColor = true, bool includeMagenta = true)
    {
        using Bitmap source = new(path);
        WriteableBitmap target = new(source.PixelSize, source.Dpi, PixelFormats.Bgra8888, AlphaFormat.Premul);

        using (ILockedFramebuffer framebuffer = target.Lock())
        {
            source.CopyPixels(framebuffer);
            ApplyColorKey(framebuffer, includeTopLeftColor, includeMagenta);
        }

        return target;
    }

    private static void ApplyColorKey(ILockedFramebuffer framebuffer, bool includeTopLeftColor, bool includeMagenta)
    {
        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int rowBytes = framebuffer.RowBytes;
        if (width <= 0 || height <= 0 || rowBytes < width * 4)
        {
            return;
        }

        byte[] row = new byte[rowBytes];
        Marshal.Copy(framebuffer.Address, row, 0, rowBytes);
        byte keyB = row[0];
        byte keyG = row[1];
        byte keyR = row[2];

        for (int y = 0; y < height; y++)
        {
            IntPtr rowAddress = IntPtr.Add(framebuffer.Address, y * rowBytes);
            Marshal.Copy(rowAddress, row, 0, rowBytes);

            for (int x = 0; x < width; x++)
            {
                int index = x * 4;
                bool isTopLeftColor = includeTopLeftColor
                    && row[index] == keyB
                    && row[index + 1] == keyG
                    && row[index + 2] == keyR;
                bool isMagenta = includeMagenta
                    && row[index] == 255
                    && row[index + 1] == 0
                    && row[index + 2] == 255;

                if (isTopLeftColor || isMagenta)
                {
                    row[index] = 0;
                    row[index + 1] = 0;
                    row[index + 2] = 0;
                    row[index + 3] = 0;
                }
            }

            Marshal.Copy(row, 0, rowAddress, rowBytes);
        }
    }
}
