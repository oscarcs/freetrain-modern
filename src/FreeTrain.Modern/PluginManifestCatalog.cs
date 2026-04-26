using System.Collections.ObjectModel;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FreeTrain.Modern;

public sealed record ContributionSummary(string Type, string Id, string Name);

public sealed record PictureContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Source,
    string ResolvedPath,
    string? Error)
{
    public bool IsLoadable => Error is null && File.Exists(ResolvedPath);
    public string DisplayName => string.IsNullOrWhiteSpace(PluginTitle)
        ? PluginDirectoryName
        : PluginTitle;
}

public sealed record SpriteFrame(
    string PictureId,
    string Source,
    string ResolvedPath,
    int SourceX,
    int SourceY,
    int SourceWidth,
    int SourceHeight,
    int OffsetX,
    int OffsetY,
    string? Error)
{
    public bool IsLoadable => Error is null && File.Exists(ResolvedPath);
}

public sealed record SpriteContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Type,
    string Name,
    int SizeX,
    int SizeY,
    int Height,
    IReadOnlyList<SpriteFrame> Frames,
    ModernSpriteSet2D? SpriteSet2D,
    ModernSpriteSet3D? SpriteSet3D,
    string? Error)
{
    public bool IsLoadable => Frames.Any(frame => frame.IsLoadable)
        || SpriteSet2D?.IsLoadable == true
        || SpriteSet3D?.IsLoadable == true;
    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : string.IsNullOrWhiteSpace(PluginTitle)
            ? PluginDirectoryName
            : PluginTitle;
}

public sealed record PluginManifest(
    string DirectoryName,
    string ManifestPath,
    string Title,
    string Author,
    string Version,
    string Homepage,
    IReadOnlyList<ContributionSummary> Contributions,
    IReadOnlyList<PictureContribution> Pictures,
    IReadOnlyList<SpriteContribution> Sprites,
    IReadOnlyList<LandContribution> Lands,
    IReadOnlyList<RoadContribution> Roads,
    string? Error)
{
    public bool IsLoaded => Error is null;
    public int ContributionCount => Contributions.Count;
}

