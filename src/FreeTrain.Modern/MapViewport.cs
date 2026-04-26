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
    Terrain,
    Erase
}

public sealed class MapViewport : Control, IDisposable
{
    private const int TileWidth = 32;
    private const int TileHeight = 16;
    private const string RailRoadCrossingPictureId = "{F4380415-A2F2-41d8-8FCD-ED25A470A84D}";

    private readonly LegacySpriteSheet groundTiles;
    private readonly Bitmap railBitmap;
    private readonly IBrush background = new SolidColorBrush(Color.FromRgb(219, 232, 226));
    private readonly IBrush waterBrush = new SolidColorBrush(Color.FromRgb(107, 156, 178));
    private readonly IBrush waterTileBrush = new SolidColorBrush(Color.FromRgb(81, 148, 181));
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
    private readonly string? railRoadCrossingPath;
    private readonly Dictionary<string, Bitmap> pluginBitmaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly RenderOptions pixelArtRenderOptions = new()
    {
        BitmapInterpolationMode = BitmapInterpolationMode.None,
        EdgeMode = EdgeMode.Aliased
    };
    private TileLocation? hoverLocation;
    private TileLocation? selectedLocation;
    private TileLocation? buildAnchorLocation;
    private double zoom = 2.0;
    private bool showGrid = true;
    private bool useNightView;
    private int maxVisibleLevel;
    private MapEditMode editMode = MapEditMode.Select;
    private int activeRoadIndex;
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

        groundTiles = new LegacySpriteSheet(groundPath, TileWidth, TileHeight);
        railBitmap = LegacyBitmap.LoadWithColorKey(railPath);
        terrainRenderer = new TerrainRenderer(palettePath, cliffPath);
        roadContributions = plugins.Roads.Where(road => road.IsLoadable).ToList().AsReadOnly();
        landContributions = plugins.Lands.Where(land => land.IsLoadable).ToList().AsReadOnly();
        spriteContributions = plugins.Sprites.Where(sprite => sprite.IsLoadable).ToList().AsReadOnly();
        railRoadCrossingPath = FindRailRoadCrossingPath(assets, plugins);
        activeRoadIndex = SelectInitialRoadIndex(roadContributions);
        world = ModernWorld.CreateSample(roadContributions);
        world.Changed += OnWorldChanged;
        HashSet<(int H, int V)> occupied = CreateInitialOccupiedTiles();
        AddSampleLandEntities(plugins, occupied);
        AddSampleStructureEntities(plugins, occupied);
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

    public int WorldMaxGroundLevel => world.MaxGroundLevel;
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

