using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace FreeTrain.Modern;

    public sealed class MapViewport : Control, IDisposable
    {
    private const int TileWidth = 32;
    private const int TileHeight = 16;

    private readonly LegacySpriteSheet groundTiles;
    private readonly Bitmap railBitmap;
    private readonly IBrush background = new SolidColorBrush(Color.FromRgb(219, 232, 226));
    private readonly IBrush waterBrush = new SolidColorBrush(Color.FromRgb(107, 156, 178));
    private readonly IBrush nightBrush = new SolidColorBrush(Color.FromArgb(82, 15, 28, 54));
    private readonly Pen hoverPen = new(new SolidColorBrush(Color.FromRgb(255, 247, 153)), 2);
    private readonly Pen selectionPen = new(new SolidColorBrush(Color.FromRgb(243, 97, 72)), 2);
    private readonly WorldPreview world;
    private readonly TerrainRenderer terrainRenderer;
    private readonly IReadOnlyList<MapLandObject> landObjects;
    private readonly IReadOnlyList<MapRailObject> railObjects;
    private readonly IReadOnlyList<MapSpriteObject> mapObjects;
    private readonly Dictionary<string, Bitmap> pluginBitmaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly RenderOptions pixelArtRenderOptions = new()
    {
        BitmapInterpolationMode = BitmapInterpolationMode.None,
        EdgeMode = EdgeMode.Aliased
    };
    private TileLocation? hoverLocation;
    private TileLocation? selectedLocation;
    private double zoom = 2.0;
    private bool showGrid = true;
    private bool useNightView;
    private int maxVisibleLevel;

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
        world = WorldPreview.CreateSample();
        HashSet<(int H, int V)> occupied = new();
        landObjects = CreateSampleLandObjects(plugins, occupied);
        railObjects = CreateSampleRailObjects();
        mapObjects = CreateSampleObjects(plugins, occupied);
        maxVisibleLevel = world.MaxHeightCutLevel;
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public event Action<string>? StatusChanged;

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

    private double MapOriginX => 64;
    private double MapOriginY => 40 + world.MaxGroundLevel * 8;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(background, Bounds);

        using (context.PushRenderOptions(pixelArtRenderOptions))
        using (context.PushTransform(Matrix.CreateScale(Zoom, Zoom)))
        {
            RenderWater(context);
            RenderGround(context);
            RenderLandObjects(context);
            RenderRailObjects(context);
            RenderMapObjects(context);
            RenderMapMarkers(context);

            if (UseNightView)
            {
                context.FillRectangle(nightBrush, new Rect(0, 0, Bounds.Width / Zoom, Bounds.Height / Zoom));
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = (world.Width + world.Height) * 16 * Zoom + 160;
        double height = (world.Width + world.Height + world.MaxGroundLevel * 2) * 8 * Zoom + 180;
        return new Size(width, height);
    }

    private void RenderWater(DrawingContext context)
    {
        double width = (world.Width + world.Height) * 16 + 160;
        double height = (world.Width + world.Height) * 8 + 160;
        context.FillRectangle(waterBrush, new Rect(0, 0, width, height));
    }

    private void RenderGround(DrawingContext context)
    {
        bool noHeightCut = MaxVisibleLevel == world.MaxHeightCutLevel;
        int initialLevel = noHeightCut ? world.WaterLevel : 0;

        for (int v = 0; v < world.Height; v++)
        {
            for (int h = 0; h < world.Width; h++)
            {
                TerrainTilePreview terrain = world.GetTerrainTile(h, v);
                int groundLevel = terrain.SurfaceLevel;

                for (int z = initialLevel; z <= MaxVisibleLevel; z++)
                {
                    bool drawLand = z == terrain.BaseLevel && groundLevel <= MaxVisibleLevel;
                    bool drawWater = noHeightCut && z == world.WaterLevel && groundLevel < world.WaterLevel;
                    bool drawHeightCut = z == MaxVisibleLevel && MaxVisibleLevel < terrain.BaseLevel;

                    if (!drawLand && !drawWater && !drawHeightCut)
                    {
                        continue;
                    }

                    Point p = FromHvzToScreen(h, v, z);

                    if (drawHeightCut)
                    {
                        groundTiles.DrawTile(context, 3, p);
                    }
                    else if (drawWater)
                    {
                        groundTiles.DrawTile(context, 2, p);
                        if (ShowGrid)
                        {
                            terrainRenderer.DrawDiamond(context, p, terrainRenderer.CoastPen);
                        }
                    }
                    else
                    {
                        if (terrain.IsFlat && terrain.BaseLevel < world.WaterLevel)
                        {
                            groundTiles.DrawTile(context, 2, p);
                            if (ShowGrid)
                            {
                                terrainRenderer.DrawDiamond(context, p, terrainRenderer.CoastPen);
                            }
                        }
                        else
                        {
                            terrainRenderer.DrawTerrainTile(context, world, groundTiles, h, v, p, terrain, ShowGrid);
                        }
                    }
                }
            }
        }
    }

    private void RenderMapObjects(DrawingContext context)
    {
        foreach (MapSpriteObject mapObject in mapObjects.OrderBy(obj => obj.H + obj.V).ThenBy(obj => obj.V))
        {
            int z = world.GetTerrainTile(mapObject.H, mapObject.V).SurfaceLevel;
            if (z > MaxVisibleLevel)
            {
                continue;
            }

            Point tilePoint = FromHvzToScreen(mapObject.H, mapObject.V, z);
            if (mapObject.Contribution.SpriteSet3D is { IsLoadable: true } spriteSet)
            {
                DrawSpriteSet3D(context, spriteSet, tilePoint);
            }
            else
            {
                DrawSpriteFrame(context, mapObject.Frame, tilePoint);
            }
        }
    }

    private void RenderLandObjects(DrawingContext context)
    {
        foreach (MapLandObject landObject in landObjects.OrderBy(obj => obj.H + obj.V).ThenBy(obj => obj.V))
        {
            TerrainTilePreview terrain = world.GetTerrainTile(landObject.H, landObject.V);
            int z = terrain.SurfaceLevel;
            if (z > MaxVisibleLevel)
            {
                continue;
            }

            Point tilePoint = FromHvzToScreen(landObject.H, landObject.V, z);
            switch (landObject.Contribution.Kind)
            {
                case LandContributionKind.Static:
                    if (landObject.Contribution.StaticSprite is { } staticSprite)
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
                    if (landObject.Contribution.Forest is { } forest)
                    {
                        DrawForest(context, forest, landObject.H, landObject.V, tilePoint);
                    }
                    break;
            }
        }
    }

    private void RenderRailObjects(DrawingContext context)
    {
        foreach (MapRailObject railObject in railObjects.OrderBy(obj => obj.H + obj.V).ThenBy(obj => obj.V))
        {
            TerrainTilePreview terrain = world.GetTerrainTile(railObject.H, railObject.V);
            int z = terrain.SurfaceLevel;
            if (z > MaxVisibleLevel)
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

    private void RenderMapMarkers(DrawingContext context)
    {
        if (selectedLocation is { } selected && selected.Z <= MaxVisibleLevel)
        {
            terrainRenderer.DrawDiamond(context, FromHvzToScreen(selected.H, selected.V, selected.Z), selectionPen);
        }

        if (hoverLocation is { } hover && hover.Z <= MaxVisibleLevel)
        {
            terrainRenderer.DrawDiamond(context, FromHvzToScreen(hover.H, hover.V, hover.Z), hoverPen);
        }
    }

    public void RaiseSelectedTerrain()
    {
        if (selectedLocation is not { } selected)
        {
            return;
        }

        if (world.GetTerrainTile(selected.H, selected.V).SurfaceLevel == selected.Z
            && world.RaiseCorner(selected.H, selected.V, selected.Corner))
        {
            maxVisibleLevel = Math.Max(maxVisibleLevel, world.GetTerrainTile(selected.H, selected.V).SurfaceLevel);
            selectedLocation = selected with { Z = world.GetTerrainTile(selected.H, selected.V).SurfaceLevel };
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

        if (world.GetTerrainTile(selected.H, selected.V).SurfaceLevel == selected.Z
            && world.LowerCorner(selected.H, selected.V, selected.Corner))
        {
            selectedLocation = selected with { Z = world.GetTerrainTile(selected.H, selected.V).SurfaceLevel };
            InvalidateMeasure();
            InvalidateVisual();
            PublishStatus();
        }
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

    private MapSpriteObject[] CreateSampleObjects(PluginManifestCatalog plugins, HashSet<(int H, int V)> occupied)
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

        List<MapSpriteObject> objects = new();
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
                MarkOccupied(placed.H, placed.V, candidates[i], occupied);
                objects.Add(new MapSpriteObject(placed.H, placed.V, candidates[i], frame));
            }
        }

        return objects.ToArray();
    }

    private MapRailObject[] CreateSampleRailObjects()
    {
        HashSet<(int H, int V)> railTiles = new();
        AddRailLine(railTiles, (5, 10), (17, 10));
        AddRailLine(railTiles, (17, 10), (25, 18));
        AddRailLine(railTiles, (25, 18), (17, 26));
        AddRailLine(railTiles, (17, 26), (5, 26));
        AddRailLine(railTiles, (5, 26), (5, 10));

        Dictionary<(int H, int V), byte> masks = new();
        foreach ((int h, int v) in railTiles)
        {
            byte mask = 0;
            foreach ((int dh, int dv) in EightWayNeighbors)
            {
                if (!railTiles.Contains((h + dh, v + dv)))
                {
                    continue;
                }

                int direction = ModernRailPattern.DirectionFromDelta(dh, dv);
                mask |= (byte)(1 << direction);
            }

            masks[(h, v)] = mask;
        }

        return masks
            .Select(entry => ModernRailPattern.FromDirectionMask(entry.Value) is { } pattern
                ? new MapRailObject(entry.Key.H, entry.Key.V, pattern)
                : null)
            .Where(rail => rail is not null)
            .Cast<MapRailObject>()
            .ToArray();
    }

    private static void AddRailLine(HashSet<(int H, int V)> railTiles, (int H, int V) from, (int H, int V) to)
    {
        int h = from.H;
        int v = from.V;
        railTiles.Add((h, v));
        while (h != to.H || v != to.V)
        {
            h += Math.Sign(to.H - h);
            v += Math.Sign(to.V - v);
            railTiles.Add((h, v));
        }
    }

    private MapLandObject[] CreateSampleLandObjects(PluginManifestCatalog plugins, HashSet<(int H, int V)> occupied)
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

        List<MapLandObject> objects = new();
        if (forest is not null)
        {
            AddLandPatch(objects, occupied, forest, staticLands, 3, 18, 7, 6);
            AddLandPatch(objects, occupied, forest, staticLands, 23, 4, 5, 5);
        }

        if (random is not null)
        {
            AddLandPatch(objects, occupied, random, staticLands, 18, 18, 7, 4);
        }

        if (basicStatic is not null)
        {
            AddLandPatch(objects, occupied, basicStatic, staticLands, 7, 28, 6, 3);
        }

        return objects.ToArray();
    }

    private void AddLandPatch(
        List<MapLandObject> objects,
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

                objects.Add(new MapLandObject(h, v, land, resolved));
                occupied.Add((h, v));
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
        Point ab = new(mapPoint.X - MapOriginX, mapPoint.Y - MapOriginY);
        TileLocation? ground = PickGroundLocationFromAb(ab);
        return ground is null ? null : SelectMountainCorner(ground.Value, ab);
    }

    private TileLocation? PickGroundLocationFromAb(Point ab)
    {
        int a = (int)Math.Floor(ab.X);
        int b = (int)Math.Floor(ab.Y);
        int t = 2 * b - 16;
        int x = (a - t) >> 5;
        int y = (a + t) >> 5;
        x += (world.Height - 1) / 2;

        for (int z = MaxVisibleLevel; z >= 0; z--)
        {
            int locX = x - z;
            int locY = y + z;
            if (!TryXyToHv(locX, locY, out int h, out int v))
            {
                continue;
            }

            if (world.GetTerrainTile(h, v).SurfaceLevel == z)
            {
                return new TileLocation(h, v, z);
            }
        }

        return null;
    }

    private TileLocation SelectMountainCorner(TileLocation selected, Point ab)
    {
        TerrainTilePreview terrain = world.GetTerrainTile(selected.H, selected.V);
        (int x, int y) = HvToXy(selected.H, selected.V);
        Point tileAb = new(FromHvzToScreen(selected.H, selected.V, selected.Z).X - MapOriginX, FromHvzToScreen(selected.H, selected.V, selected.Z).Y - MapOriginY);
        Point offset = new(ab.X - tileAb.X, ab.Y - tileAb.Y);

        if (offset.X < 8)
        {
            x--;
        }
        else if (offset.X >= 24)
        {
            y++;
        }
        else if (offset.Y >= (16 - (terrain.Top + terrain.Bottom) * 4) / 2.0)
        {
            x--;
            y++;
        }

        return TryXyToHv(x, y, out int h, out int v)
            ? new TileLocation(h, v, selected.Z)
            : selected;
    }

    private (int X, int Y) HvToXy(int h, int v)
    {
        int x = h - v / 2 + (world.Height - 1) / 2;
        int y = h + (v + 1) / 2;
        return (x, y);
    }

    private bool TryXyToHv(int x, int y, out int h, out int v)
    {
        int xx = x - (world.Height - 1) / 2;
        h = (xx + y) >> 1;
        v = y - xx;
        return h >= 0 && h < world.Width && v >= 0 && v < world.Height;
    }

    private void PublishStatus()
    {
        string hover = hoverLocation is { } loc
            ? $"H {loc.H}, V {loc.V}, Z {loc.Z}, {loc.Corner}"
            : "No tile";
        string selected = selectedLocation is { } sel
            ? $"selected H {sel.H}, V {sel.V}, Z {sel.Z}, {sel.Corner}"
            : "nothing selected";

        StatusChanged?.Invoke($"{hover} | {selected} | zoom {Zoom:0.##}x | height cut {MaxVisibleLevel}");
    }

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

    private sealed record MapSpriteObject(int H, int V, SpriteContribution Contribution, SpriteFrame Frame);
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