public sealed class PluginManifestCatalog
{
    static PluginManifestCatalog()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public PluginManifestCatalog(string pluginDirectory)
    {
        PluginDirectory = pluginDirectory;
        Plugins = LoadPlugins(pluginDirectory);
        LoadedCount = Plugins.Count(plugin => plugin.IsLoaded);
        ErrorCount = Plugins.Count(plugin => !plugin.IsLoaded);
        ContributionTypeCounts = Plugins
            .SelectMany(plugin => plugin.Contributions)
            .GroupBy(contribution => contribution.Type)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Pictures = Plugins
            .SelectMany(plugin => plugin.Pictures)
            .OrderBy(picture => picture.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(picture => picture.Source, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        Sprites = Plugins
            .SelectMany(plugin => plugin.Sprites)
            .Where(sprite => sprite.Frames.Count > 0)
            .OrderBy(sprite => sprite.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        Lands = Plugins
            .SelectMany(plugin => plugin.Lands)
            .OrderBy(land => land.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(land => land.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        Roads = Plugins
            .SelectMany(plugin => plugin.Roads)
            .OrderBy(road => road.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(road => road.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public string PluginDirectory { get; }
    public IReadOnlyList<PluginManifest> Plugins { get; }
    public int LoadedCount { get; }
    public int ErrorCount { get; }
    public IReadOnlyDictionary<string, int> ContributionTypeCounts { get; }
    public IReadOnlyList<PictureContribution> Pictures { get; }
    public IReadOnlyList<SpriteContribution> Sprites { get; }
    public IReadOnlyList<LandContribution> Lands { get; }
    public IReadOnlyList<RoadContribution> Roads { get; }

    private static ReadOnlyCollection<PluginManifest> LoadPlugins(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            return Array.Empty<PluginManifest>().AsReadOnly();
        }

        return Directory.EnumerateFiles(pluginDirectory, "plugin.xml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ParsePlugin)
            .ToList()
            .AsReadOnly();
    }

    private static PluginManifest ParsePlugin(string manifestPath)
    {
        string directoryName = Path.GetFileName(Path.GetDirectoryName(manifestPath)) ?? "";

        try
        {
            XDocument document = LoadXml(manifestPath);
            XElement root = document.Root ?? throw new XmlException("Missing document root.");

            IReadOnlyList<ContributionSummary> contributions = root.Elements("contribution")
                .Select(element => new ContributionSummary(
                    AttributeValue(element, "type"),
                    AttributeValue(element, "id"),
                    ElementValue(element, "name")))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<PictureContribution> pictures = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "picture", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParsePictureContribution(manifestPath, directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyDictionary<string, PictureContribution> pictureLookup = pictures
                .Where(picture => !string.IsNullOrWhiteSpace(picture.Id))
                .GroupBy(picture => picture.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<SpriteContribution> sprites = root.Elements("contribution")
                .Where(element => !string.Equals(AttributeValue(element, "type"), "picture", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseSpriteContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .Where(sprite => sprite.Frames.Count > 0)
                .ToList()
                .AsReadOnly();
            IReadOnlyList<LandContribution> lands = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "land", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseLandContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<RoadContribution> roads = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "road", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseRoadContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .ToList()
                .AsReadOnly();

            return new PluginManifest(
                directoryName,
                manifestPath,
                ElementValue(root, "title"),
                ElementValue(root, "author"),
                ElementValue(root, "version"),
                ElementValue(root, "homepage"),
                contributions,
                pictures,
                sprites,
                lands,
                roads,
                null);
        }
        catch (Exception ex)
        {
            return new PluginManifest(
                directoryName,
                manifestPath,
                directoryName,
                "",
                "",
                "",
                Array.Empty<ContributionSummary>(),
                Array.Empty<PictureContribution>(),
                Array.Empty<SpriteContribution>(),
                Array.Empty<LandContribution>(),
                Array.Empty<RoadContribution>(),
                ex.Message);
        }
    }

    private static PictureContribution ParsePictureContribution(string manifestPath, string directoryName, string pluginTitle, XElement contribution)
    {
        string id = AttributeValue(contribution, "id");
        XElement? picture = contribution.Element("picture");
        string source = picture is null ? "" : AttributeValue(picture, "src");
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        string resolvedPath = ResolvePluginPath(pluginDirectory, source);
        string? error = null;

        if (picture is null)
        {
            error = "Missing <picture> element.";
        }
        else if (string.IsNullOrWhiteSpace(source))
        {
            error = "Missing picture src.";
        }
        else if (!File.Exists(resolvedPath))
        {
            error = "Image file not found.";
        }

        return new PictureContribution(directoryName, pluginTitle, id, source, resolvedPath, error);
    }

    private static SpriteContribution ParseSpriteContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        (int sizeX, int sizeY) = ParseSize(ElementValue(contribution, "size"));
        int height = int.TryParse(ElementValue(contribution, "height"), out int parsedHeight)
            ? parsedHeight
            : 0;
        IReadOnlyList<SpriteFrame> frames = contribution.Descendants("sprite")
            .Select(sprite => ParseSpriteFrame(pluginDirectory, contribution, sprite, pictures))
            .Where(frame => frame is not null)
            .Cast<SpriteFrame>()
            .ToList()
            .AsReadOnly();
        XElement? firstSprite = contribution.Elements("sprite").FirstOrDefault()
            ?? contribution.Descendants("sprite").FirstOrDefault();
        ModernSpriteSet2D? spriteSet2D = firstSprite is not null && sizeX > 0 && sizeY > 0
            ? Load2DSpriteSet(pluginDirectory, contribution, firstSprite, pictures, sizeX, sizeY, Math.Max(0, height))
            : null;
        ModernSpriteSet3D? spriteSet3D = firstSprite is not null && IsFixedSizeStructure(contribution) && sizeX > 0 && sizeY > 0 && height > 0
            ? Load3DSpriteSet(pluginDirectory, firstSprite, pictures, sizeX, sizeY, height)
            : null;
        string? error = frames.Count == 0
            ? "No sprite frames found."
            : frames.Any(frame => frame.IsLoadable)
                ? null
                : string.Join("; ", frames.Select(frame => frame.Error).Where(message => !string.IsNullOrWhiteSpace(message)).Distinct());

        return new SpriteContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            AttributeValue(contribution, "type"),
            ElementValue(contribution, "name"),
            sizeX,
            sizeY,
            height,
            frames,
            spriteSet2D,
            spriteSet3D,
            string.IsNullOrWhiteSpace(error) ? null : error);
    }

    private static LandContribution ParseLandContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        string className = OptionalAttributeValue(contribution.Element("class"), "name");
        string id = AttributeValue(contribution, "id");
        string name = ElementValue(contribution, "name");

        if (className.EndsWith(".StaticLandBuilder", StringComparison.OrdinalIgnoreCase)
            || contribution.Element("sprite") is not null)
        {
            SpriteFrame? sprite = contribution.Element("sprite") is { } spriteElement
                ? ParseSpriteFrame(pluginDirectory, contribution, spriteElement, pictures)
                : null;
            string? error = sprite is null
                ? "Missing static land sprite."
                : sprite.IsLoadable
                    ? null
                    : sprite.Error;

            return new LandContribution(
                directoryName,
                pluginTitle,
                id,
                name,
                LandContributionKind.Static,
                sprite,
                Array.Empty<string>(),
                null,
                error);
        }

        if (className.EndsWith(".RandomLandBuilder", StringComparison.OrdinalIgnoreCase))
        {
            string[] landIds = (contribution.Element("lands")?.Value ?? "")
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new LandContribution(
                directoryName,
                pluginTitle,
                id,
                name,
                LandContributionKind.Random,
                null,
                landIds,
                null,
                landIds.Length == 0 ? "Random land builder has no land IDs." : null);
        }

        if (className.EndsWith(".ForestBuilder", StringComparison.OrdinalIgnoreCase))
        {
            ForestSpriteSet? forest = ParseForestSpriteSet(pluginDirectory, contribution, pictures);
            return new LandContribution(
                directoryName,
                pluginTitle,
                id,
                name,
                LandContributionKind.Forest,
                null,
                Array.Empty<string>(),
                forest,
                forest?.IsLoadable == true ? null : "Forest builder has no loadable tree sprites.");
        }

        return new LandContribution(
            directoryName,
            pluginTitle,
            id,
            name,
            LandContributionKind.Unsupported,
            null,
            Array.Empty<string>(),
            null,
            string.IsNullOrWhiteSpace(className) ? "Land builder class not found." : $"Unsupported land builder '{className}'.");
    }

    private static RoadContribution ParseRoadContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        string className = OptionalAttributeValue(contribution.Element("class"), "name");
        RoadContributionKind kind = className.EndsWith(".StandardRoadContribution", StringComparison.OrdinalIgnoreCase)
            ? RoadContributionKind.Standard
            : className.EndsWith(".A3RoadContribution", StringComparison.OrdinalIgnoreCase)
                ? RoadContributionKind.A3
                : RoadContributionKind.Unsupported;
        RoadStyle style = ParseRoadStyle(contribution.Element("style"));
        IReadOnlyDictionary<byte, SpriteFrame> frames = kind switch
        {
            RoadContributionKind.Standard => ParseStandardRoadFrames(pluginDirectory, contribution, pictures),
            RoadContributionKind.A3 => ParseA3RoadFrames(pluginDirectory, contribution, pictures),
            _ => new Dictionary<byte, SpriteFrame>()
        };
        string? error = kind == RoadContributionKind.Unsupported
            ? string.IsNullOrWhiteSpace(className) ? "Road builder class not found." : $"Unsupported road builder '{className}'."
            : frames.Count == 0
                ? "No road sprites found."
                : frames.Values.Any(frame => frame.IsLoadable)
                    ? null
                    : string.Join("; ", frames.Values.Select(frame => frame.Error).Where(message => !string.IsNullOrWhiteSpace(message)).Distinct());

        return new RoadContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "description"),
            kind,
            style,
            frames,
            string.IsNullOrWhiteSpace(error) ? null : error);
    }

    private static RoadStyle ParseRoadStyle(XElement? style)
    {
        if (style is null)
        {
            return new RoadStyle("unknown", "none", 0);
        }

        int lanes = int.TryParse(AttributeValue(style, "lanes"), out int parsedLanes) ? parsedLanes : 0;
        return new RoadStyle(
            AttributeValue(style, "name"),
            AttributeValue(style, "sidewalk"),
            lanes);
    }

    private static IReadOnlyDictionary<byte, SpriteFrame> ParseStandardRoadFrames(
        string pluginDirectory,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        XElement? pictureElement = contribution.Element("picture");
        if (pictureElement is null)
        {
            return new Dictionary<byte, SpriteFrame>();
        }

        ResolvedSpritePicture picture = ResolvePictureElement(pluginDirectory, pictureElement, pictures);
        (int width, int height) = ParseSize(AttributeValue(pictureElement, "size"));
        width = width <= 0 ? 32 : width;
        height = height <= 0 ? 32 : height;
        int offsetY = ParseInt(AttributeValue(pictureElement, "offset"));

        Dictionary<byte, SpriteFrame> frames = new();
        for (byte mask = 1; mask <= 15; mask++)
        {
            (int x, int y) = StandardRoadLocations[mask - 1];
            frames[mask] = CreateSpriteFrame(picture, x * width, y * height, width, height, 0, offsetY);
        }

        return frames;
    }

    private static IReadOnlyDictionary<byte, SpriteFrame> ParseA3RoadFrames(
        string pluginDirectory,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        XElement? pictureElement = contribution.Element("picture");
        if (pictureElement is null)
        {
            return new Dictionary<byte, SpriteFrame>();
        }

        ResolvedSpritePicture picture = ResolvePictureElement(pluginDirectory, pictureElement, pictures);
        Dictionary<byte, SpriteFrame> baseFrames = new()
        {
            [0] = CreateSpriteFrame(picture, 0, 0, 32, 32, 0, 16),
            [1] = CreateSpriteFrame(picture, 32, 0, 32, 32, 0, 16),
            [2] = CreateSpriteFrame(picture, 64, 0, 32, 32, 0, 16)
        };

        Dictionary<byte, SpriteFrame> frames = new();
        for (byte mask = 1; mask <= 15; mask++)
        {
            int a3Index = mask is 2 or 8 or 10
                ? 0
                : mask is 1 or 4 or 5
                    ? 1
                    : 2;
            frames[mask] = baseFrames[(byte)a3Index];
        }

        return frames;
    }

    private static ForestSpriteSet? ParseForestSpriteSet(
        string pluginDirectory,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        XElement? pictureElement = contribution.Element("picture");
        if (pictureElement is null)
        {
            return null;
        }

        ResolvedSpritePicture picture = ResolvePictureElement(pluginDirectory, pictureElement, pictures);
        (int totalWidth, int height) = ParseSize(ElementValue(contribution, "size"));
        int count = int.TryParse(ElementValue(contribution, "count"), out int parsedCount) ? parsedCount : 0;
        int density = int.TryParse(ElementValue(contribution, "density"), out int parsedDensity) ? parsedDensity : 0;
        if (count <= 0 || totalWidth <= 0 || height <= 0)
        {
            return null;
        }

        int spriteWidth = totalWidth / count;
        List<SpriteFrame> treeSprites = new();
        for (int i = 0; i < count; i++)
        {
            treeSprites.Add(CreateSpriteFrame(
                picture,
                i * spriteWidth,
                0,
                spriteWidth,
                height,
                spriteWidth / 2,
                height));
        }

        SpriteFrame? ground = null;
        if (contribution.Element("ground") is { } groundElement
            && groundElement.Element("picture") is { } groundPictureElement)
        {
            ResolvedSpritePicture groundPicture = ResolvePictureElement(pluginDirectory, groundPictureElement, pictures);
            (int groundX, int groundY) = ParsePoint(AttributeValue(groundPictureElement, "origin"));
            ground = CreateSpriteFrame(groundPicture, groundX, groundY, 32, 16, 0, 0);
        }

        return new ForestSpriteSet(treeSprites.AsReadOnly(), ground, Math.Max(0, density));
    }

    private static SpriteFrame? ParseSpriteFrame(
        string pluginDirectory,
        XElement contribution,
        XElement sprite,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        XElement? picture = sprite.Element("picture");
        if (picture is null)
        {
            return null;
        }

        string source = AttributeValue(picture, "src");
        string pictureId = AttributeValue(picture, "ref");
        string resolvedPath = "";
        string? error = null;

        if (!string.IsNullOrWhiteSpace(source))
        {
            resolvedPath = ResolvePluginPath(pluginDirectory, source);
        }
        else if (!string.IsNullOrWhiteSpace(pictureId) && pictures.TryGetValue(pictureId, out PictureContribution? referencedPicture))
        {
            source = referencedPicture.Source;
            resolvedPath = referencedPicture.ResolvedPath;
            error = referencedPicture.Error;
        }
        else if (!string.IsNullOrWhiteSpace(pictureId))
        {
            error = $"Picture ref '{pictureId}' was not found.";
        }
        else
        {
            error = "Sprite picture is missing src or ref.";
        }

        if (error is null && !File.Exists(resolvedPath))
        {
            error = "Image file not found.";
        }

        (int sourceX, int sourceY) = ParsePoint(AttributeValue(sprite, "origin"));
        if (sourceX == 0 && sourceY == 0)
        {
            XElement? pattern = sprite.Element("pattern");
            if (pattern is not null)
            {
                (sourceX, sourceY) = ParsePoint(AttributeValue(pattern, "origin"));
            }
        }

        string offset = AttributeValue(sprite, "offset");
        (int offsetX, int offsetY) = string.IsNullOrWhiteSpace(offset)
            ? (0, DefaultOffsetY(contribution))
            : ParseOffset(offset);
        (int sourceWidth, int sourceHeight) = InferSpriteSize(contribution, sprite, offsetY);
        return new SpriteFrame(pictureId, source, resolvedPath, sourceX, sourceY, sourceWidth, sourceHeight, offsetX, offsetY, error);
    }

    private static ModernSpriteSet2D Load2DSpriteSet(
        string pluginDirectory,
        XElement contribution,
        XElement sprite,
        IReadOnlyDictionary<string, PictureContribution> pictures,
        int sizeX,
        int sizeY,
        int height)
    {
        ModernSpriteSet2D spriteSet = new(sizeX, sizeY);
        ResolvedSpritePicture picture = ResolveSpritePicture(pluginDirectory, sprite, pictures);
        (int originX, int originY) = ParsePoint(AttributeValue(sprite, "origin"));
        int offsetY = height;
        string offset = AttributeValue(sprite, "offset");
        if (!string.IsNullOrWhiteSpace(offset))
        {
            offsetY = ParseInt(offset);
        }

        int maxHeight = int.MaxValue;
        string maxHeightValue = AttributeValue(sprite, "height");
        if (!string.IsNullOrWhiteSpace(maxHeightValue))
        {
            maxHeight = ParseInt(maxHeightValue);
        }

        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                int sourceHeight = Math.Min(maxHeight, offsetY + 16 + (y - x) * 8);
                if (sourceHeight <= 0)
                {
                    continue;
                }

                int sourceX = (x + y) * 16 + originX;
                int sourceY = originY;
                int pieceOffsetY = offsetY + (y - x) * 8;
                spriteSet[x, y] = CreateSpriteFrame(picture, sourceX, sourceY, 32, sourceHeight, 0, pieceOffsetY);
            }
        }

        return spriteSet;
    }

    private static ModernSpriteSet3D Load3DSpriteSet(
        string pluginDirectory,
        XElement sprite,
        IReadOnlyDictionary<string, PictureContribution> pictures,
        int sizeX,
        int sizeY,
        int sizeZ)
    {
        ModernSpriteSet3D spriteSet = new(sizeX, sizeY, sizeZ);
        ResolvedSpritePicture picture = ResolveSpritePicture(pluginDirectory, sprite, pictures);
        (int originX, int originY) = ParsePoint(AttributeValue(sprite, "origin"));
        int offsetY = ((sizeZ << 1) + (sizeX - 1)) << 3;
        string offset = AttributeValue(sprite, "offset");
        if (!string.IsNullOrWhiteSpace(offset))
        {
            offsetY = ParseInt(offset);
        }

        int topZ = sizeZ - 1;
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                int voxelOriginX = (x + y) * 16 + originX;
                int voxelOriginY = originY + offsetY - 16 * (sizeZ - 1) + (y - x) * 8;
                int sourceX = voxelOriginX;
                int sourceY = voxelOriginY;
                int sourceWidth = 32;
                int sourceHeight = 16;

                if (y == 0 || x == sizeX - 1)
                {
                    sourceY -= 16;
                    sourceHeight += 16;
                    if (y == 0 && x == sizeX - 1)
                    {
                        // Full cap.
                    }
                    else if (y == 0 && sizeY > 1)
                    {
                        sourceWidth = 16;
                    }
                    else if (x == sizeX - 1 && sizeX > 1)
                    {
                        sourceX += 16;
                        sourceWidth -= 16;
                    }
                }

                if (sourceY < 0)
                {
                    sourceHeight += sourceY;
                    sourceY = 0;
                }

                if (sourceHeight > 0 && sourceWidth > 0)
                {
                    spriteSet[x, y, topZ] = CreateSpriteFrame(
                        picture,
                        sourceX,
                        sourceY,
                        sourceWidth,
                        sourceHeight,
                        voxelOriginX - sourceX,
                        voxelOriginY - sourceY);
                }
            }
        }

        if (sizeZ > 1)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    int voxelOriginX = (x + y) * 16 + originX;
                    int voxelOriginY = originY + offsetY + (y - x) * 8;
                    int sourceX = voxelOriginX;
                    int sourceY = voxelOriginY - (sizeZ - 2) * 16 - 8;
                    int sourceWidth;
                    int sourceHeight = 16 * (sizeZ - 1) + 8;

                    if (x == 0 && y == sizeY - 1)
                    {
                        sourceWidth = 32;
                    }
                    else if (x == 0)
                    {
                        sourceWidth = 16;
                    }
                    else if (y == sizeY - 1)
                    {
                        sourceWidth = 16;
                        sourceX += 16;
                    }
                    else
                    {
                        continue;
                    }

                    spriteSet[x, y, 0] = CreateSpriteFrame(
                        picture,
                        sourceX,
                        sourceY,
                        sourceWidth,
                        sourceHeight,
                        voxelOriginX - sourceX,
                        voxelOriginY - sourceY);
                }
            }
        }

