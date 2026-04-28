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
    private void ApplyStatus(MapViewportStatus status)
    {
        hudCashValue.Text = FormatMoney(status.Cash);
        hudWorldValue.Text = status.WorldName;
        hudDateValue.Text = status.Clock.Format(ModernTextLanguage.English);
        hudTrafficValue.Text = $"{status.WaitingPassengers:N0}/{status.StationPopulation:N0}";
        bottomStatusText.Text = $"{status.InteractionHint} {status.LastMessage}";
        bottomDetailText.Text = CreateStatusDetails(status);
        UpdatePlacementPreview(status);
        UpdateBuildCatalog(status);

        UpdateModeButtons(status.EditMode);
        UpdateContextPanels(status.EditMode);
        UpdateRailwayOperationButtons(status);
        UpdateSimulationText();
    }

    private void UpdateRailwayOperationButtons(MapViewportStatus status)
    {
        storeTrainButton.IsEnabled = status.CanStoreSelectedTrain;
        dispatchTrainButton.IsEnabled = status.CanDispatchSelectedTrain;
        removeTrainButton.IsEnabled = status.CanRemoveSelectedTrain;
    }

    private void UpdatePlacementPreview(MapViewportStatus status)
    {
        (string PlatformTitle, string PlatformDetail) = PlatformPreviewLabels(status.ActivePlatformDescription);
        (string Key, string Title, string Detail, Func<Control> Create) preview = status.EditMode switch
        {
            MapEditMode.Select when status.SelectedLocation is not null => (
                $"select:{status.SelectionTitle}:{status.SelectionDetail}",
                status.SelectionTitle,
                status.SelectionDetail,
                () => new EmptyPlacementPreviewControl()),
            MapEditMode.Rail => (
                $"rail:{status.ActiveRailName}",
                status.ActiveRailName,
                "Click and drag a rail line",
                () => new RailPlacementPreviewControl()),
            MapEditMode.Road when mapViewport.ActiveRoadContribution is { } road => (
                $"road:{road.Id}",
                road.DisplayName,
                RoadDetail(road),
                () => new RoadPlacementPreviewControl(road)),
            MapEditMode.Station when mapViewport.ActiveStationContribution is { } station => (
                $"station:{station.Id}",
                station.DisplayName,
                $"{station.SizeH}x{station.SizeV} | {FormatMoney(station.OperationCost)} upkeep",
                () => new StationPlacementPreviewControl(station)),
            MapEditMode.Structure when mapViewport.ActiveStructureContribution is { } structure => (
                $"structure:{structure.Id}:{mapViewport.ActiveStructureFrameVariantIndex}:{mapViewport.ActiveStructureColorVariantIndex}",
                structure.DisplayName,
                $"{StructureDetail(structure)} | {mapViewport.ActiveStructureColorVariantDescription}",
                () => new StructurePlacementPreviewControl(structure, mapViewport.ActiveStructureColorVariantIndex, mapViewport.ActiveStructureFrameVariantIndex)),
            MapEditMode.Platform => (
                $"platform:{status.ActivePlatformDescription}",
                PlatformTitle,
                PlatformDetail,
                () => new PlatformPlacementPreviewControl(status.ActivePlatformDescription)),
            MapEditMode.Train when mapViewport.ActiveTrainContribution is { } train => (
                $"train:{train.Id}",
                train.DisplayName,
                $"{FormatMoney(train.Price)} | fare {FormatMoney(train.Fare)}",
                () => new TrainPlacementPreviewControl(train, mapViewport.TrainCarContributions)),
            MapEditMode.Terrain => (
                "terrain",
                "Terrain",
                "Selected corner",
                () => new EmptyPlacementPreviewControl()),
            _ => (
                $"none:{status.EditMode}",
                "",
                "",
                () => new EmptyPlacementPreviewControl())
        };

        bool hasPreview = status.EditMode == MapEditMode.Select && status.SelectedLocation is not null
            || status.EditMode is MapEditMode.Rail
            or MapEditMode.Road
            or MapEditMode.Station
            or MapEditMode.Structure
            or MapEditMode.Platform
            or MapEditMode.Train
            or MapEditMode.Terrain;
        placementPreviewPanel.IsVisible = hasPreview;
        placementPreviewPanel.Margin = new Thickness(8, ToolOverlayTop(status.EditMode), 0, 0);
        placementPreviewPanel.Width = status.EditMode == MapEditMode.Select ? 340 : 148;
        if (!hasPreview)
        {
            if (placementPreviewContent.Content is IDisposable oldPreview)
            {
                oldPreview.Dispose();
            }

            placementPreviewKey = preview.Key;
            placementPreviewTitle.Text = "";
            placementPreviewDetail.Text = "";
            placementPreviewContent.Content = null;
            return;
        }

        placementPreviewTitle.Text = preview.Title;
        placementPreviewDetail.Text = preview.Detail;
        placementPreviewContent.IsVisible = status.EditMode is not (MapEditMode.Select or MapEditMode.Terrain);
        if (placementPreviewKey == preview.Key)
        {
            return;
        }

        if (placementPreviewContent.Content is IDisposable disposable)
        {
            disposable.Dispose();
        }

        placementPreviewKey = preview.Key;
        placementPreviewContent.Content = preview.Create();
    }

    private BuildCatalogItem CreateTrainCatalogItem(TrainContribution train, string activeKey)
    {
        string key = $"train:{train.Id}:{train.PluginDirectoryName}";
        string company = string.IsNullOrWhiteSpace(train.Company) ? train.PluginTitle : train.Company;
        string detail = $"{FormatMoney(train.Price)} | fare {FormatMoney(train.Fare)}";
        if (!string.IsNullOrWhiteSpace(company))
        {
            detail = $"{company} | {detail}";
        }

        return new BuildCatalogItem(
            key,
            train.DisplayName,
            detail,
            train.PluginDirectoryName,
            $"{train.DisplayName} {train.TypeName} {train.Company} {train.Description} {train.Author} {train.PluginDirectoryName} {train.Id}",
            activeKey == key,
            () => new TrainPlacementPreviewControl(train, mapViewport.TrainCarContributions),
            () => mapViewport.SelectTrain(train));
    }

    private static string RoadDetail(RoadContribution road)
    {
        return road.Style.Lanes > 0
            ? $"{road.Style.MajorType}, {road.Style.Lanes} lane(s)"
            : road.Kind.ToString();
    }

    private static string StructureDetail(SpriteContribution structure)
    {
        string kind = structure.PlacementKind switch
        {
            SpriteContributionPlacementKind.RailStationary => "rail accessory",
            SpriteContributionPlacementKind.RoadAccessory => "road accessory",
            SpriteContributionPlacementKind.ElectricPole => "electric pole",
            SpriteContributionPlacementKind.VariableHeightBuilding => "variable-height building",
            _ => "structure"
        };
        int height = Math.Max(structure.Height, structure.MaxHeight);
        string size = $"{Math.Max(1, structure.SizeX)}x{Math.Max(1, structure.SizeY)}";
        string price = structure.Price > 0 ? $" | {FormatMoney(structure.Price)}" : "";
        string population = structure.PopulationBase > 0 ? $" | pop {structure.PopulationBase:N0}" : "";
        return $"{kind}, {size}x{Math.Max(1, height)}{price}{population}";
    }

    private static (string Title, string Detail) PlatformPreviewLabels(string description)
    {
        string[] parts = description.Split(", ", 3, StringSplitOptions.TrimEntries);
        return parts.Length == 3
            ? (parts[2], $"{parts[0]}, {parts[1]}")
            : ("Platform option", description);
    }

    private static double ToolOverlayTop(MapEditMode mode)
    {
        int index = mode switch
        {
            MapEditMode.Select => 0,
            MapEditMode.Rail => 1,
            MapEditMode.Road => 2,
            MapEditMode.Station => 3,
            MapEditMode.Structure => 4,
            MapEditMode.Platform => 5,
            MapEditMode.Train => 6,
            MapEditMode.Terrain => 7,
            MapEditMode.Erase => 8,
            _ => 0
        };
        return 6 + index * 44;
    }

    private void UpdateModeButtons(MapEditMode activeMode)
    {
        foreach ((MapEditMode mode, ToggleButton button) in modeButtons)
        {
            bool active = mode == activeMode;
            button.IsChecked = active;
            button.Background = active ? ActiveToolBrush : Brushes.White;
            button.Foreground = DarkTextBrush;
        }
    }

    private void UpdateContextPanels(MapEditMode activeMode)
    {
        selectContextPanel.IsVisible = activeMode == MapEditMode.Select;
        railContextPanel.IsVisible = activeMode == MapEditMode.Rail;
        roadContextPanel.IsVisible = activeMode == MapEditMode.Road;
        stationContextPanel.IsVisible = activeMode == MapEditMode.Station;
        structureContextPanel.IsVisible = activeMode == MapEditMode.Structure;
        platformContextPanel.IsVisible = activeMode == MapEditMode.Platform;
        trainContextPanel.IsVisible = activeMode == MapEditMode.Train;
        terrainContextPanel.IsVisible = activeMode == MapEditMode.Terrain;
        eraseContextPanel.IsVisible = activeMode == MapEditMode.Erase;
    }

    private void ToggleSimulation()
    {
        SetSimulationRunning(!simulationRunning);
    }

    private void SetSimulationRunning(bool running)
    {
        simulationRunning = running;
        if (running)
        {
            simulationTimer.Start();
        }
        else
        {
            simulationTimer.Stop();
        }

        UpdateSimulationText();
    }

    private void ChangeSimulationSpeed(int delta)
    {
        simulationSpeedIndex = Math.Clamp(simulationSpeedIndex + delta, 0, simulationSpeeds.Length - 1);
        UpdateSimulationText();
    }

    private void ChangeHeightCut(int delta)
    {
        mapViewport.MaxVisibleLevel = Math.Clamp(
            mapViewport.MaxVisibleLevel + delta,
            0,
            mapViewport.WorldMaxHeightCutLevel);
    }

    private void UpdateSimulationText()
    {
        if (simulationStepValue is not null)
        {
            simulationStepValue.Text = $"{simulationSpeeds[simulationSpeedIndex]}m";
        }

        if (playPauseButton is not null)
        {
            playPauseButton.Content = HudIconOnly(simulationRunning ? ToolGlyphKind.Pause : ToolGlyphKind.Play);
        }
    }

    private void ToggleDeveloperMode()
    {
        SetDeveloperMode(!developerModeVisible);
    }

    private void SetDeveloperMode(bool visible)
    {
        developerModeVisible = visible;
        developerDrawer.IsVisible = visible;
        developerColumn.Width = new GridLength(visible ? 380 : 0);
    }

    private static string FormatCompactLocation(TileLocation? location)
    {
        return location is { } tile
            ? $"{tile.H},{tile.V},{tile.Z} {tile.Corner}"
            : "-";
    }

    private static string CreateStatusDetails(MapViewportStatus status)
    {
        string anchor = status.BuildAnchorLocation is null
            ? ""
            : $" | Anchor {FormatCompactLocation(status.BuildAnchorLocation)}";
        string selection = status.SelectedLocation is null ? "" : $" | {status.SelectionDetail}";
        return $"Hover {FormatCompactLocation(status.HoverLocation)} | Selected {FormatCompactLocation(status.SelectedLocation)}{anchor}{selection} | {status.EditMode} | Road {status.ActiveRoadName} | Station {status.ActiveStationName} | Platform {status.ActivePlatformDescription} | Train {status.ActiveTrainName} | Zoom {status.Zoom:0.##}x | Cut {status.MaxVisibleLevel}/{status.WorldMaxHeightCutLevel} | Rail {status.RailTileCount:N0} Road {status.RoadTileCount:N0} Stations {status.StationCount:N0} Platforms {status.PlatformCount:N0} Trains {status.TrainCount:N0} | Pop {status.StationPopulation:N0} Waiting {status.WaitingPassengers:N0} Loaded {status.LoadedPassengersToday:N0} Unloaded {status.UnloadedPassengersToday:N0} Stops {status.TrainStopsToday:N0}";
    }

    private static string FormatMoney(long amount)
    {
        return amount < 0
            ? $"-JPY {Math.Abs(amount):N0}"
            : $"JPY {amount:N0}";
    }

    private void LoadInitialPreview()
    {
        string? splash = assets.FindResource("splash.jpg");
        if (splash is not null)
        {
            ShowAsset(new LegacyAsset("splash.jpg", "Image", splash));
        }
        else
        {
            debugSelectionText.Text = assets.CoreResourceDirectory;
        }
    }

    private void ShowAsset(LegacyAsset asset)
    {
        try
        {
            previewImage.Source = new Bitmap(asset.Path);
            debugSelectionText.Text = asset.Path;
        }
        catch (Exception ex)
        {
            previewImage.Source = null;
            debugSelectionText.Text = $"{asset.Path}\n{ex.Message}";
        }
    }

    private void OpenResourceFolder()
    {
        string command;
        string arguments;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            command = "open";
            arguments = assets.CoreResourceDirectory;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            command = "explorer.exe";
            arguments = assets.CoreResourceDirectory;
        }
        else
        {
            command = "xdg-open";
            arguments = assets.CoreResourceDirectory;
        }

        Process.Start(new ProcessStartInfo(command, arguments) { UseShellExecute = false });
    }

    private async Task ShowNewWorldDialogAsync()
    {
        NewWorldDialog dialog = new(ModernWorldCreationOptions.Default);
        ModernWorldCreationOptions? options = await dialog.ShowDialog<ModernWorldCreationOptions?>(this);
        if (options is null)
        {
            return;
        }

        mapViewport.CreateNewWorld(options);
        ShowMessage($"Created new world: {options.Normalize().Name}");
    }

    private void SaveWorldSnapshot()
    {
        ModernWorldSnapshot snapshot = mapViewport.CreateWorldSnapshot();
        JsonSerializerOptions options = new() { WriteIndented = true };
        File.WriteAllText(worldSnapshotPath, JsonSerializer.Serialize(snapshot, options));
        ShowMessage($"Saved world snapshot: {worldSnapshotPath}");
    }

    private void LoadWorldSnapshot()
    {
        if (!File.Exists(worldSnapshotPath))
        {
            ShowMessage($"No world snapshot found: {worldSnapshotPath}");
            return;
        }

        ModernWorldSnapshot? snapshot = JsonSerializer.Deserialize<ModernWorldSnapshot>(File.ReadAllText(worldSnapshotPath));
        if (snapshot is null)
        {
            ShowMessage($"Could not load world snapshot: {worldSnapshotPath}");
            return;
        }

        mapViewport.LoadWorldSnapshot(snapshot);
        ShowMessage($"Loaded world snapshot: {worldSnapshotPath}");
    }

    private void ShowMessage(string message)
    {
        bottomStatusText.Text = message;
    }

    protected override void OnClosed(EventArgs e)
    {
        simulationTimer.Stop();
        if (placementPreviewContent.Content is IDisposable preview)
        {
            preview.Dispose();
            placementPreviewContent.Content = null;
        }

        mapViewport.Dispose();
        toolbarIconStrip?.Dispose();
        base.OnClosed(e);
    }
}
