using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace FreeTrain.Modern;

public sealed partial class MainWindow : Window
{
    private enum ToolGlyphKind
    {
        Pointer,
        Road,
        Rail,
        Station,
        Structure,
        Platform,
        Train,
        Terrain,
        Bulldoze,
        ZoomOut,
        ZoomIn,
        Play,
        Pause,
        Slower,
        Faster,
        Night
    }

    private sealed class StripIcon : Control
    {
        private readonly Bitmap bitmap;
        private readonly int index;
        private readonly int sourceWidth;
        private readonly int sourceHeight;
        private readonly double size;

        public StripIcon(Bitmap bitmap, int index, int sourceWidth, int sourceHeight, double size = 28)
        {
            this.bitmap = bitmap;
            this.index = index;
            this.sourceWidth = sourceWidth;
            this.sourceHeight = sourceHeight;
            this.size = size;
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(size, size);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            double scale = Math.Floor(Math.Min(Bounds.Width / sourceWidth, Bounds.Height / sourceHeight));
            scale = Math.Max(1, scale);
            Size targetSize = new(sourceWidth * scale, sourceHeight * scale);
            Point targetPoint = new((Bounds.Width - targetSize.Width) / 2, (Bounds.Height - targetSize.Height) / 2);
            Rect source = new(index * sourceWidth, 0, sourceWidth, sourceHeight);
            context.DrawImage(bitmap, source, new Rect(targetPoint, targetSize));
        }
    }

    private sealed class ToolGlyph : Control
    {
        private readonly ToolGlyphKind kind;
        private readonly double size;
        private readonly Pen pen;
        private readonly IBrush brush;

