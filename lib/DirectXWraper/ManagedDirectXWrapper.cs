using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyVersion("1.0.0.9")]

namespace freetrain.DirectXWrapper
{
    public enum ColorMask { R, G, B }
    public enum DDSurfaceAllocation { Auto, ForceVideoMem, ForceSystemMem }

    public sealed class GDIGraphics : IDisposable
    {
        private readonly Surface surface;
        public readonly Graphics graphics;

        public GDIGraphics(Surface surface)
        {
            this.surface = surface;
            graphics = Graphics.FromImage(surface.Bitmap);
        }

        public void Dispose()
        {
            graphics.Dispose();
            surface.Flush();
        }
    }

    public class Surface : IDisposable
    {
        private Color sourceKey = Color.Empty;
        private bool hasSourceColorKey;

        internal Surface(Bitmap bitmap)
        {
            Bitmap = bitmap;
            size = bitmap.Size;
            resetClipRect();
        }

        internal Bitmap Bitmap { get; private set; }
        public Size size;
        public string displayModeName { get { return "managed-gdi"; } }
        public Rectangle clipRect { get; set; }
        public bool IsLost { get { return false; } }

        public Color sourceColorKey
        {
            set
            {
                sourceKey = value;
                hasSourceColorKey = true;
            }
        }

        public void resetClipRect()
        {
            clipRect = new Rectangle(Point.Empty, size);
        }

        public void bltFast(int destX, int destY, Surface source, Rectangle srcRect)
        {
            blt(new Point(destX, destY), source, srcRect);
        }

        public void blt(int dstX1, int dstY1, int dstX2, int dstY2, Surface source, int srcX1, int srcY1, int srcX2, int srcY2)
        {
            DrawImage(new Rectangle(dstX1, dstY1, dstX2 - dstX1, dstY2 - dstY1), source,
                new Rectangle(srcX1, srcY1, srcX2 - srcX1, srcY2 - srcY1), false, Color.Empty, false, null, null);
        }

        public void blt(Point dst, Surface source, Rectangle src)
        {
            DrawImage(new Rectangle(dst, src.Size), source, src, false, Color.Empty, false, null, null);
        }

        public void blt(Point dstPos, Surface source, Point srcPos, Size sz)
        {
            DrawImage(new Rectangle(dstPos, sz), source, new Rectangle(srcPos, sz), false, Color.Empty, false, null, null);
        }

        public void blt(Point dstPos, Surface source)
        {
            blt(dstPos, source, Point.Empty, source.size);
        }

        public void bltAlpha(Point dstPos, Surface source, Point srcPos, Size sz)
        {
            DrawImage(new Rectangle(dstPos, sz), source, new Rectangle(srcPos, sz), true, Color.Empty, false, null, null);
        }

        public void bltAlpha(Point dstPos, Surface source)
        {
            bltAlpha(dstPos, source, Point.Empty, source.size);
        }

        public void bltShape(Point dstPos, Surface source, Point srcPos, Size sz, Color fill)
        {
            DrawImage(new Rectangle(dstPos, sz), source, new Rectangle(srcPos, sz), false, fill, false, null, null);
        }

        public void bltShape(Point dstPos, Surface source, Color fill)
        {
            bltShape(dstPos, source, Point.Empty, source.size, fill);
        }

        public void bltColorTransform(Point dstPos, Surface source, Point srcPos, Size sz, Color[] srcColors, Color[] dstColors, bool vflip)
        {
            DrawImage(new Rectangle(dstPos, sz), source, new Rectangle(srcPos, sz), false, Color.Empty, vflip, srcColors, dstColors);
        }

        public void bltHueTransform(Point dstPos, Surface source, Point srcPos, Size sz, Color rDest, Color gDest, Color bDest)
        {
            blt(dstPos, source, srcPos, sz);
        }

        public void fill(Color c)
        {
            fill(clipRect, c);
        }

        public void fill(Rectangle rect, Color c)
        {
            Rectangle target = Rectangle.Intersect(rect, clipRect);
            if (target.Width <= 0 || target.Height <= 0) return;
            using (Graphics g = Graphics.FromImage(Bitmap))
            using (Brush b = new SolidBrush(c))
            {
                g.FillRectangle(b, target);
            }
        }

        public bool HitTest(Point p)
        {
            return HitTest(p.X, p.Y);
        }

        public bool HitTest(int x, int y)
        {
            if (x < 0 || x >= size.Width || y < 0 || y >= size.Height) return false;
            if (!hasSourceColorKey) return true;
            return Bitmap.GetPixel(x, y).ToArgb() != sourceKey.ToArgb();
        }

        public void drawPolygon(Point p1, Point p2, Point p3, Point p4)
        {
            using (Graphics g = Graphics.FromImage(Bitmap))
            {
                g.DrawPolygon(Pens.Black, new[] { p1, p2, p3, p4 });
            }
        }

        public void drawBox(Rectangle r)
        {
            using (Graphics g = Graphics.FromImage(Bitmap))
            {
                g.DrawRectangle(Pens.Black, r);
            }
        }

        public void restore() {}

        public Bitmap createBitmap()
        {
            return (Bitmap)Bitmap.Clone();
        }

        public void GDICopyBits(Graphics g, Rectangle dst, Rectangle src)
        {
            g.DrawImage(Bitmap, dst, src, GraphicsUnit.Pixel);
        }

        public void GDICopyBits(Graphics g, Rectangle dst, Point src)
        {
            GDICopyBits(g, dst, new Rectangle(src, dst.Size));
        }

        public void Dispose()
        {
            if (Bitmap != null)
            {
                Bitmap.Dispose();
                Bitmap = null;
            }
        }