        return spriteSet;
    }

    private static SpriteFrame CreateSpriteFrame(
        ResolvedSpritePicture picture,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        int offsetX,
        int offsetY)
    {
        string? error = picture.Error;
        if (error is null && !File.Exists(picture.ResolvedPath))
        {
            error = "Image file not found.";
        }

        return new SpriteFrame(
            picture.PictureId,
            picture.Source,
            picture.ResolvedPath,
            sourceX,
            sourceY,
            sourceWidth,
            sourceHeight,
            offsetX,
            offsetY,
            error);
    }

    private static ResolvedSpritePicture ResolveSpritePicture(
        string pluginDirectory,
        XElement sprite,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        XElement? picture = sprite.Element("picture");
        if (picture is null)
        {
            return new ResolvedSpritePicture("", "", "", "Missing <picture> element.");
        }

        return ResolvePictureElement(pluginDirectory, picture, pictures);
    }

    private static ResolvedSpritePicture ResolvePictureElement(
        string pluginDirectory,
        XElement picture,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string source = AttributeValue(picture, "src");
        string pictureId = AttributeValue(picture, "ref");
        string resolvedPath = "";
        string? error = null;

        if (!string.IsNullOrWhiteSpace(source))
        {
            resolvedPath = ResolvePluginPath(pluginDirectory, source);
        }
        else if (!string.IsNullOrWhiteSpace(pictureId) && pictures.TryGetValue(pictureId, out PictureContribution? referencedPicture))
        {
            source = referencedPicture.Source;
            resolvedPath = referencedPicture.ResolvedPath;
            error = referencedPicture.Error;
        }
        else if (!string.IsNullOrWhiteSpace(pictureId))
        {
            error = $"Picture ref '{pictureId}' was not found.";
        }
        else
        {
            error = "Sprite picture is missing src or ref.";
        }

        return new ResolvedSpritePicture(pictureId, source, resolvedPath, error);
    }

    private static int DefaultOffsetY(XElement contribution)
    {
        (int sizeX, int _) = ParseSize(ElementValue(contribution, "size"));
        if (IsFixedSizeStructure(contribution)
            && sizeX > 0
            && int.TryParse(ElementValue(contribution, "height"), out int height)
            && height > 0)
        {
            return ((height << 1) + (sizeX - 1)) << 3;
        }

        return 0;
    }

    private static (int Width, int Height) InferSpriteSize(XElement contribution, XElement sprite, int offsetY)
    {
        (int sourceWidth, int sourceHeight) = ParseSize(AttributeValue(sprite, "size"));
        if (sourceWidth > 0 && sourceHeight > 0)
        {
            return (sourceWidth, sourceHeight);
        }

        (int sizeX, int sizeY) = ParseSize(ElementValue(contribution, "size"));
        string type = AttributeValue(contribution, "type");
        if (IsFixedSizeStructure(contribution) && sizeX > 0 && sizeY > 0)
        {
            return ((sizeX + sizeY) * 16, Math.Max(16, offsetY + 16));
        }

        if (type.Contains("land", StringComparison.OrdinalIgnoreCase))
        {
            int maxHeight = int.TryParse(AttributeValue(sprite, "height"), out int parsedHeight)
                ? parsedHeight
                : int.MaxValue;
            return (32, Math.Min(maxHeight, offsetY + 16));
        }

        return (32, 32);
    }

    private static bool IsFixedSizeStructure(XElement contribution)
    {
        string type = AttributeValue(contribution, "type");
        return type.Contains("structure", StringComparison.OrdinalIgnoreCase)
            || type.Contains("station", StringComparison.OrdinalIgnoreCase)
            || type.Contains("commercial", StringComparison.OrdinalIgnoreCase);
    }

    private static (int X, int Y) ParsePoint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (0, 0);
        }

        string[] parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        int x = parts.Length > 0 && int.TryParse(parts[0], out int parsedX) ? parsedX : 0;
        int y = parts.Length > 1 && int.TryParse(parts[1], out int parsedY) ? parsedY : 0;
        return (x, y);
    }

    private static (int X, int Y) ParseOffset(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (0, 0);
        }

        string[] parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            int y = int.TryParse(parts[0], out int parsedY) ? parsedY : 0;
            return (0, y);
        }

        int x = int.TryParse(parts[0], out int parsedX) ? parsedX : 0;
        int parsedY2 = int.TryParse(parts[1], out int y2) ? y2 : 0;
        return (x, parsedY2);
    }

    private static int ParseInt(string value)
    {
        string first = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "";
        return int.TryParse(first, out int parsed) ? parsed : 0;
    }

    private static (int Width, int Height) ParseSize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (0, 0);
        }

        string[] parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        int width = parts.Length > 0 && int.TryParse(parts[0], out int parsedWidth) ? parsedWidth : 0;
        int height = parts.Length > 1 && int.TryParse(parts[1], out int parsedHeight) ? parsedHeight : 0;
        return (width, height);
    }

    private static string ResolvePluginPath(string pluginDirectory, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "";
        }

        string normalized = source.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(pluginDirectory, normalized));
    }

    private static XDocument LoadXml(string path)
    {
        XmlReaderSettings settings = new()
        {
            CheckCharacters = false,
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            IgnoreComments = true
        };

        using FileStream stream = File.OpenRead(path);
        using XmlReader reader = XmlReader.Create(stream, settings, path);
        return XDocument.Load(reader, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
    }

    private static string ElementValue(XElement element, string name)
    {
        return element.Element(name)?.Value.Trim() ?? "";
    }

    private static string AttributeValue(XElement element, string name)
    {
        return element.Attribute(name)?.Value.Trim() ?? "";
    }

    private static string OptionalAttributeValue(XElement? element, string name)
    {
        return element?.Attribute(name)?.Value.Trim() ?? "";
    }

    private static readonly (int X, int Y)[] StandardRoadLocations =
    {
        (2, 4),
        (1, 4),
        (1, 1),
        (1, 3),
        (0, 1),
        (2, 0),
        (0, 2),
        (2, 3),
        (2, 1),
        (0, 0),
        (0, 3),
        (1, 0),
        (2, 2),
        (1, 2),
        (0, 4)
    };

    private readonly record struct ResolvedSpritePicture(string PictureId, string Source, string ResolvedPath, string? Error);
}
