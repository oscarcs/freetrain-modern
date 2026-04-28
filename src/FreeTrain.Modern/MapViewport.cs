using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace FreeTrain.Modern;

public enum MapEditMode
{
    Select,
    Rail,
    Road,
    Station,
    Structure,
    Platform,
    Train,
    Terrain,
    Erase
}

public sealed class MapViewport : Control, IDisposable
{
    private readonly record struct RenderQueueItem(int H, int V, int Z, int Layer, Action<DrawingContext> Draw);
    private readonly record struct GeneratedTreeCandidate(int H, int V, int Z, double Score, int TieBreaker);

    private const int TileWidth = 32;
    private const int TileHeight = 16;
    private const string RailRoadCrossingPictureId = "{F4380415-A2F2-41d8-8FCD-ED25A470A84D}";

    private readonly LegacySpriteSheet groundTiles;
    private readonly Bitmap railBitmap;
    private readonly Bitmap? bridgeRailBitmap;
    private readonly Bitmap? bridgePierBitmap;
    private readonly Bitmap? defaultBridgePierBitmap;
    private readonly Bitmap? steelSupportedRailBitmap;
    private readonly Bitmap? tunnelRailBitmap;
    private readonly Bitmap? garageRailBitmap;
    private readonly Bitmap thinPlatformBitmap;
    private readonly Bitmap fatPlatformBitmap;
    private readonly IBrush background = Brushes.Black;
    private readonly IBrush nightBrush = new SolidColorBrush(Color.FromArgb(82, 15, 28, 54));
    private readonly Pen hoverPen = new(new SolidColorBrush(Color.FromRgb(255, 247, 153)), 2);
    private readonly Pen selectionPen = new(new SolidColorBrush(Color.FromRgb(255, 112, 88)), 2.4);
    private readonly Pen buildAnchorPen = new(new SolidColorBrush(Color.FromRgb(71, 135, 255)), 2.2);
    private readonly Pen validPreviewPen = new(new SolidColorBrush(Color.FromRgb(63, 189, 121)), 2);
    private readonly Pen invalidPreviewPen = new(new SolidColorBrush(Color.FromRgb(222, 80, 80)), 2);
    private readonly IBrush hoverFill = new SolidColorBrush(Color.FromArgb(44, 255, 247, 153));
    private readonly IBrush selectionFill = new SolidColorBrush(Color.FromArgb(58, 255, 112, 88));
    private readonly IBrush buildAnchorFill = new SolidColorBrush(Color.FromArgb(50, 71, 135, 255));
    private readonly IBrush validPreviewFill = new SolidColorBrush(Color.FromArgb(44, 63, 189, 121));
    private readonly IBrush invalidPreviewFill = new SolidColorBrush(Color.FromArgb(50, 222, 80, 80));
    private ModernWorld world;
    private readonly TerrainRenderer terrainRenderer;
    private readonly IReadOnlyList<RoadContribution> roadContributions;
    private readonly IReadOnlyList<LandContribution> landContributions;
    private readonly IReadOnlyList<SpriteContribution> spriteContributions;
    private readonly IReadOnlyList<SpriteContribution> structureContributions;
    private readonly IReadOnlyList<StationContribution> stationContributions;
    private readonly IReadOnlyList<SpecialRailContribution> specialRailContributions;
    private readonly IReadOnlyList<TrainContribution> trainContributions;
    private readonly IReadOnlyDictionary<string, TrainCarContribution> trainCarContributions;
    private readonly string? railRoadCrossingPath;
    private readonly Dictionary<string, Bitmap> pluginBitmaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string ForestId, int H, int V), IReadOnlyList<ForestTreePattern>> forestPatternCache = new();
    private readonly RenderOptions pixelArtRenderOptions = new()
    {
        BitmapInterpolationMode = BitmapInterpolationMode.None,
        EdgeMode = EdgeMode.Aliased
    };
    private TileLocation? hoverLocation;
    private TileLocation? selectedLocation;
    private TileLocation? buildAnchorLocation;
    private double zoom = 2.0;
    private bool showGrid;
    private bool useNightView;
    private int maxVisibleLevel;
    private int worldMaxGroundLevel;
    private MapEditMode editMode = MapEditMode.Select;
    private int activeRoadIndex;
    private int activeSpecialRailIndex = -1;
    private int activeRailBuildLevel = 1;
    private int activeStationIndex;
    private int activeStructureIndex;
    private int activeStructureFrameIndex;
    private int activeStructureColorVariantIndex;
    private int activeTrainIndex;
    private int activePlatformDirectionIndex = 2;
    private int activePlatformLength = 4;
    private PlatformStyle activePlatformStyle = PlatformStyle.ThinRoof;
    private string lastMessage = "Ready.";
    private Rect visibleContentBounds;
    private bool hasVisibleContentBounds;

    public MapViewport(LegacyAssetCatalog assets, PluginManifestCatalog plugins)
    {
        string groundPath = assets.FindResource("EmptyChip.bmp")
            ?? throw new FileNotFoundException("Missing legacy ground sprite sheet.", "EmptyChip.bmp");
        string palettePath = assets.FindResource("mountainPalette.xml")
            ?? throw new FileNotFoundException("Missing legacy mountain palette.", "mountainPalette.xml");
        string cliffPath = assets.FindResource("Cliff.bmp")
            ?? throw new FileNotFoundException("Missing legacy cliff sprite sheet.", "Cliff.bmp");
        string railPath = assets.FindResource("RailRoads.bmp")
            ?? throw new FileNotFoundException("Missing legacy rail sprite sheet.", "RailRoads.bmp");
        string? bridgeRailPath = FindPluginAsset(assets, "org.kohsuke.freetrain.rail", "BridgeRail.bmp");
        string? bridgePierPath = FindPluginAsset(assets, "org.kohsuke.freetrain.rail", "BridgePier.bmp");
        string? defaultBridgePierPath = assets.FindResource("BridgePier.bmp");
        string? steelSupportedRailPath = FindPluginAsset(assets, "org.kohsuke.freetrain.rail", "StealSupportedRail.bmp");
        string? tunnelRailPath = FindPluginAsset(assets, "org.kohsuke.freetrain.rail", "TunnelRail.bmp");
        string? garageRailPath = FindPluginAsset(assets, "org.kohsuke.freetrain.rail.garage", "garage.bmp");
        string thinPlatformPath = Path.Combine(assets.PluginDirectory, "system", "ThinPlatform.bmp");
        if (!File.Exists(thinPlatformPath))
        {
            throw new FileNotFoundException("Missing legacy thin platform sprite sheet.", thinPlatformPath);
        }

        string fatPlatformPath = assets.FindResource("FatPlatform.bmp")
            ?? throw new FileNotFoundException("Missing legacy fat platform sprite sheet.", "FatPlatform.bmp");

        groundTiles = new LegacySpriteSheet(groundPath, TileWidth, TileHeight);
        railBitmap = LegacyBitmap.LoadWithColorKey(railPath);
        bridgeRailBitmap = bridgeRailPath is null ? null : LegacyBitmap.LoadWithColorKey(bridgeRailPath);
        bridgePierBitmap = bridgePierPath is null ? null : LegacyBitmap.LoadWithColorKey(bridgePierPath);
        defaultBridgePierBitmap = defaultBridgePierPath is null ? null : LegacyBitmap.LoadWithColorKey(defaultBridgePierPath);
        steelSupportedRailBitmap = steelSupportedRailPath is null ? null : LegacyBitmap.LoadWithColorKey(steelSupportedRailPath);
        tunnelRailBitmap = tunnelRailPath is null ? null : LegacyBitmap.LoadWithColorKey(tunnelRailPath);
        garageRailBitmap = garageRailPath is null ? null : LegacyBitmap.LoadWithColorKey(garageRailPath);
        thinPlatformBitmap = LegacyBitmap.LoadWithColorKey(thinPlatformPath);
        fatPlatformBitmap = LegacyBitmap.LoadWithColorKey(fatPlatformPath);
        terrainRenderer = new TerrainRenderer(palettePath, cliffPath, groundPath);
        roadContributions = plugins.Roads.Where(road => road.IsLoadable).ToList().AsReadOnly();
        landContributions = plugins.Lands.Where(land => land.IsLoadable).ToList().AsReadOnly();
        spriteContributions = plugins.Sprites.Where(sprite => sprite.IsLoadable).ToList().AsReadOnly();
        structureContributions = plugins.Structures
            .Concat(plugins.RailStationaries)
            .Concat(plugins.RoadAccessories)
            .Concat(plugins.ElectricPoles)
            .Where(sprite => sprite.IsLoadable)
            .OrderBy(sprite => StructurePlacementPriority(sprite.PlacementKind))
            .ThenBy(sprite => sprite.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        stationContributions = plugins.Stations.Where(station => station.IsLoadable).ToList().AsReadOnly();
        specialRailContributions = plugins.SpecialRails
            .Where(rail => rail.Kind is not ModernSpecialRailKind.Unsupported)
            .OrderBy(rail => SpecialRailPriority(rail.Kind))
            .ThenBy(rail => rail.Class.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        trainContributions = plugins.Trains.Where(train => train.IsLoadable).ToList().AsReadOnly();
        trainCarContributions = plugins.TrainCars
            .Where(car => car.IsLoadable)
            .GroupBy(car => car.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        railRoadCrossingPath = FindRailRoadCrossingPath(assets, plugins);
        activeRoadIndex = SelectInitialRoadIndex(roadContributions);
        world = ModernWorld.CreateNew(ModernWorldCreationOptions.Default);
        AddGeneratedTreeScatter(world, ModernWorldCreationOptions.Default.TerrainKind);
        worldMaxGroundLevel = world.MaxGroundLevel;
        world.Changed += OnWorldChanged;
        maxVisibleLevel = world.MaxHeightCutLevel;
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public event Action<MapViewportStatus>? StatusChanged;

    public double Zoom
    {
        get => zoom;
        set
        {
            double next = Math.Clamp(value, 0.75, 4.0);
            if (Math.Abs(zoom - next) < 0.001)
            {
                return;
            }

            zoom = next;
            InvalidateMeasure();
            InvalidateVisual();
            PublishStatus();
        }
    }

    public bool ShowGrid
    {
        get => showGrid;
        set
        {
            if (showGrid == value)
            {
                return;
            }

            showGrid = value;
            InvalidateVisual();
            PublishStatus();
        }
    }

    public bool UseNightView
    {
        get => useNightView;
        set
        {
            if (useNightView == value)
            {
                return;
            }

            useNightView = value;
            InvalidateVisual();
            PublishStatus();
        }
    }

    public int MaxVisibleLevel
    {
        get => maxVisibleLevel;
        set
        {
            int next = Math.Clamp(value, 0, world.MaxHeightCutLevel);
            if (maxVisibleLevel == next)
            {
                return;
            }

            maxVisibleLevel = next;
            InvalidateVisual();
            PublishStatus();
        }
    }

    public int WorldMaxGroundLevel => worldMaxGroundLevel;
    public int WorldMaxHeightCutLevel => world.MaxHeightCutLevel;
    public MapEditMode EditMode
    {
        get => editMode;
        set
        {
            if (editMode == value)
            {
                return;
            }

            editMode = value;
            if (!ToolUsesAnchor)
            {
                buildAnchorLocation = null;
            }

            InvalidateVisual();
            PublishStatus();
        }
    }

    public string ActiveRoadName => ActiveRoadContribution?.DisplayName ?? "No road plugins loaded";
    public string ActiveStationName => ActiveStationContribution?.DisplayName ?? "No station plugins loaded";
    public string ActiveStructureName => ActiveStructureContribution is { } structure
        ? structure.Frames.Count > 1
            ? $"{structure.DisplayName} ({Math.Clamp(activeStructureFrameIndex, 0, structure.Frames.Count - 1) + 1}/{structure.Frames.Count})"
            : structure.DisplayName
        : "No structure plugins loaded";
    public string ActiveTrainName => ActiveTrainContribution?.DisplayName ?? "No train plugins loaded";
    public string ActivePlatformDescription => $"{ModernDirection.FromIndex(activePlatformDirectionIndex).EnglishName}, {activePlatformLength} tile(s), {FormatPlatformStyle(activePlatformStyle)}";
    public int ActiveStructureColorVariantIndex => activeStructureColorVariantIndex;
    public int ActiveStructureColorVariantCount => ActiveStructureContribution is { } structure
        ? GetColorVariantCount(structure)
        : 1;
    public string ActiveStructureColorVariantDescription => ActiveStructureColorVariantCount > 1
        ? $"Color {Math.Clamp(activeStructureColorVariantIndex, 0, ActiveStructureColorVariantCount - 1) + 1}/{ActiveStructureColorVariantCount}"
        : "Default color";

    public MapViewportStatus CurrentStatus => CreateStatus();

    public void SetVisibleContentViewport(Vector offset, Size viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            hasVisibleContentBounds = false;
            return;
        }

        Rect next = new(offset.X, offset.Y, viewport.Width, viewport.Height);
        if (hasVisibleContentBounds && AreClose(visibleContentBounds, next))
        {
            return;
        }

        visibleContentBounds = next;
        hasVisibleContentBounds = true;
        InvalidateVisual();
    }

    public ModernWorldSnapshot CreateWorldSnapshot()
    {
        return world.ToSnapshot();
    }

    public void CreateNewWorld(ModernWorldCreationOptions options)
    {
        ModernWorldCreationOptions normalized = options.Normalize();
        ModernWorld next = ModernWorld.CreateNew(normalized);
        AddGeneratedTreeScatter(next, normalized.TerrainKind);
        SetWorld(next);
    }

    public void LoadWorldSnapshot(ModernWorldSnapshot snapshot)
    {
        Dictionary<string, RoadContribution> roadLookup = roadContributions
            .Where(road => !string.IsNullOrWhiteSpace(road.Id))
            .GroupBy(road => road.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, LandContribution> landLookup = landContributions
            .Where(land => !string.IsNullOrWhiteSpace(land.Id))
            .GroupBy(land => land.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, SpriteContribution> spriteLookup = spriteContributions
            .Where(sprite => !string.IsNullOrWhiteSpace(sprite.Id))
            .GroupBy(sprite => sprite.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, StationContribution> stationLookup = stationContributions
            .Where(station => !string.IsNullOrWhiteSpace(station.Id))
            .GroupBy(station => station.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, TrainContribution> trainLookup = trainContributions
            .Where(train => !string.IsNullOrWhiteSpace(train.Id))
            .GroupBy(train => train.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        SetWorld(ModernWorld.FromSnapshot(snapshot, roadLookup, landLookup, spriteLookup, stationLookup, trainLookup));
    }

    private void SetWorld(ModernWorld nextWorld)
    {
        world.Changed -= OnWorldChanged;
        world = nextWorld;
        world.Changed += OnWorldChanged;
        worldMaxGroundLevel = world.MaxGroundLevel;
        selectedLocation = null;
        hoverLocation = null;
        buildAnchorLocation = null;
        maxVisibleLevel = world.MaxHeightCutLevel;
        activeRailBuildLevel = Math.Clamp(activeRailBuildLevel, 0, world.MaxHeightCutLevel);
        lastMessage = $"World ready: {world.Name}.";
        InvalidateMeasure();
        InvalidateVisual();
        PublishStatus();
    }

    public RoadContribution? ActiveRoadContribution => roadContributions.Count == 0
        ? null
        : roadContributions[Math.Clamp(activeRoadIndex, 0, roadContributions.Count - 1)];
    public SpecialRailContribution? ActiveSpecialRailContribution => activeSpecialRailIndex < 0 || specialRailContributions.Count == 0
        ? null
        : specialRailContributions[Math.Clamp(activeSpecialRailIndex, 0, specialRailContributions.Count - 1)];
    public ModernSpecialRailKind ActiveRailKind => ActiveSpecialRailContribution?.Kind ?? ModernSpecialRailKind.Normal;
    public string ActiveRailName => ActiveSpecialRailContribution is { } rail
        ? $"{SpecialRailDisplayName(rail)}{RailTargetLevelSuffix(rail.Kind)}"
        : "Standard rail";
    public StationContribution? ActiveStationContribution => stationContributions.Count == 0
        ? null
        : stationContributions[Math.Clamp(activeStationIndex, 0, stationContributions.Count - 1)];
    public SpriteContribution? ActiveStructureContribution => structureContributions.Count == 0
        ? null
        : structureContributions[Math.Clamp(activeStructureIndex, 0, structureContributions.Count - 1)];
    public TrainContribution? ActiveTrainContribution => trainContributions.Count == 0
        ? null
        : trainContributions[Math.Clamp(activeTrainIndex, 0, trainContributions.Count - 1)];
    public IReadOnlyDictionary<string, TrainCarContribution> TrainCarContributions => trainCarContributions;
    public IReadOnlyList<RoadContribution> RoadContributions => roadContributions;
    public IReadOnlyList<SpecialRailContribution> SpecialRailContributions => specialRailContributions;
    public IReadOnlyList<StationContribution> StationContributions => stationContributions;
    public IReadOnlyList<SpriteContribution> StructureContributions => structureContributions;
    public IReadOnlyList<TrainContribution> TrainContributions => trainContributions;

    private void OnWorldChanged(object? sender, ModernWorldChangedEventArgs e)
    {
        if (e.Kind is ModernWorldChangeKind.Terrain or ModernWorldChangeKind.Reset)
        {
            worldMaxGroundLevel = world.MaxGroundLevel;
            InvalidateMeasure();
        }

        if (!string.IsNullOrWhiteSpace(e.Description))
        {
            lastMessage = e.Description;
        }

        InvalidateVisual();
        PublishStatus();
    }

    private bool ActiveRailKindUsesTargetLevel()
    {
        return ActiveRailKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported;
    }

    private string RailTargetLevelSuffix(ModernSpecialRailKind kind)
    {
        return kind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported
            ? $" @ level {activeRailBuildLevel}"
            : "";
    }

    private double MapOriginX => 64;
    private double MapOriginY => 40 + worldMaxGroundLevel * 8;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect visibleMapRect = VisibleMapRect;
        Rect expandedVisibleMapRect = ExpandRect(visibleMapRect, 96, 160);
        context.FillRectangle(background, hasVisibleContentBounds ? visibleContentBounds : Bounds);

        using (context.PushRenderOptions(pixelArtRenderOptions))
        using (context.PushTransform(Matrix.CreateScale(Zoom, Zoom)))
        {
            RenderWater(context, expandedVisibleMapRect);
            RenderGround(context, expandedVisibleMapRect);
            RenderLandObjects(context, expandedVisibleMapRect);
            RenderRoadObjects(context, expandedVisibleMapRect);
            RenderRailRoadCrossings(context, behindRail: true, expandedVisibleMapRect);
            RenderRailObjects(context, expandedVisibleMapRect);
            RenderRailRoadCrossings(context, behindRail: false, expandedVisibleMapRect);
            RenderWorldObjects(context, expandedVisibleMapRect);
            RenderMapMarkers(context);

            if (UseNightView)
            {
                context.FillRectangle(nightBrush, visibleMapRect);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = world.Width * TileWidth * Zoom + 160;
        double height = (world.Height * (TileHeight / 2.0) + worldMaxGroundLevel * TileHeight) * Zoom + 180;
        return new Size(width, height);
    }

    private Rect VisibleMapRect
    {
        get
        {
            if (hasVisibleContentBounds)
            {
                return new Rect(
                    visibleContentBounds.X / Zoom,
                    visibleContentBounds.Y / Zoom,
                    visibleContentBounds.Width / Zoom,
                    visibleContentBounds.Height / Zoom);
            }

            return new Rect(0, 0, Math.Max(1, Bounds.Width / Zoom), Math.Max(1, Bounds.Height / Zoom));
        }
    }

    private static bool AreClose(Rect left, Rect right)
    {
        return Math.Abs(left.X - right.X) < 0.1
            && Math.Abs(left.Y - right.Y) < 0.1
            && Math.Abs(left.Width - right.Width) < 0.1
            && Math.Abs(left.Height - right.Height) < 0.1;
    }

    private static Rect ExpandRect(Rect rect, double horizontal, double vertical)
    {
        return new Rect(
            rect.X - horizontal,
            rect.Y - vertical,
            rect.Width + horizontal * 2,
            rect.Height + vertical * 2);
    }

    private void RenderWater(DrawingContext context, Rect visibleMapRect)
    {
    }

    private void RenderGround(DrawingContext context, Rect visibleMapRect)
    {
        foreach ((int h, int v) in EnumerateVisibleTiles(visibleMapRect, 64, 96))
        {
            TerrainTilePreview terrain = world.GetTerrainTile(h, v);
            int visualLevel = GetTerrainVisualLevel(terrain);
            Point p = FromHvzToScreen(h, v, visualLevel);

            if (MaxVisibleLevel < terrain.BaseLevel)
            {
                groundTiles.DrawTile(context, 3, p);
                continue;
            }

            if (IsWaterLevel(terrain.BaseLevel) && MaxVisibleLevel >= world.WaterLevel)
            {
                if (!IsFullyUnderwater(terrain))
                {
                    Point shorePoint = FromHvzToScreen(h, v, terrain.BaseLevel);
                    terrainRenderer.DrawTerrainTile(context, world, groundTiles, h, v, shorePoint, terrain, ShowGrid);
                }
                else
                {
                    terrainRenderer.DrawWaterSurfaceTile(context, p);
                }

                continue;
            }

            terrainRenderer.DrawTerrainTile(context, world, groundTiles, h, v, p, terrain, ShowGrid);
        }
    }

    private void RenderWorldObjects(DrawingContext context, Rect visibleMapRect)
    {
        List<RenderQueueItem> items = new();

        foreach (ModernPlatform platform in world.Platforms)
        {
            IReadOnlyList<ModernVoxelKey> voxels = world.GetPlatformVoxels(platform);
            for (int i = 0; i < voxels.Count; i++)
            {
                ModernVoxelKey voxel = voxels[i];
                if (voxel.Z > MaxVisibleLevel || !IsTilePotentiallyVisible(voxel.H, voxel.V, voxel.Z, visibleMapRect, 64, 80, 64, 64))
                {
                    continue;
                }

                int index = i;
                int layer = PlatformDrawsAfterTrain(platform) ? 40 : 0;
                items.Add(new RenderQueueItem(voxel.H, voxel.V, voxel.Z, layer, drawContext =>
                    DrawPlatformTile(drawContext, platform, index, FromHvzToScreen(voxel.H, voxel.V, voxel.Z))));
            }
        }

        foreach ((ModernTrain Train, ModernTrainCarPlacement Car, int Index) train in world.Trains.SelectMany(train => train.Cars.Select((car, index) => (Train: train, Car: car, Index: index))))
        {
            ModernTrainCarPlacement car = train.Car;
            ModernTrainCarRenderPose pose = world.GetTrainCarRenderPose(car);
            if (car.Location.Z > MaxVisibleLevel
                || !trainCarContributions.TryGetValue(car.CarContributionId, out TrainCarContribution? contribution)
                || contribution.FrameForAngle(pose.Angle) is not { } frame
                || !IsTilePotentiallyVisible(car.Location.H, car.Location.V, car.Location.Z, visibleMapRect, 64, 96, 64, 64))
            {
                continue;
            }

            items.Add(new RenderQueueItem(car.Location.H, car.Location.V, car.Location.Z, 20, drawContext =>
            {
                Point tilePoint = FromHvzToScreen(car.Location.H, car.Location.V, car.Location.Z);
                DrawSpriteFrame(drawContext, frame, new Point(tilePoint.X + pose.OffsetX, tilePoint.Y + pose.OffsetY - 9));
            }));
        }

        foreach (MapRailObject garage in world.CreateRailObjects().Where(rail => rail.SpecialKind == ModernSpecialRailKind.Garage))
        {
            if (garage.Z > MaxVisibleLevel || !IsTilePotentiallyVisible(garage.H, garage.V, garage.Z, visibleMapRect, 48, 48, 48, 48))
            {
                continue;
            }

            items.Add(new RenderQueueItem(garage.H, garage.V, garage.Z, 45, drawContext =>
                DrawGarageForeground(drawContext, garage, FromHvzToScreen(garage.H, garage.V, garage.Z))));
        }

        foreach (ModernStation station in world.Stations)
        {
            if (station.Z > MaxVisibleLevel || !IsTilePotentiallyVisible(station.H, station.V, station.Z, visibleMapRect, 260, 320, 260, 160))
            {
                continue;
            }

            if (station.Contribution.SpriteSet2D is { IsLoadable: true } spriteSet)
            {
                Point tilePoint = FromHvzToScreen(station.H, station.V, station.Z);
                foreach (ModernSpriteVoxel2D voxel in spriteSet.InVoxelDrawOrder())
                {
                    ModernSpriteVoxel2D stationVoxel = voxel;
                    int sortH = station.H + stationVoxel.X;
                    int sortV = station.V + stationVoxel.Y;
                    Point voxelPoint = new(
                        tilePoint.X + (stationVoxel.X + stationVoxel.Y) * 16,
                        tilePoint.Y + (-stationVoxel.X + stationVoxel.Y) * 8);
                    items.Add(new RenderQueueItem(sortH, sortV, station.Z, 30, drawContext =>
                        DrawSpriteFrame(drawContext, stationVoxel.Frame, voxelPoint)));
                }
            }
            else if (station.Contribution.Frame is { } frame)
            {
                int sortH = station.H + station.FootprintH - 1;
                int sortV = station.V + station.FootprintV - 1;
                items.Add(new RenderQueueItem(sortH, sortV, station.Z, 30, drawContext =>
                    DrawSpriteFrame(drawContext, frame, FromHvzToScreen(station.H, station.V, station.Z))));
            }
        }

        foreach (ModernPlacedEntity mapObject in world.StructureEntities)
        {
            int z = world.GetTerrainTile(mapObject.H, mapObject.V).SurfaceLevel;
            if (z > MaxVisibleLevel || !IsTilePotentiallyVisible(mapObject.H, mapObject.V, z, visibleMapRect, 320, 560, 160, 120))
            {
                continue;
            }

            int sortH = mapObject.H + Math.Max(1, mapObject.FootprintH) - 1;
            int sortV = mapObject.V + Math.Max(1, mapObject.FootprintV) - 1;
            items.Add(new RenderQueueItem(sortH, sortV, z, 30, drawContext =>
            {
                Point tilePoint = FromHvzToScreen(mapObject.H, mapObject.V, z);
                if (mapObject.StructureContribution?.SpriteSet3D is { IsLoadable: true } spriteSet)
                {
                    DrawSpriteSet3D(drawContext, spriteSet, tilePoint, mapObject.StructureColorVariantIndex);
                }
                else if (mapObject.StructureFrame is { } frame)
                {
                    DrawSpriteFrame(drawContext, frame, tilePoint, mapObject.StructureColorVariantIndex);
                }
            }));
        }

        foreach (RenderQueueItem item in items
            .OrderBy(item => item.H + item.V)
            .ThenBy(item => item.V)
            .ThenBy(item => item.Z)
            .ThenBy(item => item.Layer))
        {
            item.Draw(context);
        }
    }

    private void RenderLandObjects(DrawingContext context, Rect visibleMapRect)
    {
        foreach (ModernPlacedEntity landObject in world.LandEntities.OrderBy(obj => obj.H + obj.V).ThenBy(obj => obj.V))
        {
            TerrainTilePreview terrain = world.GetTerrainTile(landObject.H, landObject.V);
            int z = terrain.SurfaceLevel;
            if (z > MaxVisibleLevel || !IsTilePotentiallyVisible(landObject.H, landObject.V, z, visibleMapRect, 96, 160, 96, 96))
            {
                continue;
            }

            Point tilePoint = FromHvzToScreen(landObject.H, landObject.V, z);
            if (landObject.LandContribution is not { } land)
            {
                continue;
            }

            switch (land.Kind)
            {
                case LandContributionKind.Static:
                    if (land.StaticSprite is { } staticSprite)
                    {
                        DrawSpriteFrame(context, staticSprite, tilePoint);
                    }
                    break;
                case LandContributionKind.Random:
                    if (landObject.ResolvedStaticLand?.StaticSprite is { } randomSprite)
                    {
                        DrawSpriteFrame(context, randomSprite, tilePoint);
                    }
                    break;
                case LandContributionKind.Forest:
                    if (land.Forest is { } forest)
                    {
                        DrawForest(context, forest, landObject.H, landObject.V, tilePoint);
                    }
                    break;
            }
        }
    }

    private void RenderRailObjects(DrawingContext context, Rect visibleMapRect)
    {
        RenderBridgePierVoxels(context, visibleMapRect);

        foreach (MapRailObject railObject in world.CreateRailObjects().OrderBy(obj => obj.H + obj.V).ThenBy(obj => obj.V))
        {
            int z = railObject.Z;
            if (z > MaxVisibleLevel || !IsTilePotentiallyVisible(railObject.H, railObject.V, z, visibleMapRect, 48, 48, 48, 48))
            {
                continue;
            }

            Point tilePoint = FromHvzToScreen(railObject.H, railObject.V, z);
            if (DrawSpecialRailObject(context, railObject, tilePoint))
            {
                continue;
            }

            Point targetPoint = new(tilePoint.X, tilePoint.Y - railObject.Pattern.OffsetY);
            context.DrawImage(
                railBitmap,
                new Rect(
                    railObject.Pattern.SourceX,
                    railObject.Pattern.SourceY,
                    railObject.Pattern.SourceWidth,
                    railObject.Pattern.SourceHeight),
                new Rect(targetPoint, new Size(railObject.Pattern.SourceWidth, railObject.Pattern.SourceHeight)));
        }
    }

    private void RenderBridgePierVoxels(DrawingContext context, Rect visibleMapRect)
    {
        if (bridgePierBitmap is null && defaultBridgePierBitmap is null)
        {
            return;
        }

        Dictionary<(int H, int V), MapRailObject> railLookup = world.CreateRailObjects()
            .Where(rail => rail.SpecialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported)
            .ToDictionary(rail => (rail.H, rail.V));
        foreach (ModernVoxelKey pier in world.BridgePierVoxels.OrderBy(key => key.H + key.V).ThenBy(key => key.V).ThenBy(key => key.Z))
        {
            if (pier.Z > MaxVisibleLevel
                || !railLookup.TryGetValue((pier.H, pier.V), out MapRailObject? rail)
                || !IsTilePotentiallyVisible(pier.H, pier.V, pier.Z, visibleMapRect, 48, 64, 48, 48))
            {
                continue;
            }

            Bitmap? bitmap = rail.SpecialKind == ModernSpecialRailKind.SteelSupported
                ? defaultBridgePierBitmap ?? bridgePierBitmap
                : bridgePierBitmap ?? defaultBridgePierBitmap;
            if (bitmap is null)
            {
                continue;
            }

            Rect source = rail.SpecialKind == ModernSpecialRailKind.SteelSupported
                ? new Rect(0, 0, 32, 32)
                : BridgePierSourceRect(rail, pier);
            Point tilePoint = FromHvzToScreen(pier.H, pier.V, pier.Z);
            context.DrawImage(bitmap, source, new Rect(new Point(tilePoint.X, tilePoint.Y - 16), source.Size));
        }
    }

    private static Rect BridgePierSourceRect(MapRailObject rail, ModernVoxelKey pier)
    {
        ModernDirection direction = DirectionFromRailMask(rail.Pattern.DirectionMask);
        int orientationRow = direction.IsParallelToX ? 0 : 1;
        int topOrBodyColumn = pier.Z == rail.Z - 1 ? 0 : 1;
        return new Rect(topOrBodyColumn * 32, orientationRow * 32, 32, 32);
    }

    private bool DrawSpecialRailObject(DrawingContext context, MapRailObject railObject, Point tilePoint)
    {
        Bitmap? bitmap = railObject.SpecialKind switch
        {
            ModernSpecialRailKind.Bridge => bridgeRailBitmap,
            ModernSpecialRailKind.SteelSupported => steelSupportedRailBitmap,
            ModernSpecialRailKind.Tunnel => tunnelRailBitmap,
            ModernSpecialRailKind.Garage => garageRailBitmap,
            _ => null
        };
        if (bitmap is null)
        {
            return false;
        }

        int column = SpecialRailSourceColumn(railObject);
        Rect source = railObject.SpecialKind == ModernSpecialRailKind.Garage
            ? new Rect(column * 32, 0, 32, 27)
            : new Rect(column * 32, 0, 32, 32);
        double offsetY = railObject.SpecialKind == ModernSpecialRailKind.Garage ? 11 : 16;
        context.DrawImage(bitmap, source, new Rect(new Point(tilePoint.X, tilePoint.Y - offsetY), source.Size));
        return true;
    }

    private void DrawGarageForeground(DrawingContext context, MapRailObject railObject, Point tilePoint)
    {
        if (garageRailBitmap is null)
        {
            return;
        }

        int column = SpecialRailSourceColumn(railObject) + 2;
        Rect source = new(column * 32, 0, 32, 27);
        context.DrawImage(garageRailBitmap, source, new Rect(new Point(tilePoint.X, tilePoint.Y - 11), source.Size));
    }

    private void RenderRoadObjects(DrawingContext context, Rect visibleMapRect)
    {
        foreach (MapRoadObject roadObject in world.CreateRoadObjects().OrderBy(obj => obj.H + obj.V).ThenBy(obj => obj.V))
        {
            TerrainTilePreview terrain = world.GetTerrainTile(roadObject.H, roadObject.V);
            int z = terrain.SurfaceLevel;
            if (z > MaxVisibleLevel
                || roadObject.Frame is not { } frame
                || !IsTilePotentiallyVisible(roadObject.H, roadObject.V, z, visibleMapRect, 64, 64, 64, 64))
            {
                continue;
            }

            DrawSpriteFrame(context, frame, FromHvzToScreen(roadObject.H, roadObject.V, z));
        }
    }

    private void RenderRailRoadCrossings(DrawingContext context, bool behindRail, Rect visibleMapRect)
    {
        if (railRoadCrossingPath is null)
        {
            return;
        }

        foreach (ModernTrafficVoxel traffic in world.TrafficVoxels
            .Where(voxel => voxel.Accessory?.Kind == ModernTrafficAccessoryKind.RailRoadCrossing)
            .OrderBy(voxel => voxel.Location.H + voxel.Location.V)
            .ThenBy(voxel => voxel.Location.V))
        {
            int h = traffic.Location.H;
            int v = traffic.Location.V;
            int z = traffic.Location.Z;
            if (z > MaxVisibleLevel
                || traffic.Accessory?.CrossingOrientation is not { } orientation
                || !IsTilePotentiallyVisible(h, v, z, visibleMapRect, 64, 80, 64, 64))
            {
                continue;
            }

            SpriteFrame frame = CreateRailRoadCrossingFrame(orientation, behindRail);
            DrawSpriteFrame(context, frame, FromHvzToScreen(h, v, z));
        }
    }

    private void RenderMapMarkers(DrawingContext context)
    {
        if (buildAnchorLocation is { } previewAnchor && hoverLocation is { } rawPreviewHover && ToolUsesAnchor)
        {
            TileLocation previewHover = GetTransportPlacementLocation(rawPreviewHover);
            bool canBuildLine = EditMode switch
            {
                MapEditMode.Rail => CanBuildRailLine(previewAnchor, previewHover),
                MapEditMode.Road => CanBuildRoadLine(previewAnchor, previewHover),
                _ => false
            };
            Pen previewPen = canBuildLine ? validPreviewPen : invalidPreviewPen;
            IBrush previewFill = canBuildLine ? validPreviewFill : invalidPreviewFill;
            IEnumerable<TileLocation> previewLine = EditMode == MapEditMode.Rail
                ? world.PreviewRailLine(previewAnchor, previewHover)
                : PreviewLine(previewAnchor, previewHover);
            foreach (TileLocation location in previewLine)
            {
                if (location.Z <= MaxVisibleLevel)
                {
                    DrawDiamondFill(context, FromHvzToScreen(location.H, location.V, location.Z), previewFill, previewPen);
                }
            }
        }

        if (selectedLocation is { } selected && selected.Z <= MaxVisibleLevel)
        {
            DrawLocationMarker(context, selected, selectionFill, selectionPen);
        }

        if (buildAnchorLocation is { } anchor && anchor.Z <= MaxVisibleLevel)
        {
            DrawDiamondFill(context, FromHvzToScreen(anchor.H, anchor.V, anchor.Z), buildAnchorFill, buildAnchorPen);
        }

        if (hoverLocation is { } hover && hover.Z <= MaxVisibleLevel)
        {
            DrawLocationMarker(context, hover, hoverFill, hoverPen);
        }
    }

    private void DrawLocationMarker(DrawingContext context, TileLocation location, IBrush fill, Pen outline)
    {
        if (EditMode == MapEditMode.Terrain)
        {
            DrawCornerMarker(context, location, fill, outline);
            return;
        }

        DrawDiamondFill(context, FromHvzToScreen(location.H, location.V, location.Z), fill, outline);
    }

    private static void DrawDiamondFill(DrawingContext context, Point p, IBrush fill, Pen outline)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext path = geometry.Open())
        {
            path.BeginFigure(new Point(p.X + 16, p.Y), true);
            path.LineTo(new Point(p.X + 32, p.Y + 8));
            path.LineTo(new Point(p.X + 16, p.Y + 16));
            path.LineTo(new Point(p.X, p.Y + 8));
            path.EndFigure(true);
        }

        context.DrawGeometry(fill, outline, geometry);
    }

    private void DrawCornerMarker(DrawingContext context, TileLocation location, IBrush fill, Pen outline)
    {
        Point p = GetTerrainCornerScreenPoint(location);
        context.DrawEllipse(fill, outline, p, 5.5, 5.5);
        context.DrawEllipse(outline.Brush, null, p, 2, 2);
    }

    public void RaiseSelectedTerrain()
    {
        if (selectedLocation is not { } selected)
        {
            return;
        }

        if (world.RaiseCorner(selected.H, selected.V, selected.Corner))
        {
            TerrainTilePreview terrain = world.GetTerrainTile(selected.H, selected.V);
            maxVisibleLevel = Math.Max(maxVisibleLevel, terrain.SurfaceLevel);
            selectedLocation = selected with { Z = GetTerrainVisualLevel(terrain) };
            InvalidateMeasure();
            InvalidateVisual();
            PublishStatus();
        }
    }

    public void LowerSelectedTerrain()
    {
        if (selectedLocation is not { } selected)
        {
            return;
        }

        if (world.LowerCorner(selected.H, selected.V, selected.Corner))
        {
            selectedLocation = selected with { Z = GetTerrainVisualLevel(world.GetTerrainTile(selected.H, selected.V)) };
            InvalidateMeasure();
            InvalidateVisual();
            PublishStatus();
        }
    }

    public void SelectNextRoad()
    {
        if (roadContributions.Count == 0)
        {
            return;
        }

        activeRoadIndex = (activeRoadIndex + 1) % roadContributions.Count;
        PublishStatus();
    }

    public void SelectRoad(RoadContribution road)
    {
        int index = IndexOfReferenceOrValue(roadContributions, road);
        if (index < 0)
        {
            return;
        }

        activeRoadIndex = index;
        PublishStatus();
    }

    public void SelectPreviousRoad()
    {
        if (roadContributions.Count == 0)
        {
            return;
        }

        activeRoadIndex = (activeRoadIndex + roadContributions.Count - 1) % roadContributions.Count;
        PublishStatus();
    }

    public void SelectNextSpecialRail()
    {
        if (specialRailContributions.Count == 0)
        {
            return;
        }

        activeSpecialRailIndex++;
        if (activeSpecialRailIndex >= specialRailContributions.Count)
        {
            activeSpecialRailIndex = -1;
        }

        PublishStatus();
    }

    public void SelectSpecialRail(SpecialRailContribution? rail)
    {
        if (rail is null)
        {
            activeSpecialRailIndex = -1;
            PublishStatus();
            return;
        }

        int index = IndexOfReferenceOrValue(specialRailContributions, rail);
        if (index < 0)
        {
            return;
        }

        activeSpecialRailIndex = index;
        PublishStatus();
    }

    public void SelectPreviousSpecialRail()
    {
        if (specialRailContributions.Count == 0)
        {
            return;
        }

        activeSpecialRailIndex--;
        if (activeSpecialRailIndex < -1)
        {
            activeSpecialRailIndex = specialRailContributions.Count - 1;
        }

        PublishStatus();
    }

    public void ChangeRailBuildLevel(int delta)
    {
        int next = Math.Clamp(activeRailBuildLevel + delta, 0, world.MaxHeightCutLevel);
        if (activeRailBuildLevel == next)
        {
            return;
        }

        activeRailBuildLevel = next;
        if (buildAnchorLocation is { } anchor && ActiveRailKindUsesTargetLevel())
        {
            buildAnchorLocation = anchor with { Z = activeRailBuildLevel };
        }

        PublishStatus();
        InvalidateVisual();
    }

    public void SelectNextStation()
    {
        if (stationContributions.Count == 0)
        {
            return;
        }

        activeStationIndex = (activeStationIndex + 1) % stationContributions.Count;
        PublishStatus();
    }

    public void SelectStation(StationContribution station)
    {
        int index = IndexOfReferenceOrValue(stationContributions, station);
        if (index < 0)
        {
            return;
        }

        activeStationIndex = index;
        PublishStatus();
    }

    public void SelectPreviousStation()
    {
        if (stationContributions.Count == 0)
        {
            return;
        }

        activeStationIndex = (activeStationIndex + stationContributions.Count - 1) % stationContributions.Count;
        PublishStatus();
    }

    public void SelectNextStructure()
    {
        if (structureContributions.Count == 0)
        {
            return;
        }

        activeStructureIndex = (activeStructureIndex + 1) % structureContributions.Count;
        activeStructureFrameIndex = 0;
        activeStructureColorVariantIndex = 0;
        PublishStatus();
    }

    public void SelectStructure(SpriteContribution structure)
    {
        int index = IndexOfReferenceOrValue(structureContributions, structure);
        if (index < 0)
        {
            return;
        }

        activeStructureIndex = index;
        activeStructureFrameIndex = 0;
        activeStructureColorVariantIndex = 0;
        PublishStatus();
    }

    public void SelectPreviousStructure()
    {
        if (structureContributions.Count == 0)
        {
            return;
        }

        activeStructureIndex = (activeStructureIndex + structureContributions.Count - 1) % structureContributions.Count;
        activeStructureFrameIndex = 0;
        activeStructureColorVariantIndex = 0;
        PublishStatus();
    }

    public void CycleStructureVariant()
    {
        if (ActiveStructureContribution is not { } structure || structure.Frames.Count <= 1)
        {
            return;
        }

        activeStructureFrameIndex = (activeStructureFrameIndex + 1) % structure.Frames.Count;
        PublishStatus();
    }

    public void ChangeStructureColorVariant(int delta)
    {
        int count = ActiveStructureColorVariantCount;
        if (count <= 1)
        {
            return;
        }

        activeStructureColorVariantIndex = (activeStructureColorVariantIndex + delta) % count;
        if (activeStructureColorVariantIndex < 0)
        {
            activeStructureColorVariantIndex += count;
        }

        PublishStatus();
        InvalidateVisual();
    }

    public void SelectNextTrain()
    {
        if (trainContributions.Count == 0)
        {
            return;
        }

        activeTrainIndex = (activeTrainIndex + 1) % trainContributions.Count;
        PublishStatus();
    }

    public void SelectTrain(TrainContribution train)
    {
        int index = IndexOfReferenceOrValue(trainContributions, train);
        if (index < 0)
        {
            return;
        }

        activeTrainIndex = index;
        PublishStatus();
    }

    public void SelectPreviousTrain()
    {
        if (trainContributions.Count == 0)
        {
            return;
        }

        activeTrainIndex = (activeTrainIndex + trainContributions.Count - 1) % trainContributions.Count;
        PublishStatus();
    }

    public void StoreSelectedTrainInGarage()
    {
        if (selectedLocation is not { } selected
            || world.GetTrainAt(selected) is not { } train
            || !world.StoreTrainInGarage(train.TrainId))
        {
            return;
        }

        InvalidateVisual();
        PublishStatus();
    }

    public void DispatchSelectedTrainFromGarage()
    {
        if (selectedLocation is not { } selected
            || world.GetTrainAt(selected) is not { } train
            || !world.DispatchTrainFromGarage(train.TrainId, trainCarContributions))
        {
            return;
        }

        InvalidateVisual();
        PublishStatus();
    }

    public void RemoveSelectedTrain()
    {
        if (selectedLocation is not { } selected
            || world.GetTrainAt(selected) is not { } train
            || !world.RemoveTrain(train.TrainId))
        {
            return;
        }

        InvalidateVisual();
        PublishStatus();
    }

    public void ChangePlatformLength(int delta)
    {
        activePlatformLength = Math.Clamp(activePlatformLength + delta, 1, 12);
        PublishStatus();
    }

    public void RotatePlatformDirection()
    {
        activePlatformDirectionIndex = (activePlatformDirectionIndex + 2) % 8;
        PublishStatus();
    }

    public void CyclePlatformStyle()
    {
        activePlatformStyle = activePlatformStyle switch
        {
            PlatformStyle.ThinNoRoof => PlatformStyle.ThinRoof,
            PlatformStyle.ThinRoof => PlatformStyle.Fat,
            _ => PlatformStyle.ThinNoRoof
        };
        PublishStatus();
    }

    public void AdvanceClock(long minutes)
    {
        world.AdvanceClock(minutes);
        PublishStatus();
    }

    private void DrawSpriteFrame(DrawingContext context, SpriteFrame frame, Point tilePoint, int colorVariantIndex = 0)
    {
        if (!frame.IsLoadable)
        {
            return;
        }

        Bitmap bitmap = GetPluginBitmap(frame, colorVariantIndex);
        Size imageSize = bitmap.Size;
        double sourceX = Math.Clamp(frame.SourceX, 0, Math.Max(0, imageSize.Width - 1));
        double sourceY = Math.Clamp(frame.SourceY, 0, Math.Max(0, imageSize.Height - 1));
        double sourceWidth = Math.Min(frame.SourceWidth, imageSize.Width - sourceX);
        double sourceHeight = Math.Min(frame.SourceHeight, imageSize.Height - sourceY);

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        Rect source = new(sourceX, sourceY, sourceWidth, sourceHeight);
        Point targetPoint = new(tilePoint.X - frame.OffsetX, tilePoint.Y - frame.OffsetY);
        context.DrawImage(bitmap, source, new Rect(targetPoint, source.Size));
    }

    private SpriteFrame CreateRailRoadCrossingFrame(ModernRailRoadCrossingOrientation orientation, bool behindRail)
    {
        int orientationIndex = orientation == ModernRailRoadCrossingOrientation.RailNorthSouth ? 0 : 1;
        int layerIndex = behindRail ? 1 : 0;
        int sourceColumn = (orientationIndex == 0 ? 2 : 0) + layerIndex;
        return new SpriteFrame(
            RailRoadCrossingPictureId,
            "crossing.bmp",
            railRoadCrossingPath!,
            sourceColumn * 32,
            0,
            32,
            32,
            0,
            16,
            Array.Empty<ColorMapEntry>(),
            null);
    }

    private void DrawSpriteSet3D(DrawingContext context, ModernSpriteSet3D spriteSet, Point basePoint, int colorVariantIndex = 0)
    {
        foreach (ModernSpriteVoxel3D voxel in spriteSet.InVoxelDrawOrder())
        {
            Point voxelPoint = new(
                basePoint.X + (voxel.X + voxel.Y) * 16,
                basePoint.Y + (-voxel.X + voxel.Y) * 8 - voxel.Z * 16);
            DrawSpriteFrame(context, voxel.Frame, voxelPoint, colorVariantIndex);
        }
    }

    private void DrawSpriteSet2D(DrawingContext context, ModernSpriteSet2D spriteSet, Point basePoint, int colorVariantIndex = 0)
    {
        foreach (ModernSpriteVoxel2D voxel in spriteSet.InVoxelDrawOrder())
        {
            Point voxelPoint = new(
                basePoint.X + (voxel.X + voxel.Y) * 16,
                basePoint.Y + (-voxel.X + voxel.Y) * 8);
            DrawSpriteFrame(context, voxel.Frame, voxelPoint, colorVariantIndex);
        }
    }

    private void DrawPlatformTile(DrawingContext context, ModernPlatform platform, int index, Point tilePoint)
    {
        if (platform.Style == PlatformStyle.Fat)
        {
            int sourceColumn = platform.Direction.IsParallelToY ? 0 : 1;
            Rect source = new(sourceColumn * 32, 0, 32, 32);
            Rect target = new(new Point(tilePoint.X, tilePoint.Y - 16), source.Size);
            context.DrawImage(fatPlatformBitmap, source, target);
            return;
        }

        bool hasRoof = platform.Style == PlatformStyle.ThinRoof
            && platform.Length / 4 <= index
            && index < platform.Length - platform.Length / 4;
        int thinSourceColumn = platform.Direction.Index + (hasRoof ? 1 : 0);
        Rect thinSource = new(thinSourceColumn * 32, 0, 32, 24);
        Rect thinTarget = new(new Point(tilePoint.X, tilePoint.Y - 8), thinSource.Size);
        context.DrawImage(thinPlatformBitmap, thinSource, thinTarget);
    }

    private static bool PlatformDrawsAfterTrain(ModernPlatform platform)
    {
        return platform.Style != PlatformStyle.Fat
            && (platform.Direction == ModernDirection.West || platform.Direction == ModernDirection.North);
    }

    private static int SpecialRailSourceColumn(MapRailObject railObject)
    {
        ModernDirection direction = DirectionFromRailMask(railObject.Pattern.DirectionMask);
        return railObject.SpecialKind switch
        {
            ModernSpecialRailKind.Bridge => BridgeRailColumn(direction),
            ModernSpecialRailKind.SteelSupported => direction.Index switch
            {
                0 or 4 => 3,
                1 or 5 => 1,
                2 or 6 => 0,
                3 or 7 => 2,
                _ => 0
            },
            ModernSpecialRailKind.Tunnel => direction.IsParallelToY ? 1 : 0,
            ModernSpecialRailKind.Garage => direction.IsParallelToX ? 0 : 1,
            _ => 0
        };
    }

    private static int BridgeRailColumn(ModernDirection direction)
    {
        return direction.Index switch
        {
            0 => 4,
            4 => 4,
            2 => 1,
            6 => 1,
            _ => direction.IsParallelToX ? 1 : 4
        };
    }

    private static ModernDirection DirectionFromRailMask(byte mask)
    {
        for (int i = 0; i < 8; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                return ModernDirection.FromIndex(i);
            }
        }

        return ModernDirection.East;
    }

    private void DrawForest(DrawingContext context, ForestSpriteSet forest, int h, int v, Point tilePoint)
    {
        if (forest.Ground is { IsLoadable: true } ground)
        {
            DrawSpriteFrame(context, ground, tilePoint);
        }

        IReadOnlyList<ForestTreePattern> patterns = GetForestPatterns(forest, h, v);
        foreach (ForestTreePattern pattern in patterns)
        {
            SpriteFrame sprite = forest.TreeSprites[pattern.SpriteIndex];
            DrawSpriteFrame(context, sprite, new Point(tilePoint.X + pattern.OffsetX, tilePoint.Y + pattern.OffsetY));
        }
    }

    private IReadOnlyList<ForestTreePattern> GetForestPatterns(ForestSpriteSet forest, int h, int v)
    {
        string forestId = forest.TreeSprites.FirstOrDefault()?.ResolvedPath ?? "";
        (string ForestId, int H, int V) key = (forestId, h, v);
        if (!forestPatternCache.TryGetValue(key, out IReadOnlyList<ForestTreePattern>? patterns))
        {
            patterns = CreateForestPatterns(forest, h, v);
            forestPatternCache[key] = patterns;
        }

        return patterns;
    }

    private void AddGeneratedTreeScatter(ModernWorld target, ModernWorldTerrainKind terrainKind)
    {
        if (terrainKind == ModernWorldTerrainKind.Flat)
        {
            return;
        }

        LandContribution? forest = landContributions
            .Where(land => land.Kind == LandContributionKind.Forest && land.IsLoadable)
            .OrderByDescending(land => land.PluginDirectoryName.Contains("forest", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(land => land.Forest?.Density ?? 0)
            .FirstOrDefault();
        if (forest is null)
        {
            return;
        }

        int maxTrees = Math.Clamp(target.Width * target.Height / 7, 32, 2_400);
        List<GeneratedTreeCandidate> candidates = new();
        for (int v = 0; v < target.Height; v++)
        {
            for (int h = 0; h < target.Width; h++)
            {
                TerrainTilePreview terrain = target.GetTerrainTile(h, v);
                if (!terrain.IsFlat || !IsDryLevel(target, terrain.SurfaceLevel))
                {
                    continue;
                }

                double score = ForestSuitability(target.Name, h, v);
                int tieBreaker = StableHash($"{target.Name}:generated-tree-order", h, v);
                candidates.Add(new GeneratedTreeCandidate(h, v, terrain.SurfaceLevel, score, tieBreaker));
            }
        }

        foreach (GeneratedTreeCandidate candidate in candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.TieBreaker)
            .Take(maxTrees))
        {
            target.AddEntity(ModernPlacedEntity.Land(candidate.H, candidate.V, candidate.Z, forest));
        }
    }

    private static double ForestSuitability(string worldName, int h, int v)
    {
        int broadSeed = StableHash($"{worldName}:generated-tree-broad", 17, 43);
        int midSeed = StableHash($"{worldName}:generated-tree-mid", 71, 29);
        double broad = FractalForestNoise(h * 0.045 + 12.5, v * 0.045 - 8.75, broadSeed, 4, 0.54);
        double mid = FractalForestNoise(h * 0.13 - 21.0, v * 0.13 + 35.0, midSeed, 3, 0.50);
        double jitter = PositiveModulo(StableHash($"{worldName}:generated-tree-jitter", h, v), 10_000) / 10_000.0;
        return broad * 0.62 + mid * 0.25 + jitter * 0.13;
    }

    private static double FractalForestNoise(double h, double v, int seed, int octaves, double persistence)
    {
        double total = 0;
        double amplitude = 1;
        double frequency = 1;
        double amplitudeTotal = 0;

        for (int octave = 0; octave < octaves; octave++)
        {
            total += ForestValueNoise(h * frequency, v * frequency, seed + octave * 1013) * amplitude;
            amplitudeTotal += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }

        return amplitudeTotal <= 0 ? 0 : total / amplitudeTotal;
    }

    private static double ForestValueNoise(double h, double v, int seed)
    {
        int h0 = (int)Math.Floor(h);
        int v0 = (int)Math.Floor(v);
        double tx = SmoothStep(h - h0);
        double ty = SmoothStep(v - v0);

        double a = ForestRandomUnit(h0, v0, seed);
        double b = ForestRandomUnit(h0 + 1, v0, seed);
        double c = ForestRandomUnit(h0, v0 + 1, seed);
        double d = ForestRandomUnit(h0 + 1, v0 + 1, seed);
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    private static double ForestRandomUnit(int h, int v, int seed)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)h) * 16777619u;
            hash = (hash ^ (uint)v) * 16777619u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            hash ^= hash >> 16;
            return hash / (double)uint.MaxValue;
        }
    }

    private static double Lerp(double a, double b, double amount)
    {
        return a + (b - a) * amount;
    }

    private static double SmoothStep(double value)
    {
        return value * value * (3 - 2 * value);
    }

    private Bitmap GetPluginBitmap(SpriteFrame frame, int colorVariantIndex = 0)
    {
        string key = BitmapCacheKey(frame, colorVariantIndex);
        if (!pluginBitmaps.TryGetValue(key, out Bitmap? bitmap))
        {
            bitmap = LegacyBitmap.LoadWithColorKey(frame.ResolvedPath, frame.ColorMaps, colorVariantIndex);
            pluginBitmaps[key] = bitmap;
        }

        return bitmap;
    }

    private static string BitmapCacheKey(SpriteFrame frame, int colorVariantIndex)
    {
        return frame.ColorMaps.Count == 0
            ? frame.ResolvedPath
            : $"{frame.ResolvedPath}|{colorVariantIndex}|{string.Join(";", frame.ColorMaps.Select(map => $"{map.From}>{map.To}"))}";
    }

    private HashSet<(int H, int V)> CreateInitialOccupiedTiles()
    {
        HashSet<(int H, int V)> occupied = new(world.Transport.RailTiles.Select(tile => (tile.H, tile.V)));
        foreach (KeyValuePair<(int H, int V), RoadContribution> road in world.Transport.RoadTiles)
        {
            occupied.Add(road.Key);
        }

        return occupied;
    }

    private void AddSampleStructureEntities(PluginManifestCatalog plugins, HashSet<(int H, int V)> occupied)
    {
        (int H, int V)[] positions =
        {
            (5, 6),
            (10, 7),
            (15, 7),
            (20, 8),
            (25, 9),
            (6, 12),
            (12, 13),
            (18, 14),
            (24, 15),
            (8, 20),
            (14, 21),
            (20, 22),
            (26, 21),
            (4, 25),
            (11, 27),
            (18, 27),
            (25, 27)
        };

        SpriteContribution[] candidates = SelectFixedStructureSamples(plugins, positions.Length);

        for (int i = 0; i < candidates.Length; i++)
        {
            SpriteFrame? frame = candidates[i].Frames.FirstOrDefault(candidate => candidate.IsLoadable);
            if (frame is null)
            {
                continue;
            }

            (int h, int v) = positions[i];
            TileLocation? location = FindFlatDryLocation(h, v, candidates[i], frame, occupied);
            if (location is { } placed)
            {
                ModernPlacedEntity entity = ModernPlacedEntity.Structure(placed.H, placed.V, placed.Z, candidates[i], frame);
                if (world.AddEntity(entity))
                {
                    MarkOccupied(placed.H, placed.V, candidates[i], occupied);
                }
            }
        }
    }

    private void AddSampleRailService()
    {
        StationContribution? station = stationContributions
            .OrderBy(candidate => Math.Max(1, candidate.SizeH) * Math.Max(1, candidate.SizeV))
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (station is not null)
        {
            TryAddSampleStation(station, 12, 13, "Loop North");
            TryAddSampleStation(station, 21, 22, "Loop South");
        }

        TryAddSamplePlatform("north", 12, 11, ModernDirection.East, 5, PlatformStyle.ThinRoof);
        TryAddSamplePlatform("south", 21, 20, ModernDirection.West, 5, PlatformStyle.ThinRoof);

        TrainContribution? train = trainContributions.FirstOrDefault(candidate => candidate.CreateCarIds(3).All(trainCarContributions.ContainsKey));
        if (train is not null)
        {
            string trainId = "train:sample";
            world.AddTrain(new ModernTrain(trainId, train, Array.Empty<ModernTrainCarPlacement>(), 0));
            world.PlaceTrain(trainId, new ModernVoxelKey(14, 7, world.GetGroundLevel(14, 7)), ModernDirection.East, trainCarContributions);
        }
    }

    private void TryAddSampleStation(StationContribution station, int h, int v, string name)
    {
        int z = world.GetGroundLevel(h, v);
        world.AddStation(new ModernStation($"station:sample:{h}:{v}", h, v, z, station) { Name = name });
    }

    private void TryAddSamplePlatform(string id, int h, int v, ModernDirection direction, int length, PlatformStyle style)
    {
        int z = world.GetGroundLevel(h, v);
        world.AddPlatform(new ModernPlatform($"platform:sample:{id}", h, v, z, direction.Index, length, style, null));
    }

    private void AddSampleLandEntities(PluginManifestCatalog plugins, HashSet<(int H, int V)> occupied)
    {
        Dictionary<string, LandContribution> staticLands = plugins.Lands
            .Where(land => land.Kind == LandContributionKind.Static && land.StaticSprite?.IsLoadable == true)
            .GroupBy(land => land.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        LandContribution? forest = plugins.Lands
            .Where(land => land.Kind == LandContributionKind.Forest && land.IsLoadable)
            .OrderByDescending(land => land.PluginDirectoryName.Contains("forest", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(land => land.Forest?.Density ?? 0)
            .FirstOrDefault();

        LandContribution? random = plugins.Lands
            .Where(land => land.Kind == LandContributionKind.Random && land.RandomLandIds.Any(staticLands.ContainsKey))
            .OrderByDescending(land => land.RandomLandIds.Count)
            .FirstOrDefault();

        LandContribution? basicStatic = staticLands.Values
            .OrderBy(land => land.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(land => land.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (forest is not null)
        {
            AddLandPatch(occupied, forest, staticLands, 3, 18, 7, 6);
            AddLandPatch(occupied, forest, staticLands, 23, 4, 5, 5);
        }

        if (random is not null)
        {
            AddLandPatch(occupied, random, staticLands, 18, 18, 7, 4);
        }

        if (basicStatic is not null)
        {
            AddLandPatch(occupied, basicStatic, staticLands, 7, 28, 6, 3);
        }
    }

    private void AddLandPatch(
        HashSet<(int H, int V)> occupied,
        LandContribution land,
        IReadOnlyDictionary<string, LandContribution> staticLands,
        int preferredH,
        int preferredV,
        int width,
        int height)
    {
        (preferredH, preferredV) = FindLandPatchOrigin(preferredH, preferredV, width, height, occupied)
            ?? (preferredH, preferredV);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int h = preferredH + x;
                int v = preferredV + y;
                if (h < 0 || h >= world.Width || v < 0 || v >= world.Height || occupied.Contains((h, v)))
                {
                    continue;
                }

                TerrainTilePreview terrain = world.GetTerrainTile(h, v);
                if (!terrain.IsFlat || !IsDryLevel(terrain.SurfaceLevel))
                {
                    continue;
                }

                LandContribution? resolved = null;
                if (land.Kind == LandContributionKind.Random)
                {
                    string? id = SelectRandomLandId(land, h, v, staticLands);
                    if (id is null || !staticLands.TryGetValue(id, out resolved))
                    {
                        continue;
                    }
                }

                ModernPlacedEntity entity = ModernPlacedEntity.Land(h, v, terrain.SurfaceLevel, land, resolved);
                if (world.AddEntity(entity))
                {
                    occupied.Add((h, v));
                }
            }
        }
    }

    private (int H, int V)? FindLandPatchOrigin(
        int preferredH,
        int preferredV,
        int width,
        int height,
        HashSet<(int H, int V)> occupied)
    {
        (int H, int V)? best = null;
        int bestScore = -1;
        int bestDistance = int.MaxValue;

        for (int v = 0; v <= world.Height - height; v++)
        {
            for (int h = 0; h <= world.Width - width; h++)
            {
                int score = CountUsableLandTiles(h, v, width, height, occupied);
                if (score == 0)
                {
                    continue;
                }

                int distance = Math.Abs(h - preferredH) + Math.Abs(v - preferredV);
                if (score > bestScore || (score == bestScore && distance < bestDistance))
                {
                    best = (h, v);
                    bestScore = score;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private int CountUsableLandTiles(
        int startH,
        int startV,
        int width,
        int height,
        HashSet<(int H, int V)> occupied)
    {
        int count = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int h = startH + x;
                int v = startV + y;
                if (occupied.Contains((h, v)))
                {
                    continue;
                }

                TerrainTilePreview terrain = world.GetTerrainTile(h, v);
                if (terrain.IsFlat && IsDryLevel(terrain.SurfaceLevel))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static string? SelectRandomLandId(
        LandContribution land,
        int h,
        int v,
        IReadOnlyDictionary<string, LandContribution> staticLands)
    {
        string[] available = land.RandomLandIds
            .Where(staticLands.ContainsKey)
            .ToArray();
        if (available.Length == 0)
        {
            return null;
        }

        int index = Math.Abs(StableHash(land.Id, h, v)) % available.Length;
        return available[index];
    }

    private static SpriteContribution[] SelectFixedStructureSamples(PluginManifestCatalog plugins, int count)
    {
        SpriteContribution[] fixedStructures = plugins.Sprites
            .Where(sprite => sprite.SpriteSet3D?.IsLoadable == true)
            .OrderBy(sprite => sprite.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<SpriteContribution> selected = new();
        AddRepresentative(fixedStructures, selected, sprite => sprite.SizeX == 1 && sprite.SizeY == 1);
        AddRepresentative(fixedStructures, selected, sprite => sprite.SizeX >= 2 && sprite.SizeY >= 2 && sprite.Height <= 2);
        AddRepresentative(fixedStructures, selected, sprite => sprite.SizeX > sprite.SizeY);
        AddRepresentative(fixedStructures, selected, sprite => sprite.SizeY > sprite.SizeX);
        AddRepresentative(fixedStructures, selected, sprite => sprite.Height >= 5);
        AddRepresentative(fixedStructures, selected, sprite => sprite.SizeX >= 5 || sprite.SizeY >= 5);

        foreach (SpriteContribution sprite in fixedStructures
            .OrderByDescending(sprite => sprite.SizeX * sprite.SizeY)
            .ThenByDescending(sprite => sprite.Height))
        {
            if (selected.Count >= count)
            {
                break;
            }

            if (!selected.Contains(sprite))
            {
                selected.Add(sprite);
            }
        }

        return selected.Take(count).ToArray();
    }

    private static void AddRepresentative(
        IReadOnlyList<SpriteContribution> candidates,
        List<SpriteContribution> selected,
        Func<SpriteContribution, bool> predicate)
    {
        SpriteContribution? match = candidates
            .Where(predicate)
            .OrderByDescending(sprite => sprite.SizeX * sprite.SizeY * Math.Max(1, sprite.Height))
            .FirstOrDefault();
        if (match is not null && !selected.Contains(match))
        {
            selected.Add(match);
        }
    }

    private TileLocation? FindFlatDryLocation(
        int preferredH,
        int preferredV,
        SpriteContribution contribution,
        SpriteFrame frame,
        HashSet<(int H, int V)> occupied)
    {
        TileLocation? best = null;
        int bestDistance = int.MaxValue;
        int footprintX = Math.Max(1, contribution.SpriteSet3D?.SizeX ?? contribution.SizeX);
        int footprintY = Math.Max(1, contribution.SpriteSet3D?.SizeY ?? contribution.SizeY);

        for (int v = 0; v < world.Height; v++)
        {
            for (int h = 0; h < world.Width; h++)
            {
                if (!CanPlaceFootprint(h, v, footprintX, footprintY, occupied, out TerrainTilePreview terrain))
                {
                    continue;
                }

                Point screen = FromHvzToScreen(h, v, terrain.SurfaceLevel);
                if (screen.X - frame.OffsetX < 0 || screen.Y - frame.OffsetY < 0)
                {
                    continue;
                }

                int distance = Math.Abs(h - preferredH) + Math.Abs(v - preferredV);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new TileLocation(h, v, terrain.SurfaceLevel);
                }
            }
        }

        return best;
    }

    private bool CanPlaceFootprint(
        int h,
        int v,
        int footprintX,
        int footprintY,
        HashSet<(int H, int V)> occupied,
        out TerrainTilePreview baseTerrain)
    {
        baseTerrain = world.GetTerrainTile(h, v);
        if (!baseTerrain.IsFlat || !IsDryLevel(baseTerrain.SurfaceLevel))
        {
            return false;
        }

        for (int y = 0; y < footprintY; y++)
        {
            for (int x = 0; x < footprintX; x++)
            {
                int tileH = h + x;
                int tileV = v + y;
                if (tileH < 0 || tileH >= world.Width || tileV < 0 || tileV >= world.Height || occupied.Contains((tileH, tileV)))
                {
                    return false;
                }

                TerrainTilePreview terrain = world.GetTerrainTile(tileH, tileV);
                if (!terrain.IsFlat || terrain.SurfaceLevel != baseTerrain.SurfaceLevel || !IsDryLevel(terrain.SurfaceLevel))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void MarkOccupied(int h, int v, SpriteContribution contribution, HashSet<(int H, int V)> occupied)
    {
        int footprintX = Math.Max(1, contribution.SpriteSet3D?.SizeX ?? contribution.SizeX);
        int footprintY = Math.Max(1, contribution.SpriteSet3D?.SizeY ?? contribution.SizeY);
        for (int y = 0; y < footprintY; y++)
        {
            for (int x = 0; x < footprintX; x++)
            {
                occupied.Add((h + x, v + y));
            }
        }
    }

    private static int SpriteTypePriority(string type)
    {
        if (type.Contains("structure", StringComparison.OrdinalIgnoreCase)) return 0;
        if (type.Contains("commercial", StringComparison.OrdinalIgnoreCase)) return 1;
        if (type.Contains("pole", StringComparison.OrdinalIgnoreCase)) return 2;
        if (type.Contains("train", StringComparison.OrdinalIgnoreCase)) return 3;
        return 4;
    }

    private static IReadOnlyList<ForestTreePattern> CreateForestPatterns(ForestSpriteSet forest, int h, int v)
    {
        if (forest.Density <= 0 || forest.TreeSprites.Count == 0)
        {
            return Array.Empty<ForestTreePattern>();
        }

        Random random = new(StableHash("forest", h, v));
        int count = 0;
        for (int i = 0; i < forest.Density * 2; i++)
        {
            if (random.Next(2) == 0)
            {
                count++;
            }
        }

        ForestTreePattern[] patterns = new ForestTreePattern[count];
        for (int i = 0; i < count; i++)
        {
            int x = random.Next(16);
            int y = random.Next(16);
            patterns[i] = new ForestTreePattern(
                x + y,
                ((-x + y) >> 1) + 8,
                random.Next(forest.TreeSprites.Count));
        }

        for (int i = 0; i < patterns.Length - 1; i++)
        {
            for (int j = i + 1; j < patterns.Length; j++)
            {
                if (patterns[i].OffsetY <= patterns[j].OffsetY)
                {
                    continue;
                }

                int offsetX = patterns[i].OffsetX;
                int offsetY = patterns[i].OffsetY;
                patterns[i] = patterns[i] with
                {
                    OffsetX = patterns[j].OffsetX,
                    OffsetY = patterns[j].OffsetY
                };
                patterns[j] = patterns[j] with
                {
                    OffsetX = offsetX,
                    OffsetY = offsetY
                };
            }
        }

        return patterns;
    }

    private static int StableHash(string value, int h, int v)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in value)
            {
                hash = hash * 31 + c;
            }

            hash = hash * 31 + h;
            hash = hash * 31 + v;
            return hash == int.MinValue ? 0 : hash;
        }
    }

    private static int PositiveModulo(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private Point FromHvzToScreen(int h, int v, int z)
    {
        int projectedV = v - z * 2;
        return new Point(MapOriginX + 16 * (2 * h + (projectedV & 1)), MapOriginY + 8 * projectedV);
    }

    private int GetTerrainVisualLevel(TerrainTilePreview terrain)
    {
        if (MaxVisibleLevel < terrain.BaseLevel)
        {
            return MaxVisibleLevel;
        }

        if (IsWaterLevel(terrain.BaseLevel) && MaxVisibleLevel >= world.WaterLevel)
        {
            return world.WaterLevel;
        }

        return terrain.BaseLevel;
    }

    private bool IsFullyUnderwater(TerrainTilePreview terrain)
    {
        return world.WaterLevel > 0
            && (terrain.BaseLevel * 4 + terrain.MaxCornerHeight) <= world.WaterLevel * 4;
    }

    private bool IsWaterLevel(int surfaceLevel)
    {
        return IsWaterLevel(world, surfaceLevel);
    }

    private static bool IsWaterLevel(ModernWorld target, int surfaceLevel)
    {
        return target.WaterLevel > 0 && surfaceLevel <= target.WaterLevel;
    }

    private bool IsDryLevel(int surfaceLevel)
    {
        return IsDryLevel(world, surfaceLevel);
    }

    private static bool IsDryLevel(ModernWorld target, int surfaceLevel)
    {
        return target.WaterLevel <= 0 || surfaceLevel > target.WaterLevel;
    }

    private IEnumerable<(int H, int V)> EnumerateVisibleTiles(Rect visibleMapRect, double horizontalMargin, double verticalMargin)
    {
        Rect rect = ExpandRect(visibleMapRect, horizontalMargin, verticalMargin);
        int maxRenderLevel = Math.Max(MaxVisibleLevel, MaxVisibleLevel >= world.WaterLevel ? world.WaterLevel : 0);
        int minH = Math.Max(0, (int)Math.Floor((rect.Left - MapOriginX - 32) / 32) - 1);
        int maxH = Math.Min(world.Width - 1, (int)Math.Ceiling((rect.Right - MapOriginX + 32) / 32) + 1);
        int minV = Math.Max(0, (int)Math.Floor((rect.Top - MapOriginY) / 8) - 2);
        int maxV = Math.Min(world.Height - 1, (int)Math.Ceiling((rect.Bottom - MapOriginY) / 8) + maxRenderLevel * 2 + 2);

        for (int v = minV; v <= maxV; v++)
        {
            for (int h = minH; h <= maxH; h++)
            {
                yield return (h, v);
            }
        }
    }

    private bool IsTilePotentiallyVisible(
        int h,
        int v,
        int z,
        Rect visibleMapRect,
        double leftMargin,
        double topMargin,
        double rightMargin,
        double bottomMargin)
    {
        Point p = FromHvzToScreen(h, v, z);
        return p.X >= visibleMapRect.Left - leftMargin
            && p.X <= visibleMapRect.Right + rightMargin
            && p.Y >= visibleMapRect.Top - topMargin
            && p.Y <= visibleMapRect.Bottom + bottomMargin;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        TileLocation? location = PickTile(e.GetPosition(this));
        if (hoverLocation != location)
        {
            hoverLocation = location;
            PublishStatus();
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        PointerPoint point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            selectedLocation = PickTile(point.Position);
            if (selectedLocation is { } location)
            {
                ApplyEdit(location);
            }

            PublishStatus();
            InvalidateVisual();
            e.Handled = true;
        }
        else if (point.Properties.IsRightButtonPressed && PickTile(point.Position) is { } location)
        {
            selectedLocation = location;
            world.RemoveTransportAt(location);
            if (buildAnchorLocation is { } anchor && anchor.H == location.H && anchor.V == location.V)
            {
                buildAnchorLocation = null;
            }

            PublishStatus();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            Zoom += e.Delta.Y > 0 ? 0.25 : -0.25;
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (EditMode == MapEditMode.Rail && e.Key is Key.PageUp or Key.PageDown)
        {
            ChangeRailBuildLevel(e.Key == Key.PageUp ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape || EditMode != MapEditMode.Rail || buildAnchorLocation is null)
        {
            return;
        }

        buildAnchorLocation = null;
        lastMessage = "Rail construction canceled.";
        PublishStatus();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        hoverLocation = null;
        PublishStatus();
        InvalidateVisual();
    }

    private TileLocation? PickTile(Point controlPoint)
    {
        Point mapPoint = new(controlPoint.X / Zoom, controlPoint.Y / Zoom);
        TileLocation? ground = PickRenderedTile(mapPoint);
        return ground is null ? null : SelectNearestTerrainCorner(ground.Value, mapPoint);
    }

    private TileLocation? PickRenderedTile(Point mapPoint)
    {
        TileLocation? best = null;
        int bestDrawOrder = int.MinValue;
        int maxPickLevel = Math.Max(MaxVisibleLevel, MaxVisibleLevel >= world.WaterLevel ? world.WaterLevel : 0);

        for (int z = maxPickLevel; z >= 0; z--)
        {
            double vAtPoint = (mapPoint.Y - MapOriginY) / 8.0 + z * 2;
            int centerV = (int)Math.Floor(vAtPoint);
            for (int v = centerV - 2; v <= centerV + 2; v++)
            {
                if (v < 0 || v >= world.Height)
                {
                    continue;
                }

                int projectedV = v - z * 2;
                double hAtPoint = (mapPoint.X - MapOriginX - ((projectedV & 1) != 0 ? 16 : 0)) / 32.0;
                int centerH = (int)Math.Floor(hAtPoint);
                for (int h = centerH - 1; h <= centerH + 1; h++)
                {
                    if (!world.IsInside(h, v))
                    {
                        continue;
                    }

                    TerrainTilePreview terrain = world.GetTerrainTile(h, v);
                    int visualLevel = GetTerrainVisualLevel(terrain);
                    if (visualLevel != z)
                    {
                        continue;
                    }

                    Point tilePoint = FromHvzToScreen(h, v, visualLevel);
                    if (!ContainsDiamond(mapPoint, tilePoint))
                    {
                        continue;
                    }

                    int drawOrder = v * world.Width + h;
                    if (drawOrder > bestDrawOrder)
                    {
                        bestDrawOrder = drawOrder;
                        best = new TileLocation(h, v, visualLevel);
                    }
                }
            }
        }

        return best;
    }

    private static bool ContainsDiamond(Point point, Point tilePoint)
    {
        double dx = Math.Abs(point.X - (tilePoint.X + 16));
        double dy = Math.Abs(point.Y - (tilePoint.Y + 8));
        return dx / 16.0 + dy / 8.0 <= 1.0;
    }

    private TileLocation SelectNearestTerrainCorner(TileLocation selected, Point mapPoint)
    {
        TerrainCorner bestCorner = TerrainCorner.Top;
        double bestDistance = double.MaxValue;
        foreach (TerrainCorner corner in Enum.GetValues<TerrainCorner>())
        {
            Point cornerPoint = GetTerrainCornerScreenPoint(selected with { Corner = corner });
            double dx = cornerPoint.X - mapPoint.X;
            double dy = cornerPoint.Y - mapPoint.Y;
            double distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCorner = corner;
            }
        }

        return selected with { Corner = bestCorner };
    }

    private Point GetTerrainCornerScreenPoint(TileLocation location)
    {
        TerrainTilePreview terrain = world.GetTerrainTile(location.H, location.V);
        int visualLevel = GetTerrainVisualLevel(terrain);
        Point p = FromHvzToScreen(location.H, location.V, visualLevel);
        bool usesTerrainProfile = visualLevel == terrain.BaseLevel && IsDryLevel(terrain.BaseLevel);
        int cornerHeight = usesTerrainProfile ? GetCornerHeight(terrain, location.Corner) : 0;

        Point basePoint = location.Corner switch
        {
            TerrainCorner.Top => new Point(p.X + 16, p.Y),
            TerrainCorner.Right => new Point(p.X + 32, p.Y + 8),
            TerrainCorner.Bottom => new Point(p.X + 16, p.Y + 16),
            TerrainCorner.Left => new Point(p.X, p.Y + 8),
            _ => new Point(p.X + 16, p.Y)
        };

        return new Point(basePoint.X, basePoint.Y - cornerHeight * 4);
    }

    private static int GetCornerHeight(TerrainTilePreview terrain, TerrainCorner corner)
    {
        return corner switch
        {
            TerrainCorner.Top => terrain.Top,
            TerrainCorner.Right => terrain.Right,
            TerrainCorner.Bottom => terrain.Bottom,
            TerrainCorner.Left => terrain.Left,
            _ => terrain.Top
        };
    }

    private void PublishStatus()
    {
        StatusChanged?.Invoke(CreateStatus());
    }

    private MapViewportStatus CreateStatus()
    {
        string hint = CreateInteractionHint();
        ModernRailwayTileInspection? selection = selectedLocation is { } selected
            ? world.InspectRailwayAt(selected)
            : null;
        (string selectionTitle, string selectionDetail) = CreateSelectionSummary(selection);
        ModernTrain? selectedTrain = selection?.Train;
        return new MapViewportStatus(
            world.Name,
            hoverLocation,
            selectedLocation,
            buildAnchorLocation,
            EditMode,
            ActiveRailName,
            ActiveRoadName,
            ActiveStationName,
            ActiveStructureName,
            ActivePlatformDescription,
            ActiveTrainName,
            world.Clock,
            world.Account.Cash,
            world.Account.TotalDebt,
            world.Entities.Count + world.Stations.Count + world.Platforms.Count + world.Trains.Count,
            world.TrafficVoxels.Count,
            world.Transport.RailTiles.Count,
            world.Transport.RoadTiles.Count,
            world.Cars.Count,
            world.Stations.Count,
            world.Platforms.Count,
            world.Trains.Count,
            world.TotalStationPopulation,
            world.TotalWaitingPassengers,
            world.TotalLoadedPassengersToday,
            world.TotalUnloadedPassengersToday,
            world.TotalTrainStopsToday,
            Zoom,
            MaxVisibleLevel,
            world.MaxHeightCutLevel,
            ShowGrid,
            UseNightView,
            selectionTitle,
            selectionDetail,
            selectedTrain is not null && world.CanStoreTrainInGarage(selectedTrain.TrainId),
            selectedTrain is not null && world.CanDispatchTrainFromGarage(selectedTrain.TrainId, trainCarContributions),
            selectedTrain is not null,
            hint,
            lastMessage);
    }

    private static (string Title, string Detail) CreateSelectionSummary(ModernRailwayTileInspection? selection)
    {
        if (selection is null)
        {
            return ("Selection", "No tile selected.");
        }

        List<string> details = new();
        if (selection.Train is { } train)
        {
            details.Add($"Train: {train.Contribution.DisplayName}\nState: {FormatTrainState(train.State)}\nPassengers: {train.PassengerCount:N0}/{train.EffectivePassengerCapacity:N0}");
        }

        if (selection.Station is { } station)
        {
            details.Add($"Station: {station.Name}\nBuilding: {station.Contribution.DisplayName}\nTrains today: {station.Stats.TrainsToday:N0}\nLoaded/unloaded: {station.Stats.LoadedToday:N0}/{station.Stats.UnloadedToday:N0}");
        }

        if (selection.Platform is { } platform)
        {
            string stationLink = string.IsNullOrWhiteSpace(platform.StationId) ? "unassigned" : "linked";
            details.Add($"Platform: {platform.Direction.EnglishName}, {platform.Length} tile(s)\nStyle: {FormatPlatformStyle(platform.Style)}\nStation link: {stationLink}");
        }

        if (selection.RailRoad is { } railRoad)
        {
            string railKind = selection.SpecialRailKind == ModernSpecialRailKind.Normal
                ? "standard rail"
                : $"{selection.SpecialRailKind} rail";
            details.Add($"Rail: {railKind}\nShape: {railRoad.Kind}\nDirections: {string.Join("/", railRoad.Directions.Select(direction => direction.EnglishName))}");
        }

        if (details.Count == 0)
        {
            return ("Selection", $"Tile {selection.Location.H},{selection.Location.V},{selection.Location.Z}");
        }

        string title = selection.Train?.Contribution.DisplayName
            ?? selection.Station?.Name
            ?? (selection.Platform is not null ? "Platform" : "Railway");
        return (title, string.Join("\n\n", details));
    }

    private static string FormatTrainState(ModernTrainState state)
    {
        return state switch
        {
            ModernTrainState.Unplaced => "unplaced",
            ModernTrainState.Moving => "moving",
            ModernTrainState.StoppingAtStation => "stopping",
            ModernTrainState.EmergencyStopping => "emergency stop",
            ModernTrainState.InGarage => "in garage",
            _ => state.ToString()
        };
    }

    private string CreateInteractionHint()
    {
        if (hoverLocation is { } hover && ToolUsesAnchor && EditMode != MapEditMode.Rail && !world.IsBuildableSurface(hover))
        {
            return "This tile is not buildable. Pick flat dry land or erase an obstruction first.";
        }

        return EditMode switch
        {
            MapEditMode.Rail when buildAnchorLocation is null && ActiveRailKindUsesTargetLevel() => $"Rail: PageUp/PageDown or Z buttons set target level {activeRailBuildLevel}; click to start {ActiveRailName}.",
            MapEditMode.Rail when ActiveRailKindUsesTargetLevel() => $"Rail: PageUp/PageDown or Z buttons set target level {activeRailBuildLevel}; click a destination to place {ActiveRailName}.",
            MapEditMode.Rail when buildAnchorLocation is null => $"Rail: click a buildable tile to set the start point for {ActiveRailName}.",
            MapEditMode.Rail => $"Rail: click a straight or diagonal destination to place {ActiveRailName}.",
            MapEditMode.Road when buildAnchorLocation is null => "Road: click a buildable tile to set the start point.",
            MapEditMode.Road => "Road: click a north/south/east/west destination to place road.",
            MapEditMode.Station => $"Station building: click flat dry land to build {ActiveStationName}.",
            MapEditMode.Structure when ActiveStructureContribution?.PlacementKind == SpriteContributionPlacementKind.RailStationary => $"Rail accessory: click rail to build {ActiveStructureName}.",
            MapEditMode.Structure when ActiveStructureContribution?.PlacementKind == SpriteContributionPlacementKind.RoadAccessory => $"Road accessory: click road to build {ActiveStructureName}.",
            MapEditMode.Structure => $"Structure: click flat dry land to build {ActiveStructureName}.",
            MapEditMode.Platform => $"Platform: click rail to build {ActivePlatformDescription}.",
            MapEditMode.Train => $"Train: click rail to place {ActiveTrainName}.",
            MapEditMode.Terrain => "Terrain: select a tile corner, then raise or lower it.",
            MapEditMode.Erase => "Erase: click or right-click stations, platforms, rail, or road.",
            _ => "Select: click a tile to inspect it. Ctrl-scroll zooms the map."
        };
    }

    private void ApplyEdit(TileLocation location)
    {
        TileLocation transportLocation = GetTransportPlacementLocation(location);
        if (EditMode is MapEditMode.Select or MapEditMode.Terrain)
        {
            buildAnchorLocation = null;
            return;
        }

        if (EditMode == MapEditMode.Erase)
        {
            world.RemoveTransportAt(location);
            if (buildAnchorLocation is { } anchor && anchor.H == location.H && anchor.V == location.V)
            {
                buildAnchorLocation = null;
            }

            return;
        }

        if (EditMode == MapEditMode.Station && ActiveStationContribution is { } station)
        {
            world.AddStation(new ModernStation(
                $"station:{location.H}:{location.V}:{station.Id}",
                location.H,
                location.V,
                location.Z,
                station));
            return;
        }

        if (EditMode == MapEditMode.Structure
            && ActiveStructureContribution is { } structure
            && SelectStructureFrameForLocation(structure, location) is { } structureFrame)
        {
            bool allowTransportOverlap = structure.PlacementKind is SpriteContributionPlacementKind.RailStationary
                or SpriteContributionPlacementKind.RoadAccessory;
            if (structure.PlacementKind == SpriteContributionPlacementKind.RailStationary
                && !world.Transport.HasRail(location.H, location.V, location.Z))
            {
                return;
            }

            if (structure.PlacementKind == SpriteContributionPlacementKind.RoadAccessory
                && !world.Transport.HasRoad(location.H, location.V))
            {
                return;
            }

            ModernPlacedEntity entity = ModernPlacedEntity.Structure(
                location.H,
                location.V,
                location.Z,
                structure,
                structureFrame,
                activeStructureColorVariantIndex);
            if (world.AddEntity(entity, allowTransportOverlap) && entity.EntityValue > 0)
            {
                world.Spend(entity.EntityValue, ModernAccountGenre.Construction, $"Built {structure.DisplayName}.");
            }

            return;
        }

        if (EditMode == MapEditMode.Platform)
        {
            ModernPlatform platform = new(
                $"platform:{location.H}:{location.V}:{activePlatformDirectionIndex}:{activePlatformLength}:{activePlatformStyle}",
                location.H,
                location.V,
                location.Z,
                activePlatformDirectionIndex,
                activePlatformLength,
                activePlatformStyle,
                null);
            world.AddPlatform(platform);
            return;
        }

        if (EditMode == MapEditMode.Train && ActiveTrainContribution is { } train)
        {
            string trainId = $"train:{location.H}:{location.V}:{train.Id}";
            world.AddTrain(new ModernTrain(trainId, train, Array.Empty<ModernTrainCarPlacement>(), 0));
            world.PlaceTrain(trainId, new ModernVoxelKey(location.H, location.V, location.Z), ModernDirection.FromIndex(activePlatformDirectionIndex), trainCarContributions);
            return;
        }

        if (EditMode == MapEditMode.Rail && buildAnchorLocation is null)
        {
            if (CanStartRailLineAt(transportLocation))
            {
                buildAnchorLocation = transportLocation;
            }

            return;
        }

        if (EditMode == MapEditMode.Rail)
        {
            if (buildAnchorLocation is { } start && CanBuildRailLine(start, transportLocation))
            {
                world.AddRailLine(start, transportLocation, ActiveRailKind);
                buildAnchorLocation = transportLocation;
            }
            else
            {
                buildAnchorLocation = transportLocation;
            }
        }

        if (!CanBuildTransportAt(transportLocation))
        {
            return;
        }

        if (EditMode == MapEditMode.Road && ActiveRoadContribution is { } road)
        {
            if (buildAnchorLocation is { } start && CanBuildRoadLine(start, transportLocation))
            {
                world.AddRoadLine(start, transportLocation, road);
                buildAnchorLocation = transportLocation;
            }
            else
            {
                world.AddRoadTile(transportLocation, road);
                buildAnchorLocation = transportLocation;
            }
        }
    }

    private TileLocation GetTransportPlacementLocation(TileLocation location)
    {
        return EditMode == MapEditMode.Rail && ActiveRailKindUsesTargetLevel()
            ? location with { Z = activeRailBuildLevel }
            : location;
    }

    private bool CanBuildTransportAt(TileLocation location)
    {
        if (EditMode == MapEditMode.Rail)
        {
            return world.CanBuildRailLine(location, location, ActiveRailKind);
        }

        return world.IsBuildableSurface(location);
    }

    private bool CanStartRailLineAt(TileLocation location)
    {
        if (location.Z < 0 || location.Z > world.MaxHeightCutLevel)
        {
            return false;
        }

        return ActiveRailKind == ModernSpecialRailKind.Normal
            ? world.CanBuildRailLine(location, location, ActiveRailKind)
            : world.IsInside(location.H, location.V);
    }

    private SpriteFrame? SelectStructureFrameForLocation(SpriteContribution structure, TileLocation location)
    {
        SpriteFrame[] frames = structure.Frames.Where(frame => frame.IsLoadable).ToArray();
        if (frames.Length == 0)
        {
            return null;
        }

        if (structure.Type.Equals("DummyCar", StringComparison.OrdinalIgnoreCase) && frames.Length > 1)
        {
            byte roadMask = world.Transport.GetRoadMask(location.H, location.V);
            bool northSouth = (roadMask & (ModernRoadPattern.North | ModernRoadPattern.South)) != 0;
            bool eastWest = (roadMask & (ModernRoadPattern.East | ModernRoadPattern.West)) != 0;
            return northSouth && !eastWest ? frames[1] : frames[0];
        }

        if (structure.Type.Equals("HalfVoxelStructure", StringComparison.OrdinalIgnoreCase))
        {
            int directionVariant = Math.Max(0, activeStructureFrameIndex) % frames.Length;
            return frames[directionVariant];
        }

        return frames[Math.Clamp(activeStructureFrameIndex, 0, frames.Length - 1)];
    }

    private bool CanBuildRailLine(TileLocation from, TileLocation to)
    {
        return world.CanBuildRailLine(from, to, ActiveRailKind);
    }

    private bool CanBuildRoadLine(TileLocation from, TileLocation to)
    {
        return world.CanBuildRoadLine(from, to);
    }

    private static IEnumerable<TileLocation> PreviewLine(TileLocation from, TileLocation to)
    {
        int h = from.H;
        int v = from.V;
        yield return from;

        int guard = 0;
        while ((h != to.H || v != to.V) && guard++ < 512)
        {
            h += Math.Sign(to.H - h);
            v += Math.Sign(to.V - v);
            yield return new TileLocation(h, v, to.Z);
        }
    }

    private static int SelectInitialRoadIndex(IReadOnlyList<RoadContribution> roads)
    {
        if (roads.Count == 0)
        {
            return 0;
        }

        int preferred = roads
            .Select((road, index) => new { Road = road, Index = index })
            .OrderByDescending(entry => entry.Road.Kind == RoadContributionKind.Standard)
            .ThenByDescending(entry => entry.Road.Style.MajorType.Equals("street", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(entry => entry.Road.Style.Lanes)
            .ThenBy(entry => entry.Road.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Road.DisplayName, StringComparer.OrdinalIgnoreCase)
            .First()
            .Index;
        return preferred;
    }

    private static int StructurePlacementPriority(SpriteContributionPlacementKind kind)
    {
        return kind switch
        {
            SpriteContributionPlacementKind.Structure => 0,
            SpriteContributionPlacementKind.VariableHeightBuilding => 1,
            SpriteContributionPlacementKind.RailStationary => 2,
            SpriteContributionPlacementKind.RoadAccessory => 3,
            SpriteContributionPlacementKind.ElectricPole => 4,
            _ => 9
        };
    }

    private static int SpecialRailPriority(ModernSpecialRailKind kind)
    {
        return kind switch
        {
            ModernSpecialRailKind.Bridge => 0,
            ModernSpecialRailKind.SteelSupported => 1,
            ModernSpecialRailKind.Tunnel => 2,
            ModernSpecialRailKind.Garage => 3,
            _ => 9
        };
    }

    private static int GetColorVariantCount(SpriteContribution structure)
    {
        return structure.Frames
            .Concat(structure.SpriteSet2D?.InVoxelDrawOrder().Select(voxel => voxel.Frame) ?? Enumerable.Empty<SpriteFrame>())
            .Concat(structure.SpriteSet3D?.InVoxelDrawOrder().Select(voxel => voxel.Frame) ?? Enumerable.Empty<SpriteFrame>())
            .SelectMany(frame => frame.ColorMaps)
            .Select(map => LegacyBitmap.GetColorLibrarySize(map.To))
            .DefaultIfEmpty(1)
            .Max();
    }

    private static string SpecialRailDisplayName(SpecialRailContribution rail)
    {
        if (!string.IsNullOrWhiteSpace(rail.Name))
        {
            return rail.Name;
        }

        return rail.Kind switch
        {
            ModernSpecialRailKind.Bridge => "Bridge rail",
            ModernSpecialRailKind.SteelSupported => "Steel-supported rail",
            ModernSpecialRailKind.Tunnel => "Tunnel rail",
            ModernSpecialRailKind.Garage => "Train garage rail",
            _ => rail.Class.Name
        };
    }

    public static string FormatSpecialRailDisplayName(SpecialRailContribution rail)
    {
        return SpecialRailDisplayName(rail);
    }

    private static int IndexOfReferenceOrValue<T>(IReadOnlyList<T> items, T item)
        where T : class
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], item) || EqualityComparer<T>.Default.Equals(items[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    private static string? FindRailRoadCrossingPath(LegacyAssetCatalog assets, PluginManifestCatalog plugins)
    {
        string? pluginPicture = plugins.Pictures
            .FirstOrDefault(picture => string.Equals(picture.Id, RailRoadCrossingPictureId, StringComparison.OrdinalIgnoreCase)
                && picture.IsLoadable)
            ?.ResolvedPath;
        if (pluginPicture is not null)
        {
            return pluginPicture;
        }

        string fallback = Path.Combine(assets.PluginDirectory, "system", "crossing.bmp");
        return File.Exists(fallback) ? fallback : null;
    }

    private static string? FindPluginAsset(LegacyAssetCatalog assets, string pluginDirectoryName, string fileName)
    {
        string direct = Path.Combine(assets.PluginDirectory, pluginDirectoryName, fileName);
        if (File.Exists(direct))
        {
            return direct;
        }

        return Directory.Exists(assets.PluginDirectory)
            ? Directory.EnumerateFiles(assets.PluginDirectory, fileName, SearchOption.AllDirectories)
                .FirstOrDefault(path => path.Contains(pluginDirectoryName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static string FormatPlatformStyle(PlatformStyle style)
    {
        return style switch
        {
            PlatformStyle.ThinNoRoof => "thin/no roof",
            PlatformStyle.ThinRoof => "thin/roof",
            PlatformStyle.Fat => "wide",
            _ => style.ToString()
        };
    }

    private bool ToolUsesAnchor => EditMode is MapEditMode.Rail or MapEditMode.Road;

    public void Dispose()
    {
        groundTiles.Dispose();
        terrainRenderer.Dispose();
        railBitmap.Dispose();
        bridgeRailBitmap?.Dispose();
        bridgePierBitmap?.Dispose();
        defaultBridgePierBitmap?.Dispose();
        steelSupportedRailBitmap?.Dispose();
        tunnelRailBitmap?.Dispose();
        garageRailBitmap?.Dispose();
        thinPlatformBitmap.Dispose();
        fatPlatformBitmap.Dispose();
        foreach (Bitmap bitmap in pluginBitmaps.Values)
        {
            bitmap.Dispose();
        }

        pluginBitmaps.Clear();
    }

    private readonly record struct ForestTreePattern(int OffsetX, int OffsetY, int SpriteIndex);
    private static readonly (int H, int V)[] EightWayNeighbors =
    {
        (0, -1),
        (1, -1),
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1)
    };
}