        public ToolGlyph(ToolGlyphKind kind, double size = 28, Color? color = null)
        {
            this.kind = kind;
            this.size = size;
            Color glyphColor = color ?? Color.FromRgb(28, 34, 40);
            brush = new SolidColorBrush(glyphColor);
            pen = new Pen(brush, 2);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(size, size);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            double w = Bounds.Width;
            double h = Bounds.Height;
            switch (kind)
            {
                case ToolGlyphKind.Pointer:
                    DrawPolygon(context, brush,
                        new Point(w * 0.28, h * 0.18),
                        new Point(w * 0.28, h * 0.76),
                        new Point(w * 0.43, h * 0.61),
                        new Point(w * 0.55, h * 0.82),
                        new Point(w * 0.66, h * 0.76),
                        new Point(w * 0.53, h * 0.56),
                        new Point(w * 0.74, h * 0.56));
                    break;
                case ToolGlyphKind.Road:
                    context.DrawLine(pen, new Point(w * 0.16, h * 0.66), new Point(w * 0.84, h * 0.34));
                    context.DrawLine(pen, new Point(w * 0.20, h * 0.78), new Point(w * 0.88, h * 0.46));
                    context.DrawLine(new Pen(Brushes.White, 1.5), new Point(w * 0.36, h * 0.62), new Point(w * 0.48, h * 0.56));
                    context.DrawLine(new Pen(Brushes.White, 1.5), new Point(w * 0.58, h * 0.51), new Point(w * 0.70, h * 0.45));
                    break;
                case ToolGlyphKind.Station:
                    DrawDiamond(context, new SolidColorBrush(Color.FromRgb(197, 207, 188)), new Pen(brush, 1.5), w, h);
                    context.DrawRectangle(brush, null, new Rect(w * 0.30, h * 0.40, w * 0.40, h * 0.28));
                    DrawPolygon(context, brush, new Point(w * 0.24, h * 0.42), new Point(w * 0.50, h * 0.24), new Point(w * 0.76, h * 0.42));
                    context.DrawRectangle(Brushes.White, null, new Rect(w * 0.46, h * 0.52, w * 0.08, h * 0.16));
                    break;
                case ToolGlyphKind.Structure:
                    DrawDiamond(context, new SolidColorBrush(Color.FromRgb(210, 203, 186)), new Pen(brush, 1.5), w, h);
                    context.DrawRectangle(brush, null, new Rect(w * 0.28, h * 0.42, w * 0.44, h * 0.26));
                    context.DrawRectangle(brush, null, new Rect(w * 0.36, h * 0.26, w * 0.28, h * 0.18));
                    context.DrawLine(new Pen(Brushes.White, 1.5), new Point(w * 0.42, h * 0.47), new Point(w * 0.42, h * 0.62));
                    context.DrawLine(new Pen(Brushes.White, 1.5), new Point(w * 0.58, h * 0.47), new Point(w * 0.58, h * 0.62));
                    break;
                case ToolGlyphKind.Platform:
                    DrawDiamond(context, new SolidColorBrush(Color.FromRgb(190, 187, 172)), new Pen(brush, 1.5), w, h);
                    context.DrawLine(new Pen(brush, 3), new Point(w * 0.22, h * 0.62), new Point(w * 0.78, h * 0.38));
                    context.DrawLine(new Pen(Brushes.White, 2), new Point(w * 0.28, h * 0.50), new Point(w * 0.72, h * 0.30));
                    break;
                case ToolGlyphKind.Train:
                    context.DrawRectangle(brush, null, new Rect(w * 0.22, h * 0.38, w * 0.56, h * 0.24));
                    DrawPolygon(context, brush, new Point(w * 0.78, h * 0.38), new Point(w * 0.88, h * 0.50), new Point(w * 0.78, h * 0.62));
                    context.DrawRectangle(Brushes.White, null, new Rect(w * 0.32, h * 0.43, w * 0.10, h * 0.08));
                    context.DrawRectangle(Brushes.White, null, new Rect(w * 0.48, h * 0.43, w * 0.10, h * 0.08));
                    context.DrawLine(pen, new Point(w * 0.20, h * 0.72), new Point(w * 0.82, h * 0.72));
                    break;
                case ToolGlyphKind.Terrain:
                    DrawDiamond(context, new SolidColorBrush(Color.FromRgb(80, 150, 88)), new Pen(brush, 1.5), w, h);
                    context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(241, 241, 179)), 2), new Point(w * 0.50, h * 0.20), new Point(w * 0.50, h * 0.55));
                    context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(241, 241, 179)), 2), new Point(w * 0.38, h * 0.32), new Point(w * 0.50, h * 0.20));
                    context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(241, 241, 179)), 2), new Point(w * 0.62, h * 0.32), new Point(w * 0.50, h * 0.20));
                    break;
                case ToolGlyphKind.Bulldoze:
                    context.DrawRectangle(brush, null, new Rect(w * 0.20, h * 0.52, w * 0.48, h * 0.18));
                    context.DrawRectangle(null, pen, new Rect(w * 0.30, h * 0.32, w * 0.26, h * 0.20));
                    context.DrawLine(pen, new Point(w * 0.64, h * 0.50), new Point(w * 0.84, h * 0.38));
                    context.DrawLine(pen, new Point(w * 0.68, h * 0.70), new Point(w * 0.86, h * 0.70));
                    break;
                case ToolGlyphKind.ZoomOut:
                    context.DrawEllipse(null, pen, new Point(w * 0.42, h * 0.42), w * 0.20, h * 0.20);
                    context.DrawLine(pen, new Point(w * 0.57, h * 0.57), new Point(w * 0.78, h * 0.78));
                    context.DrawLine(pen, new Point(w * 0.30, h * 0.42), new Point(w * 0.54, h * 0.42));
                    break;
                case ToolGlyphKind.ZoomIn:
                    context.DrawEllipse(null, pen, new Point(w * 0.42, h * 0.42), w * 0.20, h * 0.20);
                    context.DrawLine(pen, new Point(w * 0.57, h * 0.57), new Point(w * 0.78, h * 0.78));
                    context.DrawLine(pen, new Point(w * 0.30, h * 0.42), new Point(w * 0.54, h * 0.42));
                    context.DrawLine(pen, new Point(w * 0.42, h * 0.30), new Point(w * 0.42, h * 0.54));
                    break;
                case ToolGlyphKind.Play:
                    DrawPolygon(context, brush, new Point(w * 0.34, h * 0.24), new Point(w * 0.34, h * 0.76), new Point(w * 0.76, h * 0.50));
                    break;
                case ToolGlyphKind.Pause:
                    context.FillRectangle(brush, new Rect(w * 0.32, h * 0.24, w * 0.12, h * 0.52));
                    context.FillRectangle(brush, new Rect(w * 0.56, h * 0.24, w * 0.12, h * 0.52));
                    break;
                case ToolGlyphKind.Slower:
                    DrawPolygon(context, brush, new Point(w * 0.62, h * 0.24), new Point(w * 0.62, h * 0.76), new Point(w * 0.34, h * 0.50));
                    break;
                case ToolGlyphKind.Faster:
                    DrawPolygon(context, brush, new Point(w * 0.34, h * 0.24), new Point(w * 0.34, h * 0.76), new Point(w * 0.62, h * 0.50));
                    break;
                case ToolGlyphKind.Night:
                    context.DrawEllipse(brush, null, new Point(w * 0.48, h * 0.48), w * 0.24, h * 0.24);
                    context.DrawEllipse(Brushes.White, null, new Point(w * 0.58, h * 0.38), w * 0.22, h * 0.22);
                    break;
            }
        }

        private static void DrawDiamond(DrawingContext context, IBrush fill, Pen outline, double w, double h)
        {
            DrawPolygon(context, fill,
                new Point(w * 0.50, h * 0.18),
                new Point(w * 0.82, h * 0.42),
                new Point(w * 0.50, h * 0.68),
                new Point(w * 0.18, h * 0.42));
            context.DrawLine(outline, new Point(w * 0.50, h * 0.18), new Point(w * 0.82, h * 0.42));
            context.DrawLine(outline, new Point(w * 0.82, h * 0.42), new Point(w * 0.50, h * 0.68));
            context.DrawLine(outline, new Point(w * 0.50, h * 0.68), new Point(w * 0.18, h * 0.42));
            context.DrawLine(outline, new Point(w * 0.18, h * 0.42), new Point(w * 0.50, h * 0.18));
        }

        private static void DrawPolygon(DrawingContext context, IBrush fill, params Point[] points)
        {
            if (points.Length == 0)
            {
                return;
            }

            StreamGeometry geometry = new();
            using (StreamGeometryContext path = geometry.Open())
            {
                path.BeginFigure(points[0], true);
                for (int i = 1; i < points.Length; i++)
                {
                    path.LineTo(points[i]);
                }

                path.EndFigure(true);
            }

            context.DrawGeometry(fill, null, geometry);
        }
    }
}
