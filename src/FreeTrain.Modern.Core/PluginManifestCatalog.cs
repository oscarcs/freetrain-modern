using System.Collections.ObjectModel;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FreeTrain.Modern;

public sealed record ContributionSummary(string Type, string Id, string Name);

public sealed record PluginClassReference(
    string Name,
    string Codebase);

public sealed record ContributionFactoryDefinition(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    PluginClassReference FactoryClass,
    PluginClassReference Implementation);

public sealed record MenuContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    PluginClassReference Class);

public sealed record DockingContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string MenuName,
    string MenuLocation,
    int? MenuPosition,
    bool AllowsMultiple,
    PluginClassReference Class);

public sealed record AccountGenreContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name);

public sealed record SpecialRailContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string Description,
    ModernSpecialRailKind Kind,
    PluginClassReference Class);

public sealed record SpecialStructureContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string Description,
    PluginClassReference Class);

public sealed record TrainControllerContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string Description,
    PluginClassReference Class);

public sealed record NewGameContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string Author,
    string Description,
    PluginClassReference Class);

public sealed record SpriteFactoryContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    PluginClassReference Class);

public sealed record SpriteLoaderContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    PluginClassReference Class);

public sealed record ColorLibraryContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    IReadOnlyList<string> Colors);

public sealed record ColorMapTrainPictureContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string Author,
    SpriteFrame? Frame,
    string? Error)
{
    public bool IsLoadable => Frame?.IsLoadable == true;
}

public sealed record TrainDepartureBellContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string SoundSource,
    string ResolvedPath,
    string? Error)
{
    public bool IsLoadable => Error is null && File.Exists(ResolvedPath);
}

public sealed record RailSignalContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    string Side,
    SpriteFrame? Frame,
    string? Error)
{
    public bool IsLoadable => Frame?.IsLoadable == true;
}

public sealed record DummyCarVariation(IReadOnlyList<ColorMapEntry> ColorMaps);

public sealed record DummyCarContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    SpriteFrame? EastWestFrame,
    SpriteFrame? NorthSouthFrame,
    IReadOnlyList<DummyCarVariation> Variations,
    string? Error)
{
    public bool IsLoadable => EastWestFrame?.IsLoadable == true || NorthSouthFrame?.IsLoadable == true;
}

public sealed record HalfVoxelPattern(
    string Direction,
    string Side,
    SpriteFrame Frame,
    IReadOnlyList<SpriteFrame> Highlights);

public sealed record HalfVoxelStructureContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Group,
    string Subgroup,
    string Name,
    int Price,
    int Height,
    int PopulationBase,
    string? ColorLibraryId,
    IReadOnlyList<HalfVoxelPattern> Patterns,
    string? Error)
{
    public bool IsLoadable => Patterns.Any(pattern => pattern.Frame.IsLoadable);
    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : !string.IsNullOrWhiteSpace(Subgroup)
            ? Subgroup
            : !string.IsNullOrWhiteSpace(Group)
                ? Group
                : string.IsNullOrWhiteSpace(PluginTitle)
                    ? PluginDirectoryName
                    : PluginTitle;
}

public sealed record ColorMapEntry(string From, string To);

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

public enum SpriteContributionPlacementKind
{
    Generic,
    Structure,
    RailStationary,
    RoadAccessory,
    ElectricPole,
    VariableHeightBuilding
}

