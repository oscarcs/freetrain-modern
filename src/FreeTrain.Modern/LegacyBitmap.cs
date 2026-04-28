using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FreeTrain.Modern;

public static class LegacyBitmap
{
    public static Bitmap LoadWithColorKey(string path, bool includeTopLeftColor = true, bool includeMagenta = true)
    {
        return LoadWithColorKey(path, Array.Empty<ColorMapEntry>(), includeTopLeftColor, includeMagenta);
    }

    public static Bitmap LoadWithColorKey(
        string path,
        IReadOnlyList<ColorMapEntry> colorMaps,
        int colorLibraryVariantIndex,
        bool includeTopLeftColor = true,
        bool includeMagenta = true)
    {
        using Bitmap source = new(path);
        WriteableBitmap target = new(source.PixelSize, source.Dpi, PixelFormats.Bgra8888, AlphaFormat.Premul);

        using (ILockedFramebuffer framebuffer = target.Lock())
        {
            source.CopyPixels(framebuffer);
            ApplyColorKey(framebuffer, includeTopLeftColor, includeMagenta);
            ApplyColorMaps(framebuffer, colorMaps, colorLibraryVariantIndex);
        }

        return target;
    }

    public static Bitmap LoadWithColorKey(
        string path,
        IReadOnlyList<ColorMapEntry> colorMaps,
        bool includeTopLeftColor = true,
        bool includeMagenta = true)
    {
        return LoadWithColorKey(path, colorMaps, 0, includeTopLeftColor, includeMagenta);
    }

    public static int GetColorLibrarySize(string colorLibraryId)
    {
        return DefaultColorLibraryColors.TryGetValue(colorLibraryId.Trim(), out (byte R, byte G, byte B)[]? colors)
            ? colors.Length
            : 0;
    }

    public static bool TryResolveColor(string text, int colorLibraryVariantIndex, out Color color)
    {
        color = Colors.Transparent;
        if (DefaultColorLibraryColors.TryGetValue(text.Trim(), out (byte R, byte G, byte B)[]? colors)
            && colors.Length > 0)
        {
            (byte R, byte G, byte B) resolved = colors[(int)((uint)colorLibraryVariantIndex % colors.Length)];
            color = Color.FromRgb(resolved.R, resolved.G, resolved.B);
            return true;
        }

        if (text.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3
            || !byte.TryParse(parts[0], out byte r)
            || !byte.TryParse(parts[1], out byte g)
            || !byte.TryParse(parts[2], out byte b))
        {
            return false;
        }

        color = Color.FromRgb(r, g, b);
        return true;
    }

    public static IReadOnlyList<Color> SampleMappedColorsWithColorKey(
        string path,
        IReadOnlyList<ColorMapEntry> colorMaps,
        int colorLibraryVariantIndex,
        PixelRect sourceRect,
        int maxColors = 4,
        bool includeTopLeftColor = true,
        bool includeMagenta = true)
    {
        if (colorMaps.Count == 0 || maxColors <= 0)
        {
            return Array.Empty<Color>();
        }

        ColorTransform[] transforms = CreateColorTransforms(colorMaps, colorLibraryVariantIndex);
        if (transforms.Length == 0)
        {
            return Array.Empty<Color>();
        }

        using Bitmap source = new(path);
        WriteableBitmap target = new(source.PixelSize, source.Dpi, PixelFormats.Bgra8888, AlphaFormat.Premul);
        using ILockedFramebuffer framebuffer = target.Lock();
        source.CopyPixels(framebuffer);
        ApplyColorKey(framebuffer, includeTopLeftColor, includeMagenta);

        Dictionary<Color, int> counts = new();
        int left = Math.Clamp(sourceRect.X, 0, Math.Max(0, framebuffer.Size.Width - 1));
        int top = Math.Clamp(sourceRect.Y, 0, Math.Max(0, framebuffer.Size.Height - 1));
        int right = Math.Clamp(sourceRect.Right, left, framebuffer.Size.Width);
        int bottom = Math.Clamp(sourceRect.Bottom, top, framebuffer.Size.Height);
        byte[] row = new byte[framebuffer.RowBytes];

        for (int y = top; y < bottom; y++)
        {
            IntPtr rowAddress = IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes);
            Marshal.Copy(rowAddress, row, 0, framebuffer.RowBytes);

            for (int x = left; x < right; x++)
            {
                int index = x * 4;
                if (row[index + 3] == 0)
                {
                    continue;
                }

                byte b = row[index];
                byte g = row[index + 1];
                byte r = row[index + 2];
                foreach (ColorTransform transform in transforms)
                {
                    if (!transform.TryMap(r, g, b, out byte mappedR, out byte mappedG, out byte mappedB))
                    {
                        continue;
                    }

                    Color color = QuantizeColor(mappedR, mappedG, mappedB);
                    counts[color] = counts.GetValueOrDefault(color) + 1;
                    break;
                }
            }
        }

