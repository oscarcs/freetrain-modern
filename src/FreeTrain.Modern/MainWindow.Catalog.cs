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
    private void UpdateBuildCatalog(MapViewportStatus status)
    {
        bool visible = IsBuildCatalogMode(status.EditMode);
        buildCatalogPanel.IsVisible = visible;
        if (!visible)
        {
            buildCatalogStateKey = "";
            return;
        }

        string activeKey = ActiveCatalogKey(status.EditMode);
        string nextStateKey = $"{status.EditMode}|{buildCatalogQuery}|{activeKey}|{mapViewport.ActiveStructureFrameVariantIndex}|{mapViewport.ActiveStructureColorVariantIndex}";
        if (nextStateKey == buildCatalogStateKey)
        {
            return;
        }

        IReadOnlyList<BuildCatalogItem> items = CreateBuildCatalogItems(status.EditMode, buildCatalogQuery, activeKey);
        buildCatalogTitle.Text = status.EditMode switch
        {
            MapEditMode.Rail => "Rail Catalog",
            MapEditMode.Road => "Road Catalog",
            MapEditMode.Station => "Station Catalog",
            MapEditMode.Structure => "Structure Catalog",
            MapEditMode.Train => "Train Catalog",
            _ => "Build Catalog"
        };
        buildCatalogSummary.Text = items.Count == 1
            ? "1 matching item"
            : $"{items.Count:N0} matching items";

        SyncBuildCatalogItems(items);
        buildCatalogStateKey = nextStateKey;
    }

    private void SyncBuildCatalogItems(IReadOnlyList<BuildCatalogItem> items)
    {
        Vector scrollOffset = buildCatalogScrollViewer.Offset;
        bool sameKeys = buildCatalogItems.Count == items.Count;
        if (sameKeys)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (!buildCatalogItems[i].Key.Equals(items[i].Key, StringComparison.Ordinal))
                {
                    sameKeys = false;
                    break;
                }
            }
        }

        if (!sameKeys)
        {
            buildCatalogItems.Clear();
            foreach (BuildCatalogItem item in items)
            {
                buildCatalogItems.Add(item);
            }
        }
        else
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (!buildCatalogItems[i].HasSameRenderedState(items[i]))
                {
                    buildCatalogItems[i] = items[i];
                }
            }
        }

        RestoreBuildCatalogScrollOffset(scrollOffset);
    }

    private void RestoreBuildCatalogScrollOffset(Vector offset)
    {
        void Apply()
        {
            buildCatalogScrollViewer.Offset = offset;
        }

        Apply();
        Dispatcher.UIThread.Post(Apply, DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);
    }

    private static bool IsBuildCatalogMode(MapEditMode mode)
    {
        return mode is MapEditMode.Rail
            or MapEditMode.Road
            or MapEditMode.Station
            or MapEditMode.Structure
            or MapEditMode.Train;
    }

    private string ActiveCatalogKey(MapEditMode mode)
    {
        return mode switch
        {
            MapEditMode.Rail => mapViewport.ActiveSpecialRailContribution is { } rail ? $"rail:{rail.Id}:{rail.Kind}" : "rail:standard",
            MapEditMode.Road when mapViewport.ActiveRoadContribution is { } road => $"road:{road.Id}:{road.PluginDirectoryName}",
            MapEditMode.Station when mapViewport.ActiveStationContribution is { } station => $"station:{station.Id}:{station.PluginDirectoryName}",
            MapEditMode.Structure when mapViewport.ActiveStructureContribution is { } structure => $"structure:{structure.Id}:{structure.PluginDirectoryName}",
            MapEditMode.Train when mapViewport.ActiveTrainContribution is { } train => $"train:{train.Id}:{train.PluginDirectoryName}",
            _ => ""
        };
    }

    private IReadOnlyList<BuildCatalogItem> CreateBuildCatalogItems(MapEditMode mode, string query, string activeKey)
    {
        IEnumerable<BuildCatalogItem> items = mode switch
        {
            MapEditMode.Rail => CreateRailCatalogItems(activeKey),
            MapEditMode.Road => mapViewport.RoadContributions.Select(road => CreateRoadCatalogItem(road, activeKey)),
            MapEditMode.Station => CreateStationCatalogItems(activeKey),
            MapEditMode.Structure => CreateStructureCatalogItems(activeKey),
            MapEditMode.Train => mapViewport.TrainContributions.Select(train => CreateTrainCatalogItem(train, activeKey)),
            _ => Enumerable.Empty<BuildCatalogItem>()
        };

        string normalized = query.Trim();
        if (normalized.Length > 0)
        {
            items = items.Where(item => item.Matches(normalized));
        }

        return items.Take(700).ToList().AsReadOnly();
    }

    private IEnumerable<BuildCatalogItem> CreateRailCatalogItems(string activeKey)
    {
        yield return new BuildCatalogItem(
            "rail:standard",
            "Standard rail",
            "Basic surface track",
            "Core",
            "rail standard track core",
            activeKey == "rail:standard",
            () => new RailPlacementPreviewControl(),
            () => mapViewport.SelectSpecialRail(null));

        foreach (SpecialRailContribution rail in mapViewport.SpecialRailContributions)
        {
            string title = MapViewport.FormatSpecialRailDisplayName(rail);
            string key = $"rail:{rail.Id}:{rail.Kind}";
            yield return new BuildCatalogItem(
                key,
                title,
                string.IsNullOrWhiteSpace(rail.Description) ? $"{rail.Kind} rail" : rail.Description,
                rail.PluginDirectoryName,
                $"{title} {rail.Description} {rail.PluginDirectoryName} {rail.Id} {rail.Class.Name}",
                activeKey == key,
                () => new RailPlacementPreviewControl(),
                () => mapViewport.SelectSpecialRail(rail));
        }
    }

    private BuildCatalogItem CreateRoadCatalogItem(RoadContribution road, string activeKey)
    {
        string key = $"road:{road.Id}:{road.PluginDirectoryName}";
        string style = road.Style.Lanes > 0
            ? $"{road.Style.MajorType} {road.Style.Lanes} lane(s)"
            : road.Kind.ToString();
        return new BuildCatalogItem(
            key,
            road.DisplayName,
            $"{style} | {RoadDetail(road)}",
            road.PluginDirectoryName,
            $"{road.DisplayName} {road.Description} {road.PluginDirectoryName} {road.Id} {road.Kind} {style}",
            activeKey == key,
            () => new RoadPlacementPreviewControl(road),
            () => mapViewport.SelectRoad(road));
    }

    private IEnumerable<BuildCatalogItem> CreateStationCatalogItems(string activeKey)
    {
        foreach (IGrouping<string, StationContribution> group in mapViewport.StationContributions.GroupBy(StationCatalogGroupKey))
        {
            yield return CreateStationCatalogItem(group.ToList(), activeKey);
        }
    }

    private BuildCatalogItem CreateStationCatalogItem(IReadOnlyList<StationContribution> stations, string activeKey)
    {
        StationContribution station = stations.First();
        StationContribution displayStation = stations.FirstOrDefault(candidate => activeKey == StationCatalogItemKey(candidate))
            ?? station;
        bool active = stations.Any(candidate => activeKey == StationCatalogItemKey(candidate));
        IReadOnlyList<BuildCatalogOptionGroup> optionGroups = CreateStationCatalogOptionGroups(stations, displayStation, activeKey);
        string size = $"{displayStation.SizeH}x{displayStation.SizeV}";
        string optionDetail = StationOptionDetail(stations);
        string searchText = string.Join(" ", stations.Select(candidate =>
            $"{candidate.DisplayName} {candidate.Group} {candidate.PluginDirectoryName} {candidate.Id} {candidate.SizeH}x{candidate.SizeV}"));

        return new BuildCatalogItem(
            StationCatalogGroupKey(station),
            StationCatalogTitle(stations),
            $"{size} | upkeep {FormatMoney(displayStation.OperationCost)}{optionDetail}",
            station.PluginDirectoryName,
            searchText,
            active,
            () => new StationPlacementPreviewControl(displayStation),
            () => mapViewport.SelectStation(displayStation),
            optionGroups);
    }

    private IReadOnlyList<BuildCatalogOptionGroup> CreateStationCatalogOptionGroups(
        IReadOnlyList<StationContribution> stations,
        StationContribution displayStation,
        string activeKey)
    {
        StationVariantLayout layout = CreateStationVariantLayout(stations, displayStation);
        List<BuildCatalogOptionGroup> groups = new();
        if (layout.Palettes.Count > 1)
        {
            groups.Add(new BuildCatalogOptionGroup(
                "Color",
                layout.Palettes.Select(palette =>
                {
                    StationVariantItem target = FindStationVariant(layout, palette.Key, layout.Active.DirectionKey, layout.Active.StyleKey)
                        ?? palette.FirstItem;
                    return new BuildCatalogOption(
                        palette.Label,
                        $"{palette.Label} | {target.Station.SizeH}x{target.Station.SizeV}",
                        palette.Key == layout.Active.PaletteKey,
                        () => mapViewport.SelectStation(target.Station),
                        createSwatches: () => StationColorSwatches(target.Station, palette.Label));
                }).ToList().AsReadOnly()));
        }

        if (layout.Directions.Count > 1)
        {
            groups.Add(new BuildCatalogOptionGroup(
                "Direction",
                layout.Directions.Select(direction =>
                {
                    StationVariantItem target = FindStationVariant(layout, layout.Active.PaletteKey, direction.Key, layout.Active.StyleKey)
                        ?? direction.FirstItem;
                    return new BuildCatalogOption(
                        direction.Label,
                        $"{direction.Label} | {target.Station.SizeH}x{target.Station.SizeV}",
                        direction.Key == layout.Active.DirectionKey,
                        () => mapViewport.SelectStation(target.Station));
                }).ToList().AsReadOnly()));
        }

        if (layout.Styles.Count > 1)
        {
            groups.Add(new BuildCatalogOptionGroup(
                "Style",
                layout.Styles.Select(style =>
                {
                    StationVariantItem target = FindStationVariant(layout, layout.Active.PaletteKey, layout.Active.DirectionKey, style.Key)
                        ?? style.FirstItem;
                    return new BuildCatalogOption(
                        style.Label,
                        $"{style.Label} | {target.Station.SizeH}x{target.Station.SizeV}",
                        style.Key == layout.Active.StyleKey,
                        () => mapViewport.SelectStation(target.Station));
                }).ToList().AsReadOnly()));
        }

        return groups.AsReadOnly();
    }

    private static string StationCatalogItemKey(StationContribution station)
    {
        return $"station:{station.Id}:{station.PluginDirectoryName}";
    }

    private static string StationCatalogGroupKey(StationContribution station)
    {
        string family = !string.IsNullOrWhiteSpace(station.Group)
            ? station.Group
            : StripTrailingVariantLabel(station.DisplayName);
        int shortSide = Math.Min(Math.Max(1, station.SizeH), Math.Max(1, station.SizeV));
        int longSide = Math.Max(Math.Max(1, station.SizeH), Math.Max(1, station.SizeV));
        return string.Join("|",
            station.PluginDirectoryName,
            NormalizeCatalogText(family),
            shortSide,
            longSide,
            station.OperationCost);
    }

    private static string StationCatalogTitle(IReadOnlyList<StationContribution> stations)
    {
        string title = stations
            .Select(station => station.Group)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? StripTrailingVariantLabel(stations[0].DisplayName);
        return string.IsNullOrWhiteSpace(title) ? stations[0].DisplayName : title;
    }

    private static string StationVariantTitle(StationContribution station, int index)
    {
        string family = !string.IsNullOrWhiteSpace(station.Group)
            ? station.Group
            : "";
        string title = !string.IsNullOrWhiteSpace(family) && station.DisplayName.StartsWith(family, StringComparison.OrdinalIgnoreCase)
            ? station.DisplayName[family.Length..].Trim()
            : station.DisplayName;
        return string.IsNullOrWhiteSpace(title)
            ? $"Variant {index + 1}"
            : title;
    }

    private static string StationOptionDetail(IReadOnlyList<StationContribution> stations)
    {
        StationVariantLayout layout = CreateStationVariantLayout(stations, stations[0]);
        List<string> details = new();
        if (layout.Palettes.Count > 1)
        {
            details.Add($"{layout.Palettes.Count} colors");
        }

        if (layout.Directions.Count > 1)
        {
            details.Add($"{layout.Directions.Count} directions");
        }

        if (layout.Styles.Count > 1)
        {
            details.Add($"{layout.Styles.Count} styles");
        }

        return details.Count == 0 ? "" : $" | {string.Join(" | ", details)}";
    }

    private static string StationColorLabel(StationContribution station)
    {
        string name = station.DisplayName.Trim();
        int open = name.LastIndexOf('(');
        if (open >= 0 && name.EndsWith(')') && open < name.Length - 2)
        {
            return name[(open + 1)..^1].Trim();
        }

        return "Default";
    }

    private static StationVariantLayout CreateStationVariantLayout(IReadOnlyList<StationContribution> stations, StationContribution activeStation)
    {
        bool hasColorMapAxis = stations
            .Select(StationColorMapSignature)
            .Where(signature => signature.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Skip(1)
            .Any();
        bool hasNamedColorAxis = !hasColorMapAxis
            && stations.Select(StationNameColorLabel)
                .Where(label => label.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Any();

        List<StationVariantItem> items = stations
            .Select((station, index) =>
            {
                string paletteKey = hasColorMapAxis
                    ? StationColorMapSignature(station)
                    : hasNamedColorAxis
                        ? StationNameColorLabel(station)
                        : "default";
                string directionKey = hasColorMapAxis
                    ? StationGeometrySignature(station)
                    : StationNameDirectionLabel(station) is { Length: > 0 } direction
                        ? direction
                        : StationGeometrySignature(station);
                string styleKey = hasColorMapAxis
                    ? "default"
                    : StationNameStyleLabel(station);
                if (string.IsNullOrWhiteSpace(styleKey))
                {
                    styleKey = "default";
                }

                return new StationVariantItem(station, index, paletteKey, directionKey, styleKey);
            })
            .ToList();

        StationVariantItem active = items.FirstOrDefault(item => StationCatalogItemKey(item.Station) == StationCatalogItemKey(activeStation))
            ?? items[0];

        return new StationVariantLayout(
            items.AsReadOnly(),
            CreateStationVariantAxis(items, item => item.PaletteKey, (item, ordinal) => PaletteAxisLabel(item, ordinal, hasColorMapAxis || hasNamedColorAxis)),
            CreateStationVariantAxis(items, item => item.DirectionKey, DirectionAxisLabel),
            CreateStationVariantAxis(items, item => item.StyleKey, StyleAxisLabel),
            active);
    }

    private static IReadOnlyList<StationVariantAxisValue> CreateStationVariantAxis(
        IReadOnlyList<StationVariantItem> items,
        Func<StationVariantItem, string> keySelector,
        Func<StationVariantItem, int, string> labelSelector)
    {
        List<StationVariantAxisValue> values = new();
        foreach (IGrouping<string, StationVariantItem> group in items.GroupBy(keySelector, StringComparer.OrdinalIgnoreCase))
        {
            StationVariantItem first = group.First();
            values.Add(new StationVariantAxisValue(group.Key, labelSelector(first, values.Count), first));
        }

        return values.AsReadOnly();
    }

    private static StationVariantItem? FindStationVariant(StationVariantLayout layout, string paletteKey, string directionKey, string styleKey)
    {
        return layout.Items.FirstOrDefault(item =>
                item.PaletteKey.Equals(paletteKey, StringComparison.OrdinalIgnoreCase)
                && item.DirectionKey.Equals(directionKey, StringComparison.OrdinalIgnoreCase)
                && item.StyleKey.Equals(styleKey, StringComparison.OrdinalIgnoreCase))
            ?? layout.Items.FirstOrDefault(item =>
                item.PaletteKey.Equals(paletteKey, StringComparison.OrdinalIgnoreCase)
                && item.DirectionKey.Equals(directionKey, StringComparison.OrdinalIgnoreCase))
            ?? layout.Items.FirstOrDefault(item =>
                item.PaletteKey.Equals(paletteKey, StringComparison.OrdinalIgnoreCase))
            ?? layout.Items.FirstOrDefault(item =>
                item.DirectionKey.Equals(directionKey, StringComparison.OrdinalIgnoreCase))
            ?? layout.Items.FirstOrDefault(item =>
                item.StyleKey.Equals(styleKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string PaletteAxisLabel(StationVariantItem item, int ordinal, bool hasPaletteAxis)
    {
        string label = StationNameColorLabel(item.Station);
        if (label.Length > 0 && hasPaletteAxis)
        {
            return label;
        }

        return $"Color {ordinal + 1}";
    }

    private static string DirectionAxisLabel(StationVariantItem item, int ordinal)
    {
        string label = StationNameDirectionLabel(item.Station);
        return label.Length > 0 && label.Length <= 3
            ? label
            : $"{ordinal + 1}";
    }

    private static string StyleAxisLabel(StationVariantItem item, int ordinal)
    {
        string label = StationNameStyleLabel(item.Station);
        return label.Length > 0 && !label.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? label
            : $"{ordinal + 1}";
    }

    private static string StationColorMapSignature(StationContribution station)
    {
        return string.Join(";", StationFrames(station)
            .SelectMany(frame => frame.ColorMapVariants.Count > 0
                ? frame.ColorMapVariants.SelectMany(variant => variant)
                : frame.ColorMaps)
            .Select(map => $"{map.From}>{map.To}"));
    }

    private static string StationGeometrySignature(StationContribution station)
    {
        SpriteFrame? frame = StationFrames(station).FirstOrDefault();
        return frame is null
            ? $"{station.SizeH}x{station.SizeV}"
            : $"{station.SizeH}x{station.SizeV}:{Path.GetFileName(frame.ResolvedPath)}:{frame.SourceX},{frame.SourceY},{frame.SourceWidth},{frame.SourceHeight}:{frame.OffsetX},{frame.OffsetY}";
    }

    private static string StationNameColorLabel(StationContribution station)
    {
        string label = StationColorLabel(station);
        return IsDirectionToken(label) || label.Equals("Default", StringComparison.OrdinalIgnoreCase)
            ? ""
            : label;
    }

    private static string StationNameDirectionLabel(StationContribution station)
    {
        string name = station.DisplayName.Trim();
        string parenthetical = LastParenthetical(name);
        if (IsDirectionToken(parenthetical))
        {
            return parenthetical;
        }

        char last = name.Length > 0 ? name[^1] : '\0';
        return IsDirectionToken(last.ToString()) ? last.ToString().ToUpperInvariant() : "";
    }

    private static string StationNameStyleLabel(StationContribution station)
    {
        string name = station.DisplayName.Trim();
        string family = station.Group.Trim();
        if (family.Length > 0 && name.StartsWith(family, StringComparison.OrdinalIgnoreCase))
        {
            name = name[family.Length..].Trim();
        }

        string color = StationNameColorLabel(station);
        if (color.Length > 0 && name.EndsWith($"({color})", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^(color.Length + 2)].Trim();
        }

        string direction = StationNameDirectionLabel(station);
        if (direction.Length > 0)
        {
            if (name.EndsWith($"({direction})", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^(direction.Length + 2)].Trim();
            }
            else if (name.EndsWith(direction, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^direction.Length].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(name) ? "default" : name;
    }

    private static string LastParenthetical(string value)
    {
        int open = value.LastIndexOf('(');
        return open >= 0 && value.EndsWith(')') && open < value.Length - 2
            ? value[(open + 1)..^1].Trim()
            : "";
    }

    private static bool IsDirectionToken(string value)
    {
        return value.Equals("L", StringComparison.OrdinalIgnoreCase)
            || value.Equals("R", StringComparison.OrdinalIgnoreCase)
            || value.Equals("N", StringComparison.OrdinalIgnoreCase)
            || value.Equals("S", StringComparison.OrdinalIgnoreCase)
            || value.Equals("E", StringComparison.OrdinalIgnoreCase)
            || value.Equals("W", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<BuildCatalogItem> CreateStructureCatalogItems(string activeKey)
    {
        foreach (IReadOnlyList<SpriteContribution> group in StructureCatalogGroups())
        {
            yield return CreateStructureCatalogItem(group, activeKey);
        }
    }

    private IReadOnlyList<IReadOnlyList<SpriteContribution>> StructureCatalogGroups()
    {
        structureCatalogGroups ??= mapViewport.StructureContributions
            .GroupBy(StructureCatalogGroupKey)
            .Select(group => (IReadOnlyList<SpriteContribution>)group.ToList().AsReadOnly())
            .ToList()
            .AsReadOnly();
        return structureCatalogGroups;
    }

    private BuildCatalogItem CreateStructureCatalogItem(IReadOnlyList<SpriteContribution> structures, string activeKey)
    {
        SpriteContribution structure = structures.First();
        SpriteContribution displayStructure = structures.FirstOrDefault(candidate => activeKey == StructureCatalogItemKey(candidate))
            ?? structure;
        bool active = structures.Any(candidate => activeKey == StructureCatalogItemKey(candidate));
        int colorVariantIndex = active ? mapViewport.ActiveStructureColorVariantIndex : 0;
        int frameVariantIndex = active ? mapViewport.ActiveStructureFrameVariantIndex : 0;
        IReadOnlyList<BuildCatalogOptionGroup> optionGroups = CreateStructureCatalogOptionGroups(structures, displayStructure, activeKey);
        string variantDetail = structures.Count > 1
            ? $" | {structures.Count} variants"
            : "";
        string frameDetail = displayStructure.Frames.Count > 1
            ? $" | {displayStructure.Frames.Count} directions"
            : "";
        int colorVariantCount = GetStructureColorVariantCount(displayStructure);
        string colorDetail = active && colorVariantCount > 1
            ? $" | {mapViewport.ActiveStructureColorVariantDescription}"
            : colorVariantCount > 1
                ? $" | {colorVariantCount} colors"
                : "";
        string detail = $"{StructureDetail(displayStructure)}{variantDetail}{frameDetail}{colorDetail}";
        string searchText = string.Join(" ", structures.Select(candidate =>
            $"{candidate.DisplayName} {candidate.Group} {candidate.Subgroup} {candidate.Description} {candidate.Type} {candidate.PlacementKind} {candidate.PluginDirectoryName} {candidate.Id}"));

        return new BuildCatalogItem(
            StructureCatalogGroupKey(structure),
            StructureCatalogTitle(structures),
            detail,
            structure.PluginDirectoryName,
            searchText,
            active,
            () => new StructurePlacementPreviewControl(displayStructure, colorVariantIndex, frameVariantIndex),
            () => mapViewport.SelectStructure(displayStructure),
            optionGroups);
    }

    private IReadOnlyList<BuildCatalogOptionGroup> CreateStructureCatalogOptionGroups(
        IReadOnlyList<SpriteContribution> structures,
        SpriteContribution displayStructure,
        string activeKey)
    {
        List<BuildCatalogOptionGroup> groups = new();
        if (structures.Count > 1)
        {
            groups.Add(new BuildCatalogOptionGroup(
                "Variant",
                structures.Select((structure, index) => new BuildCatalogOption(
                    $"{index + 1}",
                    $"{structure.DisplayName} | {StructureDetail(structure)}",
                    activeKey == StructureCatalogItemKey(structure),
                    () => mapViewport.SelectStructure(structure)))
                .ToList()
                .AsReadOnly()));
        }

        if (displayStructure.Frames.Count > 1)
        {
            groups.Add(new BuildCatalogOptionGroup(
                "Direction",
                Enumerable.Range(0, displayStructure.Frames.Count)
                    .Select(frame =>
                    {
                        int capturedFrame = frame;
                        return new BuildCatalogOption(
                            $"{frame + 1}",
                            $"Direction {frame + 1}",
                            mapViewport.ActiveStructureFrameVariantIndex == frame && activeKey == StructureCatalogItemKey(displayStructure),
                            () => mapViewport.SelectStructureFrameVariant(displayStructure, capturedFrame));
                    })
                    .ToList()
                    .AsReadOnly()));
        }

        int colorVariantCount = GetStructureColorVariantCount(displayStructure);
        if (colorVariantCount > 1)
        {
            groups.Add(new BuildCatalogOptionGroup(
                "Color",
                Enumerable.Range(0, colorVariantCount)
                    .Select(color =>
                    {
                        int capturedColor = color;
                        return new BuildCatalogOption(
                            $"{color + 1}",
                            $"Color {color + 1}",
                            mapViewport.ActiveStructureColorVariantIndex == color && activeKey == StructureCatalogItemKey(displayStructure),
                            () => mapViewport.SelectStructureColorVariant(displayStructure, capturedColor),
                            createSwatches: () => StructureColorSwatches(displayStructure, capturedColor));
                    })
                    .ToList()
                    .AsReadOnly()));
        }

        return groups.AsReadOnly();
    }

    private int GetStructureColorVariantCount(SpriteContribution structure)
    {
        string key = StructureCatalogItemKey(structure);
        if (structureColorVariantCountCache.TryGetValue(key, out int count))
        {
            return count;
        }

        count = mapViewport.GetStructureColorVariantCount(structure);
        structureColorVariantCountCache[key] = count;
        return count;
    }

    private static string StructureCatalogItemKey(SpriteContribution structure)
    {
        string key = $"structure:{structure.Id}:{structure.PluginDirectoryName}";
        return key;
    }

    private static string StructureCatalogGroupKey(SpriteContribution structure)
    {
        string family = !string.IsNullOrWhiteSpace(structure.Name)
            ? structure.Name
            : !string.IsNullOrWhiteSpace(structure.Subgroup)
                ? structure.Subgroup
                : structure.DisplayName;
        int shortSide = Math.Min(Math.Max(1, structure.SizeX), Math.Max(1, structure.SizeY));
        int longSide = Math.Max(Math.Max(1, structure.SizeX), Math.Max(1, structure.SizeY));
        int height = Math.Max(structure.Height, structure.MaxHeight);
        return string.Join("|",
            structure.PluginDirectoryName,
            structure.PlacementKind,
            structure.Type,
            NormalizeCatalogText(family),
            shortSide,
            longSide,
            height,
            structure.Price,
            structure.PopulationBase);
    }

    private static string StructureCatalogTitle(IReadOnlyList<SpriteContribution> structures)
    {
        if (structures.Select(structure => structure.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).Count() == 1)
        {
            return structures[0].DisplayName;
        }

        string title = structures
            .Select(structure => structure.Name)
            .Concat(structures.Select(structure => structure.Subgroup))
            .Concat(structures.Select(structure => structure.Group))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? structures[0].DisplayName;
        return title;
    }

    private static string NormalizeCatalogText(string value)
    {
        return string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim().ToUpperInvariant();
    }

    private static string StripTrailingVariantLabel(string value)
    {
        string trimmed = value.Trim();
        int open = trimmed.LastIndexOf('(');
        return open > 0 && trimmed.EndsWith(')')
            ? trimmed[..open].Trim()
            : trimmed;
    }

    private static Color SwatchForColorIndex(int index)
    {
        Color[] colors =
        {
            Color.FromRgb(63, 129, 183),
            Color.FromRgb(159, 55, 58),
            Color.FromRgb(64, 143, 85),
            Color.FromRgb(180, 145, 50),
            Color.FromRgb(123, 83, 159),
            Color.FromRgb(42, 145, 151),
            Color.FromRgb(198, 106, 47),
            Color.FromRgb(112, 112, 112)
        };
        return colors[(int)((uint)index % colors.Length)];
    }

    private IReadOnlyList<Color> StructureColorSwatches(SpriteContribution structure, int colorVariantIndex)
    {
        string key = $"{structure.PluginDirectoryName}|{structure.Id}|{colorVariantIndex}";
        if (buildCatalogSwatchCache.TryGetValue(key, out IReadOnlyList<Color>? cached))
        {
            return cached;
        }

        List<Color> colors = StructureFrames(structure)
            .Select(frame => (Frame: frame, ColorMaps: frame.ColorMapsForVariant(colorVariantIndex)))
            .Where(item => item.Frame.IsLoadable && item.ColorMaps.Count > 0)
            .SelectMany(frame => LegacyBitmap.SampleMappedColorsWithColorKey(
                frame.Frame.ResolvedPath,
                frame.ColorMaps,
                colorVariantIndex,
                new PixelRect(frame.Frame.SourceX, frame.Frame.SourceY, frame.Frame.SourceWidth, frame.Frame.SourceHeight)))
            .Distinct()
            .Take(4)
            .ToList();

        if (colors.Count == 0)
        {
            colors.Add(SwatchForColorIndex(colorVariantIndex));
        }

        IReadOnlyList<Color> swatches = colors.AsReadOnly();
        buildCatalogSwatchCache[key] = swatches;
        return swatches;
    }

    private IReadOnlyList<Color> StationColorSwatches(StationContribution station, string colorLabel)
    {
        string key = $"{station.PluginDirectoryName}|{station.Id}|station";
        if (buildCatalogSwatchCache.TryGetValue(key, out IReadOnlyList<Color>? cached))
        {
            return cached;
        }

        List<Color> colors = StationFrames(station)
            .Where(frame => frame.IsLoadable)
            .SelectMany(frame => LegacyBitmap.SampleDominantColorsWithColorKey(
                frame.ResolvedPath,
                new PixelRect(frame.SourceX, frame.SourceY, frame.SourceWidth, frame.SourceHeight),
                frame.ColorMapsForVariant(0)))
            .Distinct()
            .Take(4)
            .ToList();

        if (colors.Count == 0)
        {
            colors.Add(SwatchForColorLabel(colorLabel));
        }

        IReadOnlyList<Color> swatches = colors.AsReadOnly();
        buildCatalogSwatchCache[key] = swatches;
        return swatches;
    }

    private static IEnumerable<SpriteFrame> StationFrames(StationContribution station)
    {
        if (station.Frame is { } frame)
        {
            yield return frame;
        }

        if (station.SpriteSet2D is { } spriteSet)
        {
            foreach (ModernSpriteVoxel2D voxel in spriteSet.InVoxelDrawOrder())
            {
                yield return voxel.Frame;
            }
        }
    }

    private static IEnumerable<SpriteFrame> StructureFrames(SpriteContribution structure)
    {
        foreach (SpriteFrame frame in structure.Frames)
        {
            yield return frame;
        }

        if (structure.SpriteSet2D is { } spriteSet2D)
        {
            foreach (ModernSpriteVoxel2D voxel in spriteSet2D.InVoxelDrawOrder())
            {
                yield return voxel.Frame;
            }
        }

        if (structure.SpriteSet3D is { } spriteSet3D)
        {
            foreach (ModernSpriteVoxel3D voxel in spriteSet3D.InVoxelDrawOrder())
            {
                yield return voxel.Frame;
            }
        }
    }

    private static Color SwatchForColorLabel(string label)
    {
        string normalized = label.Replace("-", " ", StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Contains("WINE", StringComparison.Ordinal) || normalized.Contains("RED", StringComparison.Ordinal))
        {
            return Color.FromRgb(144, 48, 62);
        }

        if (normalized.Contains("TEAL", StringComparison.Ordinal) || normalized.Contains("GREEN", StringComparison.Ordinal) || normalized.Contains("OLIVE", StringComparison.Ordinal))
        {
            return Color.FromRgb(57, 132, 96);
        }

        if (normalized.Contains("BLUE", StringComparison.Ordinal) || normalized.Contains("PEACOCK", StringComparison.Ordinal) || normalized.Contains("MINT", StringComparison.Ordinal))
        {
            return Color.FromRgb(53, 131, 170);
        }

        if (normalized.Contains("SEPIA", StringComparison.Ordinal) || normalized.Contains("BROWN", StringComparison.Ordinal))
        {
            return Color.FromRgb(137, 97, 63);
        }

        if (normalized.Contains("YELLOW", StringComparison.Ordinal) || normalized.Contains("OCHER", StringComparison.Ordinal) || normalized.Contains("OCHRE", StringComparison.Ordinal))
        {
            return Color.FromRgb(190, 151, 54);
        }

        return SwatchForColorIndex(StringComparer.OrdinalIgnoreCase.GetHashCode(label));
    }
}