public sealed record SpriteContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Type,
    string Group,
    string Subgroup,
    string Name,
    string Description,
    int Price,
    int SizeX,
    int SizeY,
    int Height,
    int MinHeight,
    int MaxHeight,
    int PopulationBase,
    bool ComputerCanBuild,
    SpriteContributionPlacementKind PlacementKind,
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
        : !string.IsNullOrWhiteSpace(Subgroup)
            ? Subgroup
            : !string.IsNullOrWhiteSpace(Group)
                ? Group
                : string.IsNullOrWhiteSpace(PluginTitle)
                    ? PluginDirectoryName
                    : PluginTitle;
    public bool IsBuildableMapObject => PlacementKind is SpriteContributionPlacementKind.Structure
        or SpriteContributionPlacementKind.RailStationary
        or SpriteContributionPlacementKind.RoadAccessory
        or SpriteContributionPlacementKind.ElectricPole
        or SpriteContributionPlacementKind.VariableHeightBuilding;
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
    IReadOnlyList<SpriteContribution> Structures,
    IReadOnlyList<SpriteContribution> RailStationaries,
    IReadOnlyList<SpriteContribution> RoadAccessories,
    IReadOnlyList<SpriteContribution> ElectricPoles,
    IReadOnlyList<LandContribution> Lands,
    IReadOnlyList<RoadContribution> Roads,
    IReadOnlyList<StationContribution> Stations,
    IReadOnlyList<TrainCarContribution> TrainCars,
    IReadOnlyList<TrainContribution> Trains,
    IReadOnlyList<ContributionFactoryDefinition> ContributionFactories,
    IReadOnlyList<MenuContribution> Menus,
    IReadOnlyList<DockingContribution> DockingContents,
    IReadOnlyList<AccountGenreContribution> AccountGenres,
    IReadOnlyList<SpecialRailContribution> SpecialRails,
    IReadOnlyList<SpecialStructureContribution> SpecialStructures,
    IReadOnlyList<TrainControllerContribution> TrainControllers,
    IReadOnlyList<NewGameContribution> NewGames,
    IReadOnlyList<SpriteFactoryContribution> SpriteFactories,
    IReadOnlyList<SpriteLoaderContribution> SpriteLoaders,
    IReadOnlyList<ColorLibraryContribution> ColorLibraries,
    IReadOnlyList<ColorMapTrainPictureContribution> ColorMapTrainPictures,
    IReadOnlyList<TrainDepartureBellContribution> TrainDepartureBells,
    IReadOnlyList<RailSignalContribution> RailSignals,
    IReadOnlyList<DummyCarContribution> DummyCars,
    IReadOnlyList<HalfVoxelStructureContribution> HalfVoxelStructures,
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

    public PluginManifestCatalog(string pluginDirectory, string? language = PluginLocalizationCatalog.DefaultLanguage)
    {
        PluginDirectory = pluginDirectory;
        Localization = PluginLocalizationCatalog.Load(pluginDirectory, language);
        Plugins = LoadPlugins(pluginDirectory, Localization);
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
        Structures = Sprites
            .Where(sprite => sprite.PlacementKind is SpriteContributionPlacementKind.Structure or SpriteContributionPlacementKind.VariableHeightBuilding)
            .OrderBy(sprite => sprite.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        RailStationaries = Sprites
            .Where(sprite => sprite.PlacementKind == SpriteContributionPlacementKind.RailStationary)
            .OrderBy(sprite => sprite.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        RoadAccessories = Sprites
            .Where(sprite => sprite.PlacementKind == SpriteContributionPlacementKind.RoadAccessory)
            .OrderBy(sprite => sprite.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        ElectricPoles = Sprites
            .Where(sprite => sprite.PlacementKind == SpriteContributionPlacementKind.ElectricPole)
            .OrderBy(sprite => sprite.Group, StringComparer.OrdinalIgnoreCase)
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
        Stations = Plugins
            .SelectMany(plugin => plugin.Stations)
            .OrderBy(station => station.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(station => station.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        TrainCars = Plugins
            .SelectMany(plugin => plugin.TrainCars)
            .OrderBy(car => car.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(car => car.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        Trains = Plugins
            .SelectMany(plugin => plugin.Trains)
            .OrderBy(train => train.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(train => train.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        ContributionFactories = Plugins
            .SelectMany(plugin => plugin.ContributionFactories)
            .OrderBy(factory => factory.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(factory => factory.Id, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        Menus = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.Menus), menu => menu.PluginDirectoryName, menu => menu.Name);
        DockingContents = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.DockingContents), docking => docking.PluginDirectoryName, docking => docking.Name);
        AccountGenres = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.AccountGenres), genre => genre.PluginDirectoryName, genre => genre.Name);
        SpecialRails = Plugins
            .SelectMany(plugin => plugin.SpecialRails)
            .OrderBy(rail => rail.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rail => rail.Class.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        SpecialStructures = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.SpecialStructures), structure => structure.PluginDirectoryName, structure => structure.Name);
        TrainControllers = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.TrainControllers), controller => controller.PluginDirectoryName, controller => controller.Name);
        NewGames = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.NewGames), newGame => newGame.PluginDirectoryName, newGame => newGame.Name);
        SpriteFactories = Plugins
            .SelectMany(plugin => plugin.SpriteFactories)
            .OrderBy(factory => factory.Id, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        SpriteLoaders = Plugins
            .SelectMany(plugin => plugin.SpriteLoaders)
            .OrderBy(loader => loader.Id, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        ColorLibraries = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.ColorLibraries), library => library.PluginDirectoryName, library => library.Name);
        ColorMapTrainPictures = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.ColorMapTrainPictures), picture => picture.PluginDirectoryName, picture => picture.Name);
        TrainDepartureBells = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.TrainDepartureBells), bell => bell.PluginDirectoryName, bell => bell.Name);
        RailSignals = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.RailSignals), signal => signal.PluginDirectoryName, signal => signal.Name);
        DummyCars = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.DummyCars), car => car.PluginDirectoryName, car => car.Name);
        HalfVoxelStructures = SortByPluginAndName(Plugins.SelectMany(plugin => plugin.HalfVoxelStructures), structure => structure.PluginDirectoryName, structure => structure.DisplayName);
    }

    public string PluginDirectory { get; }
    public PluginLocalizationCatalog Localization { get; }
    public IReadOnlyList<PluginManifest> Plugins { get; }
    public int LoadedCount { get; }
    public int ErrorCount { get; }
    public IReadOnlyDictionary<string, int> ContributionTypeCounts { get; }
    public IReadOnlyList<PictureContribution> Pictures { get; }
    public IReadOnlyList<SpriteContribution> Sprites { get; }
    public IReadOnlyList<SpriteContribution> Structures { get; }
    public IReadOnlyList<SpriteContribution> RailStationaries { get; }
    public IReadOnlyList<SpriteContribution> RoadAccessories { get; }
    public IReadOnlyList<SpriteContribution> ElectricPoles { get; }
    public IReadOnlyList<LandContribution> Lands { get; }
    public IReadOnlyList<RoadContribution> Roads { get; }
    public IReadOnlyList<StationContribution> Stations { get; }
    public IReadOnlyList<TrainCarContribution> TrainCars { get; }
    public IReadOnlyList<TrainContribution> Trains { get; }
    public IReadOnlyList<ContributionFactoryDefinition> ContributionFactories { get; }
    public IReadOnlyList<MenuContribution> Menus { get; }
    public IReadOnlyList<DockingContribution> DockingContents { get; }
    public IReadOnlyList<AccountGenreContribution> AccountGenres { get; }
    public IReadOnlyList<SpecialRailContribution> SpecialRails { get; }
    public IReadOnlyList<SpecialStructureContribution> SpecialStructures { get; }
    public IReadOnlyList<TrainControllerContribution> TrainControllers { get; }
    public IReadOnlyList<NewGameContribution> NewGames { get; }
    public IReadOnlyList<SpriteFactoryContribution> SpriteFactories { get; }
    public IReadOnlyList<SpriteLoaderContribution> SpriteLoaders { get; }
    public IReadOnlyList<ColorLibraryContribution> ColorLibraries { get; }
    public IReadOnlyList<ColorMapTrainPictureContribution> ColorMapTrainPictures { get; }
    public IReadOnlyList<TrainDepartureBellContribution> TrainDepartureBells { get; }
    public IReadOnlyList<RailSignalContribution> RailSignals { get; }
    public IReadOnlyList<DummyCarContribution> DummyCars { get; }
    public IReadOnlyList<HalfVoxelStructureContribution> HalfVoxelStructures { get; }

    private static ReadOnlyCollection<PluginManifest> LoadPlugins(string pluginDirectory, PluginLocalizationCatalog localization)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            return Array.Empty<PluginManifest>().AsReadOnly();
        }

        return Directory.EnumerateFiles(pluginDirectory, "plugin.xml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => ParsePlugin(path, localization))
            .ToList()
            .AsReadOnly();
    }

    private static PluginManifest ParsePlugin(string manifestPath, PluginLocalizationCatalog localization)
    {
        string directoryName = Path.GetFileName(Path.GetDirectoryName(manifestPath)) ?? "";

        try
        {
            XDocument document = LoadXml(manifestPath);
            XElement root = document.Root ?? throw new XmlException("Missing document root.");
            localization.ApplyToManifest(directoryName, root);

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
            IReadOnlyList<SpriteContribution> baseSprites = root.Elements("contribution")
                .Where(element => !string.Equals(AttributeValue(element, "type"), "picture", StringComparison.OrdinalIgnoreCase)
                    && !IsPromotedSpriteContributionType(AttributeValue(element, "type")))
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
            IReadOnlyList<StationContribution> stations = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "station", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseStationContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<TrainCarContribution> trainCars = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "trainCar", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseTrainCarContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<TrainContribution> trains = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "train", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseTrainContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<ContributionFactoryDefinition> contributionFactories = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "contribution", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseContributionFactory(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<MenuContribution> menus = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "menu", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseMenuContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<DockingContribution> dockingContents = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "dockingContent", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseDockingContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<AccountGenreContribution> accountGenres = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "accountGenre", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseAccountGenreContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<SpecialRailContribution> specialRails = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "specialRail", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseSpecialRailContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<SpecialStructureContribution> specialStructures = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "specialStructure", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseSpecialStructureContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<TrainControllerContribution> trainControllers = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "trainController", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseTrainControllerContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<NewGameContribution> newGames = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "newGame", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseNewGameContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<SpriteFactoryContribution> spriteFactories = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "spriteFactory", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseSpriteFactoryContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<SpriteLoaderContribution> spriteLoaders = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "spriteLoader", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseSpriteLoaderContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<ColorLibraryContribution> colorLibraries = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "ColorLibrary", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseColorLibraryContribution(directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<ColorMapTrainPictureContribution> colorMapTrainPictures = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "colorMapTrainPicture", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseColorMapTrainPictureContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<TrainDepartureBellContribution> trainDepartureBells = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "trainDepartureBell", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseTrainDepartureBellContribution(manifestPath, directoryName, ElementValue(root, "title"), element))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<RailSignalContribution> railSignals = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "railSignal", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseRailSignalContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<DummyCarContribution> dummyCars = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "DummyCar", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseDummyCarContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<HalfVoxelStructureContribution> halfVoxelStructures = root.Elements("contribution")
                .Where(element => string.Equals(AttributeValue(element, "type"), "HalfVoxelStructure", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseHalfVoxelStructureContribution(manifestPath, directoryName, ElementValue(root, "title"), element, pictureLookup))
                .ToList()
                .AsReadOnly();
            IReadOnlyList<SpriteContribution> sprites = baseSprites
                .Concat(railSignals.Select(ToRailSignalSpriteContribution))
                .Concat(dummyCars.Select(ToDummyCarSpriteContribution))
                .Concat(halfVoxelStructures.Select(ToHalfVoxelSpriteContribution))
                .Where(sprite => sprite.Frames.Count > 0)
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
                sprites.Where(sprite => sprite.PlacementKind is SpriteContributionPlacementKind.Structure or SpriteContributionPlacementKind.VariableHeightBuilding).ToList().AsReadOnly(),
                sprites.Where(sprite => sprite.PlacementKind == SpriteContributionPlacementKind.RailStationary).ToList().AsReadOnly(),
                sprites.Where(sprite => sprite.PlacementKind == SpriteContributionPlacementKind.RoadAccessory).ToList().AsReadOnly(),
                sprites.Where(sprite => sprite.PlacementKind == SpriteContributionPlacementKind.ElectricPole).ToList().AsReadOnly(),
                lands,
                roads,
                stations,
                trainCars,
                trains,
                contributionFactories,
                menus,
                dockingContents,
                accountGenres,
                specialRails,
                specialStructures,
                trainControllers,
                newGames,
                spriteFactories,
                spriteLoaders,
                colorLibraries,
                colorMapTrainPictures,
                trainDepartureBells,
                railSignals,
                dummyCars,
                halfVoxelStructures,
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
                Array.Empty<SpriteContribution>(),
                Array.Empty<SpriteContribution>(),
                Array.Empty<SpriteContribution>(),
                Array.Empty<SpriteContribution>(),
                Array.Empty<LandContribution>(),
                Array.Empty<RoadContribution>(),
                Array.Empty<StationContribution>(),
                Array.Empty<TrainCarContribution>(),
                Array.Empty<TrainContribution>(),
                Array.Empty<ContributionFactoryDefinition>(),
                Array.Empty<MenuContribution>(),
                Array.Empty<DockingContribution>(),
                Array.Empty<AccountGenreContribution>(),
                Array.Empty<SpecialRailContribution>(),
                Array.Empty<SpecialStructureContribution>(),
                Array.Empty<TrainControllerContribution>(),
                Array.Empty<NewGameContribution>(),
                Array.Empty<SpriteFactoryContribution>(),
                Array.Empty<SpriteLoaderContribution>(),
                Array.Empty<ColorLibraryContribution>(),
                Array.Empty<ColorMapTrainPictureContribution>(),
                Array.Empty<TrainDepartureBellContribution>(),
                Array.Empty<RailSignalContribution>(),
                Array.Empty<DummyCarContribution>(),
                Array.Empty<HalfVoxelStructureContribution>(),
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
        IReadOnlyList<SpriteFrame> regularFrames = contribution.Descendants("sprite")
            .Select(sprite => ParseSpriteFrame(pluginDirectory, contribution, sprite, pictures))
            .Where(frame => frame is not null)
            .Cast<SpriteFrame>()
            .ToList()
            .AsReadOnly();
        IReadOnlyList<SpriteFrame> frames = regularFrames.Count > 0
            ? regularFrames
            : ParseVariableHeightBuildingFrames(pluginDirectory, contribution, pictures);
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
            ElementValue(contribution, "group"),
            ElementValue(contribution, "subgroup"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "description"),
            ParseInt(ElementValue(contribution, "price")),
            sizeX,
            sizeY,
            height,
            ParseInt(ElementValue(contribution, "minHeight")),
            ParseInt(ElementValue(contribution, "maxHeight")),
            ParsePopulationBase(contribution),
            contribution.Element("computerCannotBuild") is null,
            ClassifySpritePlacement(contribution),
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

    private static StationContribution ParseStationContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        (int sizeH, int sizeV) = ParseSize(ElementValue(contribution, "size"));
        sizeH = Math.Max(1, sizeH);
        sizeV = Math.Max(1, sizeV);
        int operationCost = int.TryParse(ElementValue(contribution, "operationCost"), out int parsedCost)
            ? parsedCost
            : 0;
        XElement? spriteElement = contribution.Element("sprite");
        SpriteFrame? frame = spriteElement is null
            ? null
            : ParseSpriteFrame(pluginDirectory, contribution, spriteElement, pictures);
        ModernSpriteSet2D? spriteSet = spriteElement is null
            ? null
            : Load2DSpriteSet(pluginDirectory, contribution, spriteElement, pictures, sizeH, sizeV, 1);
        string? error = frame is null && spriteSet is null
            ? "Station has no sprite."
            : frame?.IsLoadable == true || spriteSet?.IsLoadable == true
                ? null
                : frame?.Error ?? "Station sprite is not loadable.";

        return new StationContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "group"),
            ElementValue(contribution, "name"),
            sizeH,
            sizeV,
            operationCost,
            frame,
            spriteSet,
            string.IsNullOrWhiteSpace(error) ? null : error);
    }

    private static TrainCarContribution ParseTrainCarContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        string className = OptionalAttributeValue(contribution.Element("class"), "name");
        bool isAsymmetric = className.EndsWith(".AsymTrainCarImpl", StringComparison.OrdinalIgnoreCase);
        bool isSymmetric = className.EndsWith(".SymTrainCarImpl", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith(".ColoredTrainCarImpl", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith(".ReverseTrainCarImpl", StringComparison.OrdinalIgnoreCase);
        XElement? spriteElement = contribution.Element("sprite");
        Dictionary<int, SpriteFrame> frames = new();
        string? error = null;

        if (spriteElement is null)
        {
            error = "Train car has no sprite.";
        }
        else if (!isAsymmetric && !isSymmetric)
        {
            error = string.IsNullOrWhiteSpace(className) ? "Train car class not found." : $"Unsupported train car class '{className}'.";
        }
        else
        {
            ResolvedSpritePicture picture = ResolveSpritePicture(pluginDirectory, spriteElement, pictures);
            (int originX, int originY) = ParsePoint(AttributeValue(spriteElement, "origin"));
            int frameCount = isAsymmetric ? 16 : 8;
            for (int i = 0; i < frameCount; i++)
            {
                int sourceX = originX + (i % 8) * 32;
                int sourceY = originY + (i / 8) * 32;
                frames[i] = CreateSpriteFrame(picture, sourceX, sourceY, 32, 32, 0, 0);
            }

            if (!frames.Values.Any(frame => frame.IsLoadable))
            {
                error = string.Join("; ", frames.Values.Select(frame => frame.Error).Where(message => !string.IsNullOrWhiteSpace(message)).Distinct());
            }
        }

        return new TrainCarContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ParseInt(ElementValue(contribution, "capacity")),
            ParseInt(ElementValue(contribution, "seatedcapacity")),
            isAsymmetric,
            frames,
            string.IsNullOrWhiteSpace(error) ? null : error);
    }

    private static TrainContribution ParseTrainContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        XElement? composition = contribution.Element("composition");
        string? head = OptionalAttributeValue(composition?.Element("head"), "carRef");
        string? body = OptionalAttributeValue(composition?.Element("body"), "carRef");
        string? tail = OptionalAttributeValue(composition?.Element("tail"), "carRef");
        body = string.IsNullOrWhiteSpace(body)
            ? OptionalAttributeValue(composition?.Elements().FirstOrDefault(), "carRef")
            : body;
        int speed = ElementValue(contribution, "speed").Trim().ToLowerInvariant() switch
        {
            "superexpress" or "superfast" or "veryfast" => 1,
            "express" or "fast" => 2,
            "middle" or "medium" or "normal" => 3,
            "slow" => 4,
            _ => 5
        };
        string? error = string.IsNullOrWhiteSpace(body)
            ? "Train composition has no body car."
            : null;

        return new TrainContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "company"),
            ElementValue(contribution, "type"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "author"),
            ElementValue(contribution, "description"),
            ParseInt(ElementValue(contribution, "fare")),
            ParseInt(ElementValue(contribution, "price")),
            ParseInt(ElementValue(contribution, "amenity")),
            ElementValue(contribution, "triprange"),
            speed,
            string.IsNullOrWhiteSpace(head) ? null : head,
            string.IsNullOrWhiteSpace(body) ? null : body,
            string.IsNullOrWhiteSpace(tail) ? null : tail,
            error);
    }

    private static SpriteContribution ToRailSignalSpriteContribution(RailSignalContribution signal)
    {
        return new SpriteContribution(
            signal.PluginDirectoryName,
            signal.PluginTitle,
            signal.Id,
            "railSignal",
            "Rail signals",
            signal.Side,
            signal.Name,
            "",
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            true,
            SpriteContributionPlacementKind.RailStationary,
            signal.Frame is null ? Array.Empty<SpriteFrame>() : new[] { signal.Frame },
            null,
            null,
            signal.Error);
    }

    private static SpriteContribution ToDummyCarSpriteContribution(DummyCarContribution car)
    {
        List<SpriteFrame> frames = new();
        if (car.EastWestFrame is { } eastWest)
        {
            frames.Add(eastWest);
        }

        if (car.NorthSouthFrame is { } northSouth)
        {
            frames.Add(northSouth);
        }

        return new SpriteContribution(
            car.PluginDirectoryName,
            car.PluginTitle,
            car.Id,
            "DummyCar",
            "Road vehicles",
            "",
            car.Name,
            "",
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            true,
            SpriteContributionPlacementKind.RoadAccessory,
            frames.AsReadOnly(),
            null,
            null,
            car.Error);
    }

    private static SpriteContribution ToHalfVoxelSpriteContribution(HalfVoxelStructureContribution structure)
    {
        IReadOnlyList<SpriteFrame> frames = structure.Patterns
            .Select(pattern => pattern.Frame)
            .ToList()
            .AsReadOnly();

        return new SpriteContribution(
            structure.PluginDirectoryName,
            structure.PluginTitle,
            structure.Id,
            "HalfVoxelStructure",
            structure.Group,
            structure.Subgroup,
            structure.Name,
            "",
            structure.Price,
            1,
            1,
            Math.Max(1, structure.Height),
            0,
            0,
            structure.PopulationBase,
            true,
            SpriteContributionPlacementKind.Structure,
            frames,
            null,
            null,
            structure.Error);
    }

    private static ContributionFactoryDefinition ParseContributionFactory(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new ContributionFactoryDefinition(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ParseClassReference(contribution.Element("class")),
            ParseClassReference(contribution.Element("implementation")));
    }

    private static MenuContribution ParseMenuContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new MenuContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ParseClassReference(contribution.Element("class")));
    }

    private static DockingContribution ParseDockingContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        XElement? menu = contribution.Element("menu");
        int? position = int.TryParse(OptionalAttributeValue(menu, "position"), out int parsedPosition)
            ? parsedPosition
            : null;

        return new DockingContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            OptionalAttributeValue(menu, "name"),
            OptionalAttributeValue(menu, "location"),
            position,
            contribution.Element("multiple") is not null,
            ParseClassReference(contribution.Element("class")));
    }

    private static AccountGenreContribution ParseAccountGenreContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new AccountGenreContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"));
    }

    private static SpecialRailContribution ParseSpecialRailContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new SpecialRailContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "description"),
            ClassifySpecialRail(ParseClassReference(contribution.Element("class")).Name),
            ParseClassReference(contribution.Element("class")));
    }

    private static ModernSpecialRailKind ClassifySpecialRail(string className)
    {
        if (className.EndsWith(".BridgeRailContributionImpl", StringComparison.OrdinalIgnoreCase))
        {
            return ModernSpecialRailKind.Bridge;
        }

        if (className.EndsWith(".StealSupportedRailContributionImpl", StringComparison.OrdinalIgnoreCase))
        {
            return ModernSpecialRailKind.SteelSupported;
        }

        if (className.EndsWith(".TunnelRailContributionImpl", StringComparison.OrdinalIgnoreCase))
        {
            return ModernSpecialRailKind.Tunnel;
        }

        if (className.EndsWith(".TrainGarageContributionImpl", StringComparison.OrdinalIgnoreCase))
        {
            return ModernSpecialRailKind.Garage;
        }

        return ModernSpecialRailKind.Unsupported;
    }

    private static SpecialStructureContribution ParseSpecialStructureContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new SpecialStructureContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "description"),
            ParseClassReference(contribution.Element("class")));
    }

    private static TrainControllerContribution ParseTrainControllerContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new TrainControllerContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "description"),
            ParseClassReference(contribution.Element("class")));
    }

    private static NewGameContribution ParseNewGameContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new NewGameContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "author"),
            ElementValue(contribution, "description"),
            ParseClassReference(contribution.Element("class")));
    }

    private static SpriteFactoryContribution ParseSpriteFactoryContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new SpriteFactoryContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ParseClassReference(contribution.Element("class")));
    }

    private static SpriteLoaderContribution ParseSpriteLoaderContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        return new SpriteLoaderContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ParseClassReference(contribution.Element("class")));
    }

    private static ColorLibraryContribution ParseColorLibraryContribution(
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        List<string> colors = contribution.Elements("element")
            .Select(element => AttributeValue(element, "color"))
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .ToList();
        if (string.Equals(AttributeValue(contribution, "id"), "{COLORLIB-NULL}", StringComparison.OrdinalIgnoreCase)
            && colors.Count == 0)
        {
            colors.Add("Transparent");
        }

        return new ColorLibraryContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            colors.AsReadOnly());
    }

    private static ColorMapTrainPictureContribution ParseColorMapTrainPictureContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        SpriteFrame? frame = contribution.Element("picture") is { } pictureElement
            ? CreateSpriteFrame(ResolvePictureElement(pluginDirectory, pictureElement, pictures), 0, 0, 32, 32, 0, 0)
            : null;
        string? error = frame is null
            ? "Color-mapped train picture has no picture."
            : frame.IsLoadable
                ? null
                : frame.Error;

        return new ColorMapTrainPictureContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "author"),
            frame,
            string.IsNullOrWhiteSpace(error) ? null : error);
    }

    private static TrainDepartureBellContribution ParseTrainDepartureBellContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        string source = OptionalAttributeValue(contribution.Element("sound"), "href");
        string resolvedPath = ResolvePluginPath(pluginDirectory, source);
        string? error = string.IsNullOrWhiteSpace(source)
            ? "Departure bell has no sound href."
            : File.Exists(resolvedPath)
                ? null
                : "Sound file not found.";

        return new TrainDepartureBellContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            source,
            resolvedPath,
            error);
    }

    private static RailSignalContribution ParseRailSignalContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        SpriteFrame? frame = contribution.Element("picture") is { } pictureElement
            ? CreateSpriteFrame(ResolvePictureElement(pluginDirectory, pictureElement, pictures), 0, 0, 32, 32, 0, 0)
            : null;
        string? error = frame is null
            ? "Rail signal has no picture."
            : frame.IsLoadable
                ? null
                : frame.Error;

        return new RailSignalContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            ElementValue(contribution, "side"),
            frame,
            string.IsNullOrWhiteSpace(error) ? null : error);
    }

    private static DummyCarContribution ParseDummyCarContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        XElement? sprite = contribution.Element("sprite");
        SpriteFrame? eastWest = null;
        SpriteFrame? northSouth = null;
        IReadOnlyList<DummyCarVariation> variations = Array.Empty<DummyCarVariation>();

        if (sprite is not null)
        {
            ResolvedSpritePicture picture = ResolveSpritePicture(pluginDirectory, sprite, pictures);
            (int originX, int originY) = ParsePoint(AttributeValue(sprite, "origin"));
            int offsetY = ParseInt(AttributeValue(sprite, "offset"));
            int height = Math.Max(16, 16 + offsetY);
            eastWest = CreateSpriteFrame(picture, originX, originY, 32, height, 0, offsetY);
            northSouth = CreateSpriteFrame(picture, originX + 32, originY, 32, height, 0, offsetY);
            variations = sprite.Element("variations")?.Elements("colorVariation")
                .Select(variation => new DummyCarVariation(ParseColorMaps(variation)))
                .ToList()
                .AsReadOnly() ?? Array.Empty<DummyCarVariation>().AsReadOnly();
        }

        string? error = eastWest is null && northSouth is null
            ? "Dummy car has no sprite."
            : eastWest?.IsLoadable == true || northSouth?.IsLoadable == true
                ? null
                : eastWest?.Error ?? northSouth?.Error;

        return new DummyCarContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "name"),
            eastWest,
            northSouth,
            variations,
            string.IsNullOrWhiteSpace(error) ? null : error);
    }

    private static HalfVoxelStructureContribution ParseHalfVoxelStructureContribution(
        string manifestPath,
        string directoryName,
        string pluginTitle,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        string pluginDirectory = Path.GetDirectoryName(manifestPath) ?? "";
        XElement? sprite = contribution.Element("sprite");
        int height = ParseInt(ElementValue(contribution, "height"));
        string? colorLibraryId = OptionalAttributeValue(sprite?.Element("map"), "to");
        List<HalfVoxelPattern> patterns = new();

        if (sprite is not null)
        {
            ResolvedSpritePicture picture = ResolveSpritePicture(pluginDirectory, sprite, pictures);
            ResolvedSpritePicture? highlightPicture = sprite.Element("highlight") is { } highlight
                ? ResolvePictureElement(pluginDirectory, highlight, pictures)
                : null;

            foreach (XElement pattern in sprite.Elements("pattern"))
            {
                string direction = AttributeValue(pattern, "direction");
                string side = AttributeValue(pattern, "side");
                (int originX, int originY) = ParsePoint(AttributeValue(pattern, "origin"));
                SpriteFrame frame = CreateSpriteFrame(
                    picture,
                    originX,
                    originY,
                    24,
                    8 + Math.Max(0, height) * 16,
                    HalfVoxelOffsetX(direction, side),
                    HalfVoxelOffsetY(direction, side, height));
                List<SpriteFrame> highlights = new();
                if (highlightPicture is { } highlightResolved && pattern.Element("highlight") is not null)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        highlights.Add(CreateSpriteFrame(
                            highlightResolved,
                            originX,
                            originY,
                            24,
                            8 + Math.Max(0, height) * 16,
                            HalfVoxelOffsetX(direction, side),
                            HalfVoxelOffsetY(direction, side, height)));
                    }
                }

                patterns.Add(new HalfVoxelPattern(direction, side, frame, highlights.AsReadOnly()));
            }
        }

        string? error = sprite is null
            ? "Half-voxel structure has no sprite."
            : patterns.Count == 0
                ? "Half-voxel structure has no directional patterns."
                : patterns.Any(pattern => pattern.Frame.IsLoadable)
                    ? null
                    : string.Join("; ", patterns.Select(pattern => pattern.Frame.Error).Where(message => !string.IsNullOrWhiteSpace(message)).Distinct());

        return new HalfVoxelStructureContribution(
            directoryName,
            pluginTitle,
            AttributeValue(contribution, "id"),
            ElementValue(contribution, "group"),
            ElementValue(contribution, "subgroup"),
            ElementValue(contribution, "name"),
            ParseInt(ElementValue(contribution, "price")),
            height,
            ParsePopulationBase(contribution),
            string.IsNullOrWhiteSpace(colorLibraryId) ? null : colorLibraryId,
            patterns.AsReadOnly(),
            string.IsNullOrWhiteSpace(error) ? null : error);
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

    private static PluginClassReference ParseClassReference(XElement? element)
    {
        return new PluginClassReference(
            OptionalAttributeValue(element, "name"),
            OptionalAttributeValue(element, "codebase"));
    }

    private static IReadOnlyList<ColorMapEntry> ParseColorMaps(XElement element)
    {
        return element.Elements("map")
            .Select(map => new ColorMapEntry(AttributeValue(map, "from"), AttributeValue(map, "to")))
            .ToList()
            .AsReadOnly();
    }

    private static int HalfVoxelOffsetX(string direction, string side)
    {
        int index = HalfVoxelDirectionIndex(direction) + HalfVoxelSideIndex(side) * 4;
        return HalfVoxelOffsets[index].X;
    }

    private static int HalfVoxelOffsetY(string direction, string side, int height)
    {
        int index = HalfVoxelDirectionIndex(direction) + HalfVoxelSideIndex(side) * 4;
        return HalfVoxelOffsets[index].Y + Math.Max(0, height) * 16;
    }

    private static int HalfVoxelDirectionIndex(string direction)
    {
        return direction.Trim().ToLowerInvariant() switch
        {
            "north" => 0,
            "east" => 1,
            "south" => 2,
            "west" => 3,
            _ => 0
        };
    }

    private static int HalfVoxelSideIndex(string side)
    {
        return side.Trim().ToLowerInvariant() == "back" ? 1 : 0;
    }

    private static ReadOnlyCollection<T> SortByPluginAndName<T>(
        IEnumerable<T> source,
        Func<T, string> pluginSelector,
        Func<T, string> nameSelector)
    {
        return source
            .OrderBy(pluginSelector, StringComparer.OrdinalIgnoreCase)
            .ThenBy(nameSelector, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
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

    private static IReadOnlyList<SpriteFrame> ParseVariableHeightBuildingFrames(
        string pluginDirectory,
        XElement contribution,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        if (!string.Equals(AttributeValue(contribution, "type"), "varHeightBuilding", StringComparison.OrdinalIgnoreCase)
            || contribution.Element("pictures") is not { } pictureSet)
        {
            return Array.Empty<SpriteFrame>();
        }

        List<SpriteFrame> frames = new();
        foreach (string partName in new[] { "bottom", "middle", "top" })
        {
            if (pictureSet.Element(partName) is not { } part)
            {
                continue;
            }

            frames.Add(ParsePicturePartFrame(pluginDirectory, contribution, part, pictures));
        }

        return frames.AsReadOnly();
    }

    private static SpriteFrame ParsePicturePartFrame(
        string pluginDirectory,
        XElement contribution,
        XElement part,
        IReadOnlyDictionary<string, PictureContribution> pictures)
    {
        ResolvedSpritePicture picture = ResolvePictureElement(pluginDirectory, part.Element("picture") ?? part, pictures);
        (int sourceX, int sourceY) = ParsePoint(AttributeValue(part, "origin"));
        string offset = AttributeValue(part, "offset");
        (int offsetX, int offsetY) = string.IsNullOrWhiteSpace(offset)
            ? (0, DefaultOffsetY(contribution))
            : ParseOffset(offset);
        (int sizeX, int sizeY) = ParseSize(ElementValue(contribution, "size"));
        int sourceWidth = Math.Max(32, (Math.Max(1, sizeX) + Math.Max(1, sizeY)) * 16);
        int sourceHeight = Math.Max(16, offsetY + 16);
        return CreateSpriteFrame(picture, sourceX, sourceY, sourceWidth, sourceHeight, offsetX, offsetY);
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
            || type.Contains("commercial", StringComparison.OrdinalIgnoreCase)
            || type.Contains("railStationary", StringComparison.OrdinalIgnoreCase);
    }

    private static SpriteContributionPlacementKind ClassifySpritePlacement(XElement contribution)
    {
        string type = AttributeValue(contribution, "type");
        if (type.Equals("varHeightBuilding", StringComparison.OrdinalIgnoreCase))
        {
            return SpriteContributionPlacementKind.VariableHeightBuilding;
        }

        if (type.Equals("railStationary", StringComparison.OrdinalIgnoreCase))
        {
            return SpriteContributionPlacementKind.RailStationary;
        }

        if (type.Equals("roadAccessory", StringComparison.OrdinalIgnoreCase))
        {
            return SpriteContributionPlacementKind.RoadAccessory;
        }

        if (type.Equals("electricPole", StringComparison.OrdinalIgnoreCase))
        {
            return SpriteContributionPlacementKind.ElectricPole;
        }

        if (type.Contains("structure", StringComparison.OrdinalIgnoreCase)
            || type.Equals("commercial", StringComparison.OrdinalIgnoreCase)
            || type.Equals("specialStructure", StringComparison.OrdinalIgnoreCase))
        {
            return SpriteContributionPlacementKind.Structure;
        }

        return SpriteContributionPlacementKind.Generic;
    }

    private static bool IsPromotedSpriteContributionType(string type)
    {
        return type.Equals("railSignal", StringComparison.OrdinalIgnoreCase)
            || type.Equals("DummyCar", StringComparison.OrdinalIgnoreCase)
            || type.Equals("HalfVoxelStructure", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParsePopulationBase(XElement contribution)
    {
        return contribution.Element("population") is { } population
            ? ParseInt(ElementValue(population, "base"))
            : 0;
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

    private static readonly (int X, int Y)[] HalfVoxelOffsets =
    {
        (0, -8),
        (-8, -8),
        (0, -8),
        (-8, -8),
        (-8, -4),
        (0, -4),
        (-8, -4),
        (0, -4)
    };

    private readonly record struct ResolvedSpritePicture(string PictureId, string Source, string ResolvedPath, string? Error);
}