        return counts
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .Take(maxColors)
            .ToList()
            .AsReadOnly();
    }

    public static IReadOnlyList<Color> SampleDominantColorsWithColorKey(
        string path,
        PixelRect sourceRect,
        IReadOnlyList<ColorMapEntry>? colorMaps = null,
        int colorLibraryVariantIndex = 0,
        int maxColors = 4,
        bool includeTopLeftColor = true,
        bool includeMagenta = true)
    {
        if (maxColors <= 0)
        {
            return Array.Empty<Color>();
        }

        using Bitmap source = new(path);
        WriteableBitmap target = new(source.PixelSize, source.Dpi, PixelFormats.Bgra8888, AlphaFormat.Premul);
        using ILockedFramebuffer framebuffer = target.Lock();
        source.CopyPixels(framebuffer);
        ApplyColorKey(framebuffer, includeTopLeftColor, includeMagenta);
        if (colorMaps is { Count: > 0 })
        {
            ApplyColorMaps(framebuffer, colorMaps, colorLibraryVariantIndex);
        }

        Dictionary<Color, int> informativeCounts = new();
        Dictionary<Color, int> fallbackCounts = new();
        int left = Math.Clamp(sourceRect.X, 0, Math.Max(0, framebuffer.Size.Width - 1));
        int top = Math.Clamp(sourceRect.Y, 0, Math.Max(0, framebuffer.Size.Height - 1));
        int right = Math.Clamp(sourceRect.Right, left, framebuffer.Size.Width);
        int bottom = Math.Clamp(sourceRect.Bottom, top, framebuffer.Size.Height);
        byte[] row = new byte[framebuffer.RowBytes];

        for (int y = top; y < bottom; y++)
        {
            IntPtr rowAddress = IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes);
            Marshal.Copy(rowAddress, row, 0, framebuffer.RowBytes);

            for (int x = left; x < right; x++)
            {
                int index = x * 4;
                if (row[index + 3] == 0)
                {
                    continue;
                }

                Color color = QuantizeColor(row[index + 2], row[index + 1], row[index]);
                fallbackCounts[color] = fallbackCounts.GetValueOrDefault(color) + 1;
                if (IsInformativeSwatchColor(color))
                {
                    informativeCounts[color] = informativeCounts.GetValueOrDefault(color) + 1;
                }
            }
        }

        Dictionary<Color, int> counts = informativeCounts.Count > 0 ? informativeCounts : fallbackCounts;
        return counts
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .Take(maxColors)
            .ToList()
            .AsReadOnly();
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

    private static void ApplyColorMaps(ILockedFramebuffer framebuffer, IReadOnlyList<ColorMapEntry> colorMaps, int colorLibraryVariantIndex)
    {
        if (colorMaps.Count == 0)
        {
            return;
        }

        ColorTransform[] transforms = CreateColorTransforms(colorMaps, colorLibraryVariantIndex);
        if (transforms.Length == 0)
        {
            return;
        }

        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        int rowBytes = framebuffer.RowBytes;
        byte[] row = new byte[rowBytes];

        for (int y = 0; y < height; y++)
        {
            IntPtr rowAddress = IntPtr.Add(framebuffer.Address, y * rowBytes);
            Marshal.Copy(rowAddress, row, 0, rowBytes);

            for (int x = 0; x < width; x++)
            {
                int index = x * 4;
                if (row[index + 3] == 0)
                {
                    continue;
                }

                byte b = row[index];
                byte g = row[index + 1];
                byte r = row[index + 2];
                foreach (ColorTransform transform in transforms)
                {
                    if (!transform.TryMap(r, g, b, out byte mappedR, out byte mappedG, out byte mappedB))
                    {
                        continue;
                    }

                    row[index] = mappedB;
                    row[index + 1] = mappedG;
                    row[index + 2] = mappedR;
                    break;
                }
            }

            Marshal.Copy(row, 0, rowAddress, rowBytes);
        }
    }

    private static ColorTransform[] CreateColorTransforms(IReadOnlyList<ColorMapEntry> colorMaps, int colorLibraryVariantIndex)
    {
        return colorMaps
            .Select(map => ColorTransform.TryCreate(map, colorLibraryVariantIndex))
            .Where(transform => transform is not null)
            .Cast<ColorTransform>()
            .ToArray();
    }

    private static Color QuantizeColor(byte r, byte g, byte b)
    {
        return Color.FromRgb(
            (byte)(r / 16 * 16),
            (byte)(g / 16 * 16),
            (byte)(b / 16 * 16));
    }

    private static bool IsInformativeSwatchColor(Color color)
    {
        byte max = Math.Max(color.R, Math.Max(color.G, color.B));
        byte min = Math.Min(color.R, Math.Min(color.G, color.B));
        return max >= 48 && max - min >= 24;
    }

    private sealed class ColorTransform
    {
        private ColorTransform(int? fromR, int? fromG, int? fromB, byte toR, byte toG, byte toB, int? hueChannel)
        {
            FromR = fromR;
            FromG = fromG;
            FromB = fromB;
            ToR = toR;
            ToG = toG;
            ToB = toB;
            HueChannel = hueChannel;
        }

        private int? FromR { get; }
        private int? FromG { get; }
        private int? FromB { get; }
        private byte ToR { get; }
        private byte ToG { get; }
        private byte ToB { get; }
        private int? HueChannel { get; }

        public static ColorTransform? TryCreate(ColorMapEntry entry, int colorLibraryVariantIndex)
        {
            if (!TryParseColorPattern(entry.From, out int? fromR, out int? fromG, out int? fromB)
                || !TryParseColor(entry.To, colorLibraryVariantIndex, out byte toR, out byte toG, out byte toB))
            {
                return null;
            }

            int? hueChannel = null;
            if (fromR is null && fromG == 0 && fromB == 0)
            {
                hueChannel = 0;
            }
            else if (fromG is null && fromR == 0 && fromB == 0)
            {
                hueChannel = 1;
            }
            else if (fromB is null && fromR == 0 && fromG == 0)
            {
                hueChannel = 2;
            }

            return new ColorTransform(fromR, fromG, fromB, toR, toG, toB, hueChannel);
        }

        public bool TryMap(byte r, byte g, byte b, out byte mappedR, out byte mappedG, out byte mappedB)
        {
            mappedR = r;
            mappedG = g;
            mappedB = b;

            if (HueChannel is { } hueChannel)
            {
                byte strength = hueChannel switch
                {
                    0 => r,
                    1 => g,
                    _ => b
                };
                if (strength == 0 || !MatchesChannel(FromR, r) || !MatchesChannel(FromG, g) || !MatchesChannel(FromB, b))
                {
                    return false;
                }

                mappedR = Scale(ToR, strength);
                mappedG = Scale(ToG, strength);
                mappedB = Scale(ToB, strength);
                return true;
            }

            if (!MatchesChannel(FromR, r) || !MatchesChannel(FromG, g) || !MatchesChannel(FromB, b))
            {
                return false;
            }

            mappedR = ToR;
            mappedG = ToG;
            mappedB = ToB;
            return true;
        }

        private static bool MatchesChannel(int? expected, byte actual)
        {
            return expected is null || expected == actual;
        }

        private static byte Scale(byte channel, byte strength)
        {
            return (byte)Math.Clamp((channel * strength + 127) / 255, 0, 255);
        }

        private static bool TryParseColorPattern(string text, out int? r, out int? g, out int? b)
        {
            r = null;
            g = null;
            b = null;
            string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            return TryParsePatternChannel(parts[0], out r)
                && TryParsePatternChannel(parts[1], out g)
                && TryParsePatternChannel(parts[2], out b);
        }

        private static bool TryParsePatternChannel(string text, out int? value)
        {
            value = null;
            if (text == "*")
            {
                return true;
            }

            if (!int.TryParse(text, out int parsed))
            {
                return false;
            }

            value = Math.Clamp(parsed, 0, 255);
            return true;
        }

        private static bool TryParseColor(string text, int colorLibraryVariantIndex, out byte r, out byte g, out byte b)
        {
            r = 0;
            g = 0;
            b = 0;
            if (TryResolveColorLibrary(text, colorLibraryVariantIndex, out r, out g, out b))
            {
                return true;
            }

            if (text.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 3
                || !byte.TryParse(parts[0], out r)
                || !byte.TryParse(parts[1], out g)
                || !byte.TryParse(parts[2], out b))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveColorLibrary(string text, int colorLibraryVariantIndex, out byte r, out byte g, out byte b)
        {
            r = 0;
            g = 0;
            b = 0;
            if (!DefaultColorLibraryColors.TryGetValue(text.Trim(), out (byte R, byte G, byte B)[]? colors)
                || colors.Length == 0)
            {
                return false;
            }

            (byte R, byte G, byte B) color = colors[(int)((uint)colorLibraryVariantIndex % colors.Length)];
            r = color.R;
            g = color.G;
            b = color.B;
            return true;
        }
    }

    private static readonly IReadOnlyDictionary<string, (byte R, byte G, byte B)[]> DefaultColorLibraryColors =
        new Dictionary<string, (byte R, byte G, byte B)[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["{COLORLIB-RAINBOW}"] = [(230, 0, 18), (235, 97, 0), (245, 232, 0), (143, 195, 31), (0, 153, 68), (0, 160, 233), (40, 73, 238), (160, 37, 201)],
            ["{COLORLIB-STONES}"] = [(149, 63, 55), (129, 103, 103), (210, 167, 117), (231, 217, 170), (210, 210, 210), (129, 144, 136), (149, 149, 167), (80, 90, 106)],
            ["{COLORLIB-WOODS}"] = [(207, 136, 34), (241, 190, 109), (197, 162, 144), (95, 67, 31), (138, 101, 48), (190, 157, 38), (218, 206, 138), (98, 98, 98)],
            ["{COLORLIB-METALS}"] = [(142, 33, 33), (183, 131, 131), (142, 93, 33), (185, 175, 85), (134, 126, 36), (152, 171, 143), (105, 140, 84), (143, 174, 193), (109, 119, 154), (220, 220, 220), (153, 153, 153), (110, 110, 110)],
            ["{COLORLIB-BRICKS}"] = [(221, 99, 93), (154, 31, 11), (191, 112, 116), (210, 108, 50), (232, 193, 144), (192, 123, 40), (195, 178, 135), (150, 144, 110)],
            ["{COLORLIB-DIRTS}"] = [(153, 108, 51), (176, 136, 80), (175, 151, 128), (220, 200, 180), (170, 164, 96), (210, 210, 210), (150, 157, 141), (84, 73, 67)],
            ["{COLORLIB-PASTEL}"] = [(230, 175, 198), (225, 170, 139), (225, 222, 147), (200, 220, 162), (172, 222, 177), (190, 220, 215), (191, 202, 232), (217, 187, 232)],
            ["{COLORLIB-COLPLATE}"] = [(164, 0, 91), (248, 110, 158), (188, 12, 24), (90, 80, 33), (35, 97, 30), (154, 189, 184), (113, 223, 185), (84, 175, 210), (92, 114, 127), (61, 83, 156), (210, 210, 210), (98, 98, 98)],
            ["{COLORLIB-ROOF}"] = [(160, 160, 160), (84, 84, 84), (108, 51, 65), (168, 66, 0), (200, 11, 26), (232, 137, 36), (135, 124, 96), (138, 182, 44), (61, 133, 59), (113, 223, 185), (68, 103, 175), (142, 84, 167)],
            ["{COLORLIB-HIGHLIGHT}"] = [(0, 0, 0)]
        };
}