        world.Changed -= OnWorldChanged;
        world = ModernWorld.FromSnapshot(snapshot, roadLookup, landLookup, spriteLookup);
        world.Changed += OnWorldChanged;
        selectedLocation = null;
        hoverLocation = null;
        buildAnchorLocation = null;
        maxVisibleLevel = Math.Min(maxVisibleLevel, world.MaxHeightCutLevel);
        InvalidateMeasure();
        InvalidateVisual();
        PublishStatus();
    }

    private RoadContribution? ActiveRoadContribution => roadContributions.Count == 0
        ? null
        : roadContributions[Math.Clamp(activeRoadIndex, 0, roadContributions.Count - 1)];

    private void OnWorldChanged(object? sender, ModernWorldChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Description))
        {
            lastMessage = e.Description;
        }

        InvalidateVisual();
        PublishStatus();
    }

    private double MapOriginX => 64;
    private double MapOriginY => 40 + world.MaxGroundLevel * 8;

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
            RenderMapObjects(context, expandedVisibleMapRect);
            RenderMapMarkers(context);

            if (UseNightView)
            {
                context.FillRectangle(nightBrush, visibleMapRect);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = (world.Width + world.Height) * 16 * Zoom + 160;
        double height = (world.Width + world.Height + world.MaxGroundLevel * 2) * 8 * Zoom + 180;
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
        context.FillRectangle(waterBrush, visibleMapRect);
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

            if (terrain.BaseLevel < world.WaterLevel && MaxVisibleLevel >= world.WaterLevel)
            {
                DrawWaterTile(context, p);
                continue;
            }

            terrainRenderer.DrawTerrainTile(context, world, groundTiles, h, v, p, terrain, ShowGrid);
        }
    }

    private void RenderMapObjects(DrawingContext context, Rect visibleMapRect)
    {
        foreach (ModernPlacedEntity mapObject in world.StructureEntities.OrderBy(obj => obj.H + obj.V).ThenBy(obj => obj.V))
        {
            int z = world.GetTerrainTile(mapObject.H, mapObject.V).SurfaceLevel;
            if (z > MaxVisibleLevel || !IsTilePotentiallyVisible(mapObject.H, mapObject.V, z, visibleMapRect, 320, 560, 160, 120))
            {
                continue;
            }

            Point tilePoint = FromHvzToScreen(mapObject.H, mapObject.V, z);
            if (mapObject.StructureContribution?.SpriteSet3D is { IsLoadable: true } spriteSet)
            {
                DrawSpriteSet3D(context, spriteSet, tilePoint);
            }
            else if (mapObject.StructureFrame is { } frame)
            {
                DrawSpriteFrame(context, frame, tilePoint);
            }
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
        foreach (MapRailObject railObject in world.CreateRailObjects().OrderBy(obj => obj.H + obj.V).ThenBy(obj => obj.V))
        {
            TerrainTilePreview terrain = world.GetTerrainTile(railObject.H, railObject.V);
            int z = terrain.SurfaceLevel;
            if (z > MaxVisibleLevel || !IsTilePotentiallyVisible(railObject.H, railObject.V, z, visibleMapRect, 48, 48, 48, 48))
            {
                continue;
            }

            Point tilePoint = FromHvzToScreen(railObject.H, railObject.V, z);
            Point targetPoint = new(tilePoint.X, tilePoint.Y - railObject.Pattern.OffsetY);
            context.DrawImage(
                railBitmap,
                railObject.Pattern.SourceRect,
                new Rect(targetPoint, new Size(railObject.Pattern.SourceWidth, railObject.Pattern.SourceHeight)));
        }
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
        if (buildAnchorLocation is { } previewAnchor && hoverLocation is { } previewHover && ToolUsesAnchor)
        {
            bool canBuildLine = EditMode switch
            {
                MapEditMode.Rail => CanBuildRailLine(previewAnchor, previewHover),
                MapEditMode.Road => CanBuildRoadLine(previewAnchor, previewHover),
                _ => false
            };
            Pen previewPen = canBuildLine ? validPreviewPen : invalidPreviewPen;
            IBrush previewFill = canBuildLine ? validPreviewFill : invalidPreviewFill;
            foreach (TileLocation location in PreviewLine(previewAnchor, previewHover))
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

    private void DrawWaterTile(DrawingContext context, Point p)
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

        context.DrawGeometry(waterTileBrush, null, geometry);
    }

    private void DrawCornerMarker(DrawingContext context, TileLocation location, IBrush fill, Pen outline)
    {
        Point p = GetTerrainCornerScreenPoint(location);
        context.DrawEllipse(fill, outline, p, 4.5, 4.5);
        context.DrawLine(outline, new Point(p.X - 7, p.Y), new Point(p.X + 7, p.Y));
        context.DrawLine(outline, new Point(p.X, p.Y - 7), new Point(p.X, p.Y + 7));
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

    public void SelectPreviousRoad()
    {
        if (roadContributions.Count == 0)
        {
            return;
        }

        activeRoadIndex = (activeRoadIndex + roadContributions.Count - 1) % roadContributions.Count;
        PublishStatus();
    }

    public void AdvanceClock(long minutes)
    {
        world.AdvanceClock(minutes);
        PublishStatus();
    }

    private void DrawSpriteFrame(DrawingContext context, SpriteFrame frame, Point tilePoint)
    {
        if (!frame.IsLoadable)
        {
            return;
        }

        Bitmap bitmap = GetPluginBitmap(frame.ResolvedPath);
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
            null);
    }

    private void DrawSpriteSet3D(DrawingContext context, ModernSpriteSet3D spriteSet, Point basePoint)
    {
        foreach (ModernSpriteVoxel3D voxel in spriteSet.InVoxelDrawOrder())
        {
            Point voxelPoint = new(
                basePoint.X + (voxel.X + voxel.Y) * 16,
                basePoint.Y + (-voxel.X + voxel.Y) * 8 - voxel.Z * 16);
            DrawSpriteFrame(context, voxel.Frame, voxelPoint);
        }
    }

    private void DrawForest(DrawingContext context, ForestSpriteSet forest, int h, int v, Point tilePoint)
    {
        if (forest.Ground is { IsLoadable: true } ground)
        {
            DrawSpriteFrame(context, ground, tilePoint);
        }

        IReadOnlyList<ForestTreePattern> patterns = CreateForestPatterns(forest, h, v);
        foreach (ForestTreePattern pattern in patterns)
        {
            SpriteFrame sprite = forest.TreeSprites[pattern.SpriteIndex];
            DrawSpriteFrame(context, sprite, new Point(tilePoint.X + pattern.OffsetX, tilePoint.Y + pattern.OffsetY));
        }
    }

    private Bitmap GetPluginBitmap(string path)
    {
        if (!pluginBitmaps.TryGetValue(path, out Bitmap? bitmap))
        {
            bitmap = LegacyBitmap.LoadWithColorKey(path);
            pluginBitmaps[path] = bitmap;
        }

        return bitmap;
    }

    private HashSet<(int H, int V)> CreateInitialOccupiedTiles()
    {
        HashSet<(int H, int V)> occupied = new(world.Transport.RailTiles);
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
                if (!terrain.IsFlat || terrain.SurfaceLevel < world.WaterLevel)
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
                if (terrain.IsFlat && terrain.SurfaceLevel >= world.WaterLevel)
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
        if (!baseTerrain.IsFlat || baseTerrain.SurfaceLevel < world.WaterLevel)
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
                if (!terrain.IsFlat || terrain.SurfaceLevel != baseTerrain.SurfaceLevel || terrain.SurfaceLevel < world.WaterLevel)
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

        if (terrain.BaseLevel < world.WaterLevel && MaxVisibleLevel >= world.WaterLevel)
        {
            return world.WaterLevel;
        }

        return terrain.BaseLevel;
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
        bool usesTerrainProfile = visualLevel == terrain.BaseLevel && terrain.BaseLevel >= world.WaterLevel;
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
        return new MapViewportStatus(
            world.Name,
            hoverLocation,
            selectedLocation,
            buildAnchorLocation,
            EditMode,
            ActiveRoadName,
            world.Clock,
            world.Account.Cash,
            world.Account.TotalDebt,
            world.Entities.Count,
            world.TrafficVoxels.Count,
            world.Transport.RailTiles.Count,
            world.Transport.RoadTiles.Count,
            world.Cars.Count,
            Zoom,
            MaxVisibleLevel,
            world.MaxHeightCutLevel,
            ShowGrid,
            UseNightView,
            hint,
            lastMessage);
    }

    private string CreateInteractionHint()
    {
        if (hoverLocation is { } hover && ToolUsesAnchor && !world.IsBuildableSurface(hover))
        {
            return "This tile is not buildable. Pick flat dry land or erase an obstruction first.";
        }

        return EditMode switch
        {
            MapEditMode.Rail when buildAnchorLocation is null => "Rail: click a buildable tile to set the start point.",
            MapEditMode.Rail => "Rail: click a straight or diagonal destination to place track.",
            MapEditMode.Road when buildAnchorLocation is null => "Road: click a buildable tile to set the start point.",
            MapEditMode.Road => "Road: click a north/south/east/west destination to place road.",
            MapEditMode.Terrain => "Terrain: select a tile corner, then raise or lower it.",
            MapEditMode.Erase => "Erase: click or right-click transport on the map.",
            _ => "Select: click a tile to inspect it. Ctrl-scroll zooms the map."
        };
    }

    private void ApplyEdit(TileLocation location)
    {
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

        if (!CanBuildTransportAt(location))
        {
            return;
        }

        if (EditMode == MapEditMode.Rail)
        {
            if (buildAnchorLocation is { } start && CanBuildRailLine(start, location))
            {
                world.AddRailLine(start, location);
                buildAnchorLocation = location;
            }
            else
            {
                world.AddRailTile(location);
                buildAnchorLocation = location;
            }
        }

        if (EditMode == MapEditMode.Road && ActiveRoadContribution is { } road)
        {
            if (buildAnchorLocation is { } start && CanBuildRoadLine(start, location))
            {
                world.AddRoadLine(start, location, road);
                buildAnchorLocation = location;
            }
            else
            {
                world.AddRoadTile(location, road);
                buildAnchorLocation = location;
            }
        }
    }

    private bool CanBuildTransportAt(TileLocation location)
    {
        return world.IsBuildableSurface(location);
    }

    private bool CanBuildRailLine(TileLocation from, TileLocation to)
    {
        return world.CanBuildRailLine(from, to);
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

    private bool ToolUsesAnchor => EditMode is MapEditMode.Rail or MapEditMode.Road;

    public void Dispose()
    {
        groundTiles.Dispose();
        terrainRenderer.Dispose();
        railBitmap.Dispose();
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