        internal void Flush() {}

        private void DrawImage(Rectangle dst, Surface source, Rectangle src, bool alpha, Color shapeFill, bool vflip, Color[] srcColors, Color[] dstColors)
        {
            Rectangle clippedDst = Rectangle.Intersect(dst, clipRect);
            if (clippedDst.Width <= 0 || clippedDst.Height <= 0) return;

            Rectangle clippedSrc = src;
            clippedSrc.X += clippedDst.X - dst.X;
            clippedSrc.Y += clippedDst.Y - dst.Y;
            clippedSrc.Width = Math.Min(clippedDst.Width, src.Width);
            clippedSrc.Height = Math.Min(clippedDst.Height, src.Height);

            using (Graphics g = Graphics.FromImage(Bitmap))
            using (Bitmap image = PrepareBitmap(source, clippedSrc, alpha, shapeFill, vflip, srcColors, dstColors))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(image, clippedDst);
            }
        }

        private static Bitmap PrepareBitmap(Surface source, Rectangle src, bool alpha, Color shapeFill, bool vflip, Color[] srcColors, Color[] dstColors)
        {
            Bitmap clone = source.Bitmap.Clone(src, PixelFormat.Format32bppArgb);
            if (vflip) clone.RotateFlip(RotateFlipType.RotateNoneFlipY);

            bool needsPixelPass = source.hasSourceColorKey || alpha || !shapeFill.IsEmpty || (srcColors != null && srcColors.Length != 0);
            if (!needsPixelPass) return clone;

            for (int y = 0; y < clone.Height; y++)
            {
                for (int x = 0; x < clone.Width; x++)
                {
                    Color c = clone.GetPixel(x, y);
                    if (source.hasSourceColorKey && c.ToArgb() == source.sourceKey.ToArgb())
                    {
                        clone.SetPixel(x, y, Color.Transparent);
                        continue;
                    }

                    if (!shapeFill.IsEmpty)
                    {
                        clone.SetPixel(x, y, Color.FromArgb(c.A, shapeFill));
                    }
                    else if (srcColors != null && dstColors != null)
                    {
                        for (int i = 0; i < srcColors.Length && i < dstColors.Length; i++)
                        {
                            if ((c.ToArgb() & 0x00ffffff) == (srcColors[i].ToArgb() & 0x00ffffff))
                            {
                                clone.SetPixel(x, y, Color.FromArgb(c.A, dstColors[i]));
                                break;
                            }
                        }
                    }
                }
            }
            return clone;
        }
    }

    public class DirectDraw : IDisposable
    {
        public static DDSurfaceAllocation SurfeceAllocation { get; set; }
        public int totalVideoMemory { get { return 0; } }
        public int availableVideoMemory { get { return 0; } }
        public string displayModeName { get { return "managed-gdi"; } }

        public Surface createOffscreenSurface(int width, int height)
        {
            return new Surface(new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb));
        }

        public Surface createOffscreenSurface(Size sz)
        {
            return createOffscreenSurface(sz.Width, sz.Height);
        }

        public Surface createFromImage(Image img)
        {
            Surface surface = createOffscreenSurface(img.Width, img.Height);
            using (GDIGraphics g = new GDIGraphics(surface))
            {
                g.graphics.DrawImage(img, new Rectangle(Point.Empty, img.Size));
            }
            return surface;
        }

        public Surface createSprite(Bitmap img)
        {
            Surface surface = createFromImage(img);
            surface.sourceColorKey = img.GetPixel(0, 0);
            return surface;
        }

        public static bool isSurfaceLostException(COMException e)
        {
            return false;
        }

        public virtual void Dispose() {}
    }

    public class WindowedDirectDraw : DirectDraw
    {
        public Surface primarySurface { get; private set; }

        public WindowedDirectDraw(Control control)
        {
            Size size = control.ClientSize;
            if (size.Width <= 0 || size.Height <= 0) size = new Size(1, 1);
            primarySurface = createOffscreenSurface(size);
        }
    }

    public static class NightImageBuilder
    {
        public static int BuildNightImage(Surface surface)
        {
            using (Graphics g = Graphics.FromImage(surface.Bitmap))
            using (Brush b = new SolidBrush(Color.FromArgb(96, Color.Black)))
            {
                g.FillRectangle(b, new Rectangle(Point.Empty, surface.size));
            }
            return 0;
        }
    }

    public sealed class BGM : IDisposable
    {
        public string fileName { get; set; }
        public int volume { get; set; }
        public bool loop;
        public void run() {}
        public void pause() {}
        public void stop() {}
        public void Dispose() {}
    }

    public sealed class AudioPath : IDisposable
    {
        public void Dispose() {}
    }

    public sealed class Segment : IDisposable
    {
        public int repeats { get; set; }
        public static Segment fromFile(string fileName) { return new Segment(); }
        public static Segment fromMidiFile(string fileName) { return new Segment(); }
        public Segment clone() { return new Segment { repeats = repeats }; }
        public void downloadTo(Performance p) {}
        public void unloadFrom(Performance p) {}
        public void Dispose() {}
    }

    public sealed class SegmentState
    {
        public bool isPlaying { get { return false; } }
    }

    public sealed class Performance : IDisposable
    {
        public Performance(IWin32Window owner) {}
        public SegmentState playExclusive(Segment seg) { return new SegmentState(); }
        public SegmentState play(Segment seg) { return new SegmentState(); }
        public SegmentState play(Segment seg, int leadTime) { return new SegmentState(); }
        public AudioPath createAudioPath(Segment seg) { return new AudioPath(); }
        public void Dispose() {}
    }
}
