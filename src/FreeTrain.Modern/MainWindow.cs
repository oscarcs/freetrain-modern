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

public sealed class MainWindow : Window
{
    private readonly LegacyAssetCatalog assets;
    private readonly PluginManifestCatalog plugins;
    private readonly Image previewImage;
    private readonly TextBlock debugSelectionText;
    private readonly MapViewport mapViewport;
    private readonly string worldSnapshotPath;
    private readonly DispatcherTimer simulationTimer;
    private readonly Bitmap? toolbarIconStrip;
    private readonly Dictionary<MapEditMode, ToggleButton> modeButtons = new();
    private readonly Dictionary<string, IReadOnlyList<Color>> buildCatalogSwatchCache = new(StringComparer.Ordinal);
    private readonly int[] simulationSpeeds = { 10, 30, 60, 180, 360 };

    private ColumnDefinition developerColumn = null!;
    private Border developerDrawer = null!;
    private Button playPauseButton = null!;
    private TextBlock simulationStepValue = null!;
    private Border placementPreviewPanel = null!;
    private ContentControl placementPreviewContent = null!;
    private TextBlock placementPreviewTitle = null!;
    private TextBlock placementPreviewDetail = null!;
    private Border buildCatalogPanel = null!;
    private TextBlock buildCatalogTitle = null!;
    private TextBlock buildCatalogSummary = null!;
    private TextBox buildCatalogSearch = null!;
    private ItemsControl buildCatalogList = null!;
    private ScrollViewer buildCatalogScrollViewer = null!;
    private Border selectContextPanel = null!;
    private Border railContextPanel = null!;
    private Border roadContextPanel = null!;
    private Border stationContextPanel = null!;
    private Border structureContextPanel = null!;
    private Border platformContextPanel = null!;
    private Border trainContextPanel = null!;
    private Border terrainContextPanel = null!;
    private Border eraseContextPanel = null!;
    private Button storeTrainButton = null!;
    private Button dispatchTrainButton = null!;
    private Button removeTrainButton = null!;
    private TextBlock hudCashValue = null!;
    private TextBlock hudWorldValue = null!;
    private TextBlock hudDateValue = null!;
    private TextBlock hudTrafficValue = null!;
    private TextBlock bottomStatusText = null!;
    private TextBlock bottomDetailText = null!;

    private bool developerModeVisible;
    private bool simulationRunning;
    private int simulationSpeedIndex;
    private string placementPreviewKey = "";
    private string buildCatalogQuery = "";
    private string buildCatalogStateKey = "";

    public MainWindow(LegacyAssetCatalog assets)
    {
        this.assets = assets;
        plugins = new PluginManifestCatalog(assets.PluginDirectory);

        Title = "FreeTrain Modern";
        MinWidth = 1040;
        MinHeight = 680;
        Width = 1320;
        Height = 820;
        FontSize = 12;

        previewImage = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        debugSelectionText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = DebugMutedBrush
        };

        mapViewport = new MapViewport(assets, plugins);
        mapViewport.StatusChanged += ApplyStatus;
        worldSnapshotPath = Path.Combine(AppContext.BaseDirectory, "modern-world.snapshot.json");
        toolbarIconStrip = LoadIconStrip("Toolbar.bmp");

        simulationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(550)
        };
        simulationTimer.Tick += (_, _) => mapViewport.AdvanceClock(simulationSpeeds[simulationSpeedIndex]);

        Content = BuildLayout();
        LoadInitialPreview();
        ApplyStatus(mapViewport.CurrentStatus);
    }

    private static IBrush AppBackgroundBrush => new SolidColorBrush(Color.FromRgb(18, 21, 24));
    private static IBrush ChromeBrush => new SolidColorBrush(Color.FromRgb(31, 36, 41));
    private static IBrush ChromeRaisedBrush => new SolidColorBrush(Color.FromRgb(42, 48, 54));
    private static IBrush PanelBrush => new SolidColorBrush(Color.FromRgb(246, 248, 249));
    private static IBrush AccentBrush => new SolidColorBrush(Color.FromRgb(198, 83, 184));
    private static IBrush ActiveToolBrush => new SolidColorBrush(Color.FromRgb(238, 218, 236));
    private static IBrush TextBrush => new SolidColorBrush(Color.FromRgb(242, 244, 246));
    private static IBrush MutedTextBrush => new SolidColorBrush(Color.FromRgb(163, 171, 178));
    private static IBrush DarkTextBrush => new SolidColorBrush(Color.FromRgb(33, 37, 41));
    private static IBrush DarkMutedTextBrush => new SolidColorBrush(Color.FromRgb(102, 110, 116));
    private static IBrush DebugMutedBrush => new SolidColorBrush(Color.FromRgb(186, 192, 198));

    private Bitmap? LoadIconStrip(string resourceName)
    {
        string? path = assets.FindResource(resourceName);
        return path is null ? null : LegacyBitmap.LoadWithColorKey(path);
    }

    private Control BuildLayout()
    {
        DockPanel root = new()
        {
            Background = AppBackgroundBrush
        };

        Menu menu = BuildMenu();
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);

        Grid shell = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,0"),
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };
        developerColumn = shell.ColumnDefinitions[2];

        Control hud = BuildHud();
        Grid.SetColumnSpan(hud, 3);
        shell.Children.Add(hud);

        Control toolbar = BuildMapToolbar();
        Grid.SetRow(toolbar, 1);
        shell.Children.Add(toolbar);

        Control mapSurface = BuildMapSurface();
        Grid.SetColumn(mapSurface, 1);
        Grid.SetRow(mapSurface, 1);
        shell.Children.Add(mapSurface);

        Grid statusContent = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        bottomStatusText = new TextBlock
        {
            Foreground = DarkMutedTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(14, 7)
        };
        statusContent.Children.Add(bottomStatusText);

        bottomDetailText = new TextBlock
        {
            Foreground = DarkMutedTextBrush,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(12, 7, 14, 7),
            MaxWidth = 720
        };
        Grid.SetColumn(bottomDetailText, 1);
        statusContent.Children.Add(bottomDetailText);

        Border statusBar = new()
        {
            Background = Brushes.White,
            Child = statusContent
        };
        Grid.SetRow(statusBar, 2);
        Grid.SetColumnSpan(statusBar, 2);
        shell.Children.Add(statusBar);

        developerDrawer = BuildDeveloperDrawer();
        developerDrawer.IsVisible = false;
        Grid.SetColumn(developerDrawer, 2);
        Grid.SetRow(developerDrawer, 1);
        Grid.SetRowSpan(developerDrawer, 2);
        shell.Children.Add(developerDrawer);

        root.Children.Add(shell);
        return root;
    }

    private Menu BuildMenu()
    {
        Menu menu = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(9, 11, 13)),
            Foreground = TextBrush
        };

        MenuItem file = new() { Header = "_File" };
        file.Items.Add(new MenuItem
        {
            Header = "_New World...",
            Command = MiniCommand.Create(() => _ = ShowNewWorldDialogAsync())
        });
        file.Items.Add(new Separator());
        file.Items.Add(new MenuItem
        {
            Header = "_Open Legacy Resource Folder",
            Command = MiniCommand.Create(OpenResourceFolder)
        });
        file.Items.Add(new MenuItem
        {
            Header = "_Save World Snapshot",
            Command = MiniCommand.Create(SaveWorldSnapshot)
        });
        file.Items.Add(new MenuItem
        {
            Header = "_Load World Snapshot",
            Command = MiniCommand.Create(LoadWorldSnapshot)
        });
        file.Items.Add(new Separator());
        file.Items.Add(new MenuItem
        {
            Header = "E_xit",
            Command = MiniCommand.Create(Close)
        });
        menu.Items.Add(file);

        MenuItem view = new() { Header = "_View" };
        view.Items.Add(new MenuItem
        {
            Header = "Zoom _In",
            Command = MiniCommand.Create(() => mapViewport.Zoom += 0.25)
        });
        view.Items.Add(new MenuItem
        {
            Header = "Zoom _Out",
            Command = MiniCommand.Create(() => mapViewport.Zoom -= 0.25)
        });
        view.Items.Add(new MenuItem
        {
            Header = "_Reset Zoom",
            Command = MiniCommand.Create(() => mapViewport.Zoom = 1.0)
        });
        view.Items.Add(new Separator());
        view.Items.Add(new MenuItem
        {
            Header = "Toggle _Grid",
            Command = MiniCommand.Create(() => mapViewport.ShowGrid = !mapViewport.ShowGrid)
        });
        view.Items.Add(new MenuItem
        {
            Header = "Toggle _Night View",
            Command = MiniCommand.Create(() => mapViewport.UseNightView = !mapViewport.UseNightView)
        });
        view.Items.Add(new Separator());
        view.Items.Add(new MenuItem
        {
            Header = "Cut _Higher",
            Command = MiniCommand.Create(() => ChangeHeightCut(1))
        });
        view.Items.Add(new MenuItem
        {
            Header = "Cut _Lower",
            Command = MiniCommand.Create(() => ChangeHeightCut(-1))
        });
        view.Items.Add(new Separator());
        view.Items.Add(new MenuItem
        {
            Header = "_Developer Tools",
            Command = MiniCommand.Create(ToggleDeveloperMode)
        });
        menu.Items.Add(view);

        MenuItem simulation = new() { Header = "_Simulation" };
        simulation.Items.Add(new MenuItem
        {
            Header = simulationRunning ? "_Pause" : "_Play",
            Command = MiniCommand.Create(ToggleSimulation)
        });
        simulation.Items.Add(new MenuItem
        {
            Header = "Advance _10 Minutes",
            Command = MiniCommand.Create(() => mapViewport.AdvanceClock(10))
        });
        simulation.Items.Add(new MenuItem
        {
            Header = "Advance _1 Hour",
            Command = MiniCommand.Create(() => mapViewport.AdvanceClock(60))
        });
        menu.Items.Add(simulation);

        return menu;
    }

    private Control BuildHud()
    {
        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Background = ChromeBrush,
            MinHeight = 48
        };

        StackPanel metrics = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(12, 6),
            VerticalAlignment = VerticalAlignment.Center
        };

        hudCashValue = CreateHudMetric(metrics, "Cash", "JPY 0", emphasize: true);
        hudWorldValue = CreateHudMetric(metrics, "World", "");
        hudDateValue = CreateHudMetric(metrics, "Date", "");
        hudTrafficValue = CreateHudMetric(metrics, "Passengers", "");
        grid.Children.Add(metrics);

        Control simulationControls = BuildHudSimulationControls();
        Grid.SetColumn(simulationControls, 1);
        grid.Children.Add(simulationControls);

        return grid;
    }

    private Control BuildHudSimulationControls()
    {
        StackPanel controls = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 7, 12, 7)
        };

        playPauseButton = HudButton(HudIconOnly(ToolGlyphKind.Play), (_, _) => ToggleSimulation(), "Play or pause the simulation clock");
        controls.Children.Add(playPauseButton);
        controls.Children.Add(HudButton("10m", (_, _) => mapViewport.AdvanceClock(10), "Advance ten minutes"));
        controls.Children.Add(HudButton("1h", (_, _) => mapViewport.AdvanceClock(60), "Advance one hour"));
        controls.Children.Add(HudButton(HudIconOnly(ToolGlyphKind.Slower), (_, _) => ChangeSimulationSpeed(-1), "Slower simulation step"));
        controls.Children.Add(HudButton(HudIconOnly(ToolGlyphKind.Faster), (_, _) => ChangeSimulationSpeed(1), "Faster simulation step"));
        simulationStepValue = new TextBlock
        {
            Foreground = MutedTextBrush,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MinWidth = 42
        };
        controls.Children.Insert(controls.Children.Count - 1, simulationStepValue);

        return controls;
    }

    private static TextBlock CreateHudMetric(Panel parent, string label, string value, bool emphasize = false)
    {
        StackPanel metric = new()
        {
            Spacing = 1,
            MinWidth = emphasize ? 136 : 104
        };
        metric.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            Foreground = MutedTextBrush,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold
        });

        TextBlock valueBlock = new()
        {
            Text = value,
            Foreground = emphasize ? Brushes.White : TextBrush,
            FontSize = 14,
            FontWeight = emphasize ? FontWeight.Bold : FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        metric.Children.Add(valueBlock);
        parent.Children.Add(metric);
        return valueBlock;
    }

    private Control BuildMapToolbar()
    {
        StackPanel primaryTools = new()
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Top,
            Background = PanelBrush
        };
        primaryTools.Children.Add(ModeButton(new ToolGlyph(ToolGlyphKind.Pointer, 22), MapEditMode.Select, "Select: inspect tiles and objects"));
        primaryTools.Children.Add(ModeButton(ToolbarIcon(0, ToolGlyphKind.Rail), MapEditMode.Rail, "Rail: place track between selected tiles"));
        primaryTools.Children.Add(ModeButton(new ToolGlyph(ToolGlyphKind.Road, 22), MapEditMode.Road, "Road: place road between selected tiles"));
        primaryTools.Children.Add(ModeButton(new ToolGlyph(ToolGlyphKind.Station, 22), MapEditMode.Station, "Station: build station buildings"));
        primaryTools.Children.Add(ModeButton(new ToolGlyph(ToolGlyphKind.Structure, 22), MapEditMode.Structure, "Structure: build plugin structures and accessories"));
        primaryTools.Children.Add(ModeButton(new ToolGlyph(ToolGlyphKind.Platform, 22), MapEditMode.Platform, "Platform: build station platforms on rail"));
        primaryTools.Children.Add(ModeButton(new ToolGlyph(ToolGlyphKind.Train, 22), MapEditMode.Train, "Train: place a train on rail"));
        primaryTools.Children.Add(ModeButton(ToolbarIcon(7, ToolGlyphKind.Terrain), MapEditMode.Terrain, "Terrain: raise or lower corners"));
        primaryTools.Children.Add(ModeButton(ToolbarIcon(9, ToolGlyphKind.Bulldoze), MapEditMode.Erase, "Bulldoze: erase stations, platforms, rail, or road"));

        return new Border
        {
            Width = 52,
            Background = PanelBrush,
            BorderBrush = new SolidColorBrush(Color.FromRgb(221, 226, 229)),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = primaryTools
        };
    }

    private static Border ContextPanel(string title, params Control[] controls)
    {
        WrapPanel row = new()
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top
        };

        foreach (Control control in controls)
        {
            control.Margin = new Thickness(0, 0, 4, 4);
            row.Children.Add(control);
        }

        return new Border
        {
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = row
        };
    }

    private static Button ToolButton(object content, EventHandler<RoutedEventArgs> click, string tip)
    {
        Button button = new()
        {
            Content = content,
            Background = Brushes.White,
            Foreground = DarkTextBrush,
            FontSize = 11,
            MinWidth = 30,
            MinHeight = 28,
            Padding = new Thickness(7, 3),
            VerticalAlignment = VerticalAlignment.Top,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += click;
        ToolTip.SetTip(button, tip);
        return button;
    }

    private Border BuildPlacementOverlay()
    {
        placementPreviewTitle = new TextBlock
        {
            Foreground = DarkTextBrush,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 7, 8, 0)
        };
        placementPreviewContent = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 4, 8, 0)
        };
        placementPreviewDetail = new TextBlock
        {
            Foreground = DarkMutedTextBrush,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 3, 8, 6)
        };

        Grid contextGrid = new()
        {
            Margin = new Thickness(8, 0, 8, 8),
            VerticalAlignment = VerticalAlignment.Top
        };
        storeTrainButton = ToolButton("Store", (_, _) => mapViewport.StoreSelectedTrainInGarage(), "Store the selected train when every car is inside garage rail");
        dispatchTrainButton = ToolButton("Dispatch", (_, _) => mapViewport.DispatchSelectedTrainFromGarage(), "Dispatch the selected train from its garage track");
        removeTrainButton = ToolButton("Remove", (_, _) => mapViewport.RemoveSelectedTrain(), "Remove the selected train from the world");
        selectContextPanel = ContextPanel("Selection",
            storeTrainButton,
            dispatchTrainButton,
            removeTrainButton);
        railContextPanel = ContextPanel("Rail",
            ToolButton("<", (_, _) => mapViewport.SelectPreviousSpecialRail(), "Previous rail type"),
            ToolButton(">", (_, _) => mapViewport.SelectNextSpecialRail(), "Next rail type"),
            ToolButton("Z-", (_, _) => mapViewport.ChangeRailBuildLevel(-1), "Lower elevated rail target level"),
            ToolButton("Z+", (_, _) => mapViewport.ChangeRailBuildLevel(1), "Raise elevated rail target level"));
        roadContextPanel = ContextPanel("Road",
            ToolButton("<", (_, _) => mapViewport.SelectPreviousRoad(), "Previous road contribution"),
            ToolButton(">", (_, _) => mapViewport.SelectNextRoad(), "Next road contribution"));
        stationContextPanel = ContextPanel("Station Building",
            ToolButton("<", (_, _) => mapViewport.SelectPreviousStation(), "Previous station contribution"),
            ToolButton(">", (_, _) => mapViewport.SelectNextStation(), "Next station contribution"));
        structureContextPanel = ContextPanel("Structure",
            ToolButton("<", (_, _) => mapViewport.SelectPreviousStructure(), "Previous structure contribution"),
            ToolButton(">", (_, _) => mapViewport.SelectNextStructure(), "Next structure contribution"),
            ToolButton("Dir", (_, _) => mapViewport.CycleStructureVariant(), "Cycle structure/accessory variant"),
            ToolButton("C-", (_, _) => mapViewport.ChangeStructureColorVariant(-1), "Previous color palette variant"),
            ToolButton("C+", (_, _) => mapViewport.ChangeStructureColorVariant(1), "Next color palette variant"));
        platformContextPanel = ContextPanel("Platform",
            ToolButton("-", (_, _) => mapViewport.ChangePlatformLength(-1), "Shorter platform"),
            ToolButton("+", (_, _) => mapViewport.ChangePlatformLength(1), "Longer platform"),
            ToolButton("Dir", (_, _) => mapViewport.RotatePlatformDirection(), "Rotate platform direction"),
            ToolButton("Style", (_, _) => mapViewport.CyclePlatformStyle(), "Cycle thin, roofed, and wide platforms"));
        trainContextPanel = ContextPanel("Train",
            ToolButton("<", (_, _) => mapViewport.SelectPreviousTrain(), "Previous train contribution"),
            ToolButton(">", (_, _) => mapViewport.SelectNextTrain(), "Next train contribution"));
        terrainContextPanel = ContextPanel("Terrain",
            ToolButton("+", (_, _) => mapViewport.RaiseSelectedTerrain(), "Raise the selected terrain corner"),
            ToolButton("-", (_, _) => mapViewport.LowerSelectedTerrain(), "Lower the selected terrain corner"));
        eraseContextPanel = ContextPanel("Bulldoze");
        contextGrid.Children.Add(selectContextPanel);
        contextGrid.Children.Add(railContextPanel);
        contextGrid.Children.Add(roadContextPanel);
        contextGrid.Children.Add(stationContextPanel);
        contextGrid.Children.Add(structureContextPanel);
        contextGrid.Children.Add(platformContextPanel);
        contextGrid.Children.Add(trainContextPanel);
        contextGrid.Children.Add(terrainContextPanel);
        contextGrid.Children.Add(eraseContextPanel);

        StackPanel previewStack = new()
        {
            Spacing = 0
        };
        previewStack.Children.Add(placementPreviewTitle);
        previewStack.Children.Add(placementPreviewContent);
        previewStack.Children.Add(placementPreviewDetail);
        previewStack.Children.Add(contextGrid);

        placementPreviewPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(244, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(221, 226, 229)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Color.FromArgb(42, 0, 0, 0),
                Blur = 12,
                OffsetY = 3
            }),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MinWidth = 148,
            MaxWidth = 360,
            Child = previewStack
        };
        return placementPreviewPanel;
    }

    private Border BuildBuildCatalogPanel()
    {
        buildCatalogTitle = new TextBlock
        {
            Text = "Build Catalog",
            Foreground = DarkTextBrush,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        buildCatalogSummary = new TextBlock
        {
            Foreground = DarkMutedTextBrush,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        buildCatalogSearch = new TextBox
        {
            PlaceholderText = "Search",
            MinHeight = 30,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 8)
        };
        buildCatalogSearch.TextChanged += (_, _) =>
        {
            buildCatalogQuery = buildCatalogSearch.Text ?? "";
            buildCatalogStateKey = "";
            UpdateBuildCatalog(mapViewport.CurrentStatus);
        };

        buildCatalogList = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<BuildCatalogItem>((item, _) => BuildCatalogItemRow(item))
        };

        DockPanel content = new()
        {
            Margin = new Thickness(10)
        };
        StackPanel header = new()
        {
            Spacing = 1
        };
        header.Children.Add(buildCatalogTitle);
        header.Children.Add(buildCatalogSummary);
        header.Children.Add(buildCatalogSearch);
        DockPanel.SetDock(header, Dock.Top);
        content.Children.Add(header);
        buildCatalogScrollViewer = new ScrollViewer
        {
            Content = buildCatalogList,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        content.Children.Add(buildCatalogScrollViewer);

        buildCatalogPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(246, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 217, 221)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Color.FromArgb(46, 0, 0, 0),
                Blur = 14,
                OffsetY = 4
            }),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = 336,
            Margin = new Thickness(0, 8, 8, 8),
            Child = content
        };
        return buildCatalogPanel;
    }

    private static Control BuildCatalogItemRow(BuildCatalogItem? item)
    {
        if (item is null)
        {
            return new Border();
        }

        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("74,*"),
            MinHeight = 70
        };
        Control preview = item.CreatePreview();
        Border previewContainer = new()
        {
            Width = 66,
            Height = 48,
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220, 226, 230)),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new Viewbox
            {
                Stretch = Stretch.Uniform,
                Child = preview
            }
        };
        previewContainer.PointerPressed += (_, e) =>
        {
            item.Select();
            e.Handled = true;
        };
        grid.Children.Add(previewContainer);

        StackPanel copy = new()
        {
            Spacing = 2
        };
        copy.PointerPressed += (_, e) =>
        {
            item.Select();
            e.Handled = true;
        };
        copy.Children.Add(new TextBlock
        {
            Text = item.Title,
            Foreground = DarkTextBrush,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2
        });
        copy.Children.Add(new TextBlock
        {
            Text = item.Detail,
            Foreground = DarkMutedTextBrush,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2
        });
        copy.Children.Add(new TextBlock
        {
            Text = item.Source,
            Foreground = DarkMutedTextBrush,
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        StackPanel content = new()
        {
            Spacing = 6
        };
        content.Children.Add(grid);
        if (item.OptionGroups.Count > 0)
        {
            content.Children.Add(BuildCatalogOptionGroups(item));
        }

        return new Border
        {
            Background = item.IsActive ? ActiveToolBrush : Brushes.White,
            BorderBrush = item.IsActive
                ? AccentBrush
                : new SolidColorBrush(Color.FromRgb(226, 231, 234)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7),
            Margin = new Thickness(0, 0, 0, 6),
            Child = content
        };
    }

    private static Control BuildCatalogOptionGroups(BuildCatalogItem item)
    {
        StackPanel groups = new()
        {
            Margin = new Thickness(74, 0, 0, 0),
            Spacing = 4
        };

        foreach (BuildCatalogOptionGroup group in item.OptionGroups)
        {
            Grid row = new()
            {
                ColumnDefinitions = new ColumnDefinitions("54,*")
            };
            row.Children.Add(new TextBlock
            {
                Text = group.Title,
                Foreground = DarkMutedTextBrush,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });

            WrapPanel options = new()
            {
                ItemHeight = 26
            };
            foreach (BuildCatalogOption option in group.Options)
            {
                Button button = new()
                {
                    MinWidth = option.Swatches.Count == 0 ? 34 : 30,
                    Width = option.Swatches.Count == 0 ? double.NaN : 30,
                    Height = 22,
                    Padding = option.Swatches.Count == 0 ? new Thickness(8, 1) : new Thickness(2),
                    Margin = new Thickness(0, 0, 4, 4),
                    Background = option.IsActive ? ActiveToolBrush : Brushes.White,
                    BorderBrush = option.IsActive
                        ? AccentBrush
                        : new SolidColorBrush(Color.FromRgb(220, 226, 230)),
                    BorderThickness = new Thickness(1),
                    Content = option.Swatches.Count > 0
                        ? BuildColorSwatch(option.Swatches)
                        : new TextBlock
                        {
                            Text = option.Label,
                            FontSize = 10,
                            Foreground = DarkTextBrush,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                };
                button.Click += (_, e) =>
                {
                    e.Handled = true;
                    option.Select();
                };
                ToolTip.SetTip(button, option.ToolTip);
                options.Children.Add(button);
            }

            Grid.SetColumn(options, 1);
            row.Children.Add(options);
            groups.Children.Add(row);
        }

        return groups;
    }

    private static Control BuildColorSwatch(IReadOnlyList<Color> colors)
    {
        Grid swatch = new()
        {
            Width = 20,
            Height = 14,
            ClipToBounds = true
        };

        int count = Math.Clamp(colors.Count, 1, 4);
        swatch.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", count)));
        for (int i = 0; i < count; i++)
        {
            Border stripe = new()
            {
                Background = new SolidColorBrush(colors[i]),
                BorderBrush = i == 0 ? new SolidColorBrush(Color.FromRgb(92, 98, 104)) : null,
                BorderThickness = i == 0 ? new Thickness(1, 1, 0, 1) : new Thickness(0, 1, i == count - 1 ? 1 : 0, 1)
            };
            Grid.SetColumn(stripe, i);
            swatch.Children.Add(stripe);
        }

        return swatch;
    }

    private ToggleButton ModeButton(object content, MapEditMode mode, string tip)
    {
        ToggleButton button = new()
        {
            Content = content,
            Background = Brushes.White,
            Foreground = DarkTextBrush,
            Width = 40,
            Height = 40,
            Padding = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += (_, _) => mapViewport.EditMode = mode;
        ToolTip.SetTip(button, tip);
        modeButtons[mode] = button;
        return button;
    }

    private Control ToolbarIcon(int stripIndex, ToolGlyphKind fallback)
    {
        return toolbarIconStrip is null
            ? new ToolGlyph(fallback)
            : new StripIcon(toolbarIconStrip, stripIndex, 16, 15);
    }

    private static Control HudIconOnly(ToolGlyphKind glyph)
    {
        return new ToolGlyph(glyph, 16, Colors.White);
    }

    private static Button HudButton(object content, EventHandler<RoutedEventArgs> click, string tip)
    {
        Button button = new()
        {
            Content = content,
            Background = ChromeRaisedBrush,
            Foreground = TextBrush,
            FontSize = 11,
            MinWidth = 30,
            MinHeight = 30,
            Padding = new Thickness(7, 3),
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += click;
        ToolTip.SetTip(button, tip);
        return button;
    }

    private Control BuildMapSurface()
    {
        ScrollViewer scroller = new()
        {
            Content = mapViewport,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        void UpdateViewport()
        {
            mapViewport.SetVisibleContentViewport(scroller.Offset, scroller.Viewport);
        }

        scroller.ScrollChanged += (_, _) => UpdateViewport();
        scroller.SizeChanged += (_, _) => UpdateViewport();
        mapViewport.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty)
            {
                UpdateViewport();
            }
        };
        Dispatcher.UIThread.Post(UpdateViewport, DispatcherPriority.Loaded);

        Grid surface = new();
        surface.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(92, 162, 182)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(16, 19, 22)),
            BorderThickness = new Thickness(0),
            Child = scroller
        });
        surface.Children.Add(BuildPlacementOverlay());
        surface.Children.Add(BuildBuildCatalogPanel());
        return surface;
    }

    private Border BuildDeveloperDrawer()
    {
        DockPanel panel = new()
        {
            LastChildFill = true
        };

        Grid header = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Background = ChromeBrush,
            MinHeight = 48
        };
        header.Children.Add(new TextBlock
        {
            Text = "Developer Tools",
            Foreground = TextBrush,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0)
        });
        Button close = new()
        {
            Content = "Close",
            Background = ChromeRaisedBrush,
            Foreground = TextBrush,
            Padding = new Thickness(10, 4),
            Margin = new Thickness(8, 8, 12, 8),
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        close.Click += (_, _) => SetDeveloperMode(false);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        TabControl tabs = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(24, 28, 32)),
            Foreground = TextBrush,
            Items =
            {
                new TabItem { Header = "Assets", Content = BuildAssetDebugBrowser() },
                new TabItem { Header = "Pictures", Content = BuildPictureContributionBrowser() },
                new TabItem { Header = "Sprites", Content = BuildSpriteContributionBrowser() },
                new TabItem { Header = "Structures", Content = BuildStructureContributionBrowser() },
                new TabItem { Header = "Roads", Content = BuildRoadContributionBrowser() },
                new TabItem { Header = "Metadata", Content = BuildContributionMetadataBrowser() },
                new TabItem { Header = "Plugins", Content = BuildPluginBrowser() }
            }
        };
        panel.Children.Add(tabs);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(24, 28, 32)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 58, 64)),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = panel
        };
    }

    private Control BuildAssetDebugBrowser()
    {
        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(10)
        };

        TextBlock summary = new()
        {
            Text = $"{assets.Assets.Count} core assets",
            Foreground = TextBrush,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetColumnSpan(summary, 2);
        grid.Children.Add(summary);

        ListBox list = new()
        {
            ItemsSource = assets.Assets.Where(asset => asset.Kind == "Image").ToList(),
            Background = ChromeRaisedBrush,
            Foreground = TextBrush
        };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is LegacyAsset asset)
            {
                ShowAsset(asset);
            }
        };
        list.ItemTemplate = new FuncDataTemplate<LegacyAsset>((asset, _) =>
            new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(0, 4),
                Children =
                {
                    new TextBlock
                    {
                        Text = asset?.Name,
                        Foreground = TextBrush,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock { Text = asset?.Kind, Foreground = DebugMutedBrush, FontSize = 12 }
                }
            });
        Grid.SetRow(list, 1);
        grid.Children.Add(list);

        DockPanel preview = new()
        {
            Margin = new Thickness(10, 0, 0, 0)
        };
        debugSelectionText.Margin = new Thickness(0, 0, 0, 8);
        DockPanel.SetDock(debugSelectionText, Dock.Bottom);
        preview.Children.Add(debugSelectionText);
        preview.Children.Add(new Border
        {
            Background = Brushes.White,
            Child = new ScrollViewer
            {
                Content = previewImage,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        });
        Grid.SetColumn(preview, 1);
        Grid.SetRow(preview, 1);
        grid.Children.Add(preview);

        return grid;
    }

    private Control BuildPluginBrowser()
    {
        Grid grid = new()
        {
            RowDefinitions = new RowDefinitions("Auto,2*,*"),
            Margin = new Thickness(10)
        };

        TextBlock summary = new()
        {
            Text = $"{plugins.LoadedCount} manifests parsed | {plugins.ErrorCount} errors | {plugins.ContributionTypeCounts.Values.Sum()} contributions | {plugins.Localization.AvailableTranslationCount} {plugins.Localization.Language} translations",
            Foreground = TextBrush,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        grid.Children.Add(summary);

        ListBox list = new()
        {
            ItemsSource = plugins.Plugins,
            Background = ChromeRaisedBrush,
            Foreground = TextBrush
        };
        list.ItemTemplate = new FuncDataTemplate<PluginManifest>((plugin, _) =>
            new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(0, 4),
                Children =
                {
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(plugin?.Title) ? plugin?.DirectoryName : plugin?.Title,
                        Foreground = TextBrush,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = plugin is null
                            ? ""
                            : plugin.IsLoaded
                                ? $"{plugin.ContributionCount} contributions"
                                : $"Error: {plugin.Error}",
                        Foreground = plugin?.IsLoaded == true ? DebugMutedBrush : Brushes.IndianRed,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            });
        Grid.SetRow(list, 1);
        grid.Children.Add(list);

        ContentControl details = new()
        {
            Margin = new Thickness(0, 10, 0, 0)
        };
        list.SelectionChanged += (_, _) =>
        {
            details.Content = list.SelectedItem is PluginManifest plugin
                ? BuildPluginDetails(plugin)
                : null;
        };
        Grid.SetRow(details, 2);
        grid.Children.Add(details);

        if (plugins.Plugins.Count > 0)
        {
            list.SelectedIndex = 0;
        }

        return grid;
    }

    private static Control BuildPluginDetails(PluginManifest plugin)
    {
        StackPanel panel = new()
        {
            Spacing = 6
        };

        panel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(plugin.Title) ? plugin.DirectoryName : plugin.Title,
            Foreground = TextBrush,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Folder: {plugin.DirectoryName}",
            Foreground = DebugMutedBrush,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(plugin.Author))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Author: {plugin.Author}",
                Foreground = DebugMutedBrush,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!string.IsNullOrWhiteSpace(plugin.Version))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Version: {plugin.Version}",
                Foreground = DebugMutedBrush,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (plugin.Error is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = plugin.Error,
                Foreground = Brushes.IndianRed,
                TextWrapping = TextWrapping.Wrap
            });
        }

        string contributionSummary = string.Join("\n", plugin.Contributions
            .GroupBy(contribution => contribution.Type)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}: {group.Count()}"));

        string parsedSummary = string.Join("\n", new[]
        {
            ("factories", plugin.ContributionFactories.Count),
            ("menus", plugin.Menus.Count),
            ("docking", plugin.DockingContents.Count),
            ("account genres", plugin.AccountGenres.Count),
            ("special rails", plugin.SpecialRails.Count),
            ("special structures", plugin.SpecialStructures.Count),
            ("train controllers", plugin.TrainControllers.Count),
            ("new games", plugin.NewGames.Count),
            ("sprite factories", plugin.SpriteFactories.Count),
            ("sprite loaders", plugin.SpriteLoaders.Count),
            ("color libraries", plugin.ColorLibraries.Count),
            ("color-map train pictures", plugin.ColorMapTrainPictures.Count),
            ("departure bells", plugin.TrainDepartureBells.Count),
            ("rail signals", plugin.RailSignals.Count),
            ("dummy cars", plugin.DummyCars.Count),
            ("half-voxel structures", plugin.HalfVoxelStructures.Count)
        }.Where(item => item.Count > 0).Select(item => $"{item.Item1}: {item.Count}"));

        panel.Children.Add(new TextBlock
        {
            Text = contributionSummary.Length == 0 ? "No parsed contributions." : contributionSummary,
            Foreground = TextBrush,
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        if (parsedSummary.Length > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = parsedSummary,
                Foreground = DebugMutedBrush,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return new Border
        {
            Background = ChromeRaisedBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = new ScrollViewer { Content = panel }
        };
    }

    private Control BuildContributionMetadataBrowser()
    {
        StackPanel panel = new()
        {
            Spacing = 10,
            Margin = new Thickness(10)
        };

        panel.Children.Add(DebugSection("Runtime hooks",
            ("Contribution factories", plugins.ContributionFactories.Count),
            ("Menus", plugins.Menus.Count),
            ("Docking contents", plugins.DockingContents.Count),
            ("New-game providers", plugins.NewGames.Count),
            ("Train controllers", plugins.TrainControllers.Count)));
        panel.Children.Add(DebugSection("Simulation extensions",
            ("Account genres", plugins.AccountGenres.Count),
            ("Special rails", plugins.SpecialRails.Count),
            ("Special structures", plugins.SpecialStructures.Count),
            ("Rail signals", plugins.RailSignals.Count),
            ("Dummy cars", plugins.DummyCars.Count),
            ("Half-voxel structures", plugins.HalfVoxelStructures.Count)));
        panel.Children.Add(DebugSection("Graphics helpers",
            ("Sprite factories", plugins.SpriteFactories.Count),
            ("Sprite loaders", plugins.SpriteLoaders.Count),
            ("Color libraries", plugins.ColorLibraries.Count),
            ("Color-map train pictures", plugins.ColorMapTrainPictures.Count),
            ("Departure bells", plugins.TrainDepartureBells.Count)));

        panel.Children.Add(DebugList("Special rails", plugins.SpecialRails
            .Take(40)
            .Select(rail => $"{rail.PluginDirectoryName}: {rail.Class.Name}")));
        panel.Children.Add(DebugList("Train controllers", plugins.TrainControllers
            .Take(40)
            .Select(controller => $"{controller.PluginDirectoryName}: {FallbackName(controller.Name, controller.Class.Name)}")));
        panel.Children.Add(DebugList("New games", plugins.NewGames
            .Take(40)
            .Select(newGame => $"{newGame.PluginDirectoryName}: {FallbackName(newGame.Name, newGame.Class.Name)}")));
        panel.Children.Add(DebugList("Signals and vehicles", plugins.RailSignals
            .Take(25)
            .Select(signal => $"{signal.PluginDirectoryName}: {signal.Name} ({signal.Side})")
            .Concat(plugins.DummyCars.Take(25).Select(car => $"{car.PluginDirectoryName}: {car.Name}"))));

        return new ScrollViewer { Content = panel };
    }

    private static Control DebugSection(string title, params (string Label, int Count)[] counts)
    {
        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", counts.Length + 1))),
            Margin = new Thickness(0, 0, 0, 2)
        };

        TextBlock header = new()
        {
            Text = title,
            Foreground = TextBrush,
            FontWeight = FontWeight.Bold,
            FontSize = 14
        };
        Grid.SetColumnSpan(header, 2);
        grid.Children.Add(header);

        for (int i = 0; i < counts.Length; i++)
        {
            TextBlock label = new()
            {
                Text = counts[i].Label,
                Foreground = DebugMutedBrush,
                Margin = new Thickness(0, 4, 18, 0)
            };
            TextBlock value = new()
            {
                Text = counts[i].Count.ToString("N0"),
                Foreground = TextBrush,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(label, i + 1);
            Grid.SetRow(value, i + 1);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
        }

        return new Border
        {
            Background = ChromeRaisedBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = grid
        };
    }

    private static Control DebugList(string title, IEnumerable<string> lines)
    {
        string text = string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        return new Border
        {
            Background = ChromeRaisedBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = new TextBlock
            {
                Text = text.Length == 0 ? $"{title}: none" : $"{title}\n{text}",
                Foreground = TextBrush,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static string FallbackName(string primary, string fallback)
    {
        return !string.IsNullOrWhiteSpace(primary)
            ? primary
            : string.IsNullOrWhiteSpace(fallback) ? "(unnamed)" : fallback;
    }

    private Control BuildPictureContributionBrowser()
    {
        IReadOnlyList<PictureContribution> loadablePictures = plugins.Pictures
            .Where(picture => picture.IsLoadable)
            .Take(600)
            .ToList()
            .AsReadOnly();

        DockPanel panel = new()
        {
            Margin = new Thickness(10)
        };

        TextBlock summary = new()
        {
            Text = $"{loadablePictures.Count} loadable picture contributions shown | {plugins.Pictures.Count} parsed",
            Foreground = TextBrush,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(summary, Dock.Top);
        panel.Children.Add(summary);

        WrapPanel wrap = new()
        {
            ItemWidth = 136,
            ItemHeight = 120
        };

        foreach (PictureContribution picture in loadablePictures)
        {
            PictureContributionPreview preview = new(picture);
            ToolTip.SetTip(preview, $"{picture.DisplayName}\n{picture.Source}\n{picture.Id}");
            wrap.Children.Add(preview);
        }

        panel.Children.Add(new ScrollViewer
        {
            Content = wrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        return panel;
    }

    private Control BuildStructureContributionBrowser()
    {
        IReadOnlyList<SpriteContribution> loadableStructures = plugins.Structures
            .Concat(plugins.RailStationaries)
            .Concat(plugins.RoadAccessories)
            .Concat(plugins.ElectricPoles)
            .Where(sprite => sprite.IsLoadable)
            .OrderBy(sprite => sprite.PlacementKind)
            .ThenBy(sprite => sprite.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sprite => sprite.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(600)
            .ToList()
            .AsReadOnly();

        DockPanel panel = new()
        {
            Margin = new Thickness(10)
        };

        TextBlock summary = new()
        {
            Text = $"{loadableStructures.Count} loadable structure/accessory contributions shown | {plugins.Structures.Count} structures | {plugins.RailStationaries.Count} rail accessories | {plugins.RoadAccessories.Count} road accessories | {plugins.ElectricPoles.Count} electric poles",
            Foreground = TextBrush,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(summary, Dock.Top);
        panel.Children.Add(summary);

        WrapPanel wrap = new()
        {
            ItemWidth = 164,
            ItemHeight = 144
        };

        foreach (SpriteContribution structure in loadableStructures)
        {
            SpriteContributionPreview preview = new(structure);
            ToolTip.SetTip(preview, $"{structure.DisplayName}\n{structure.PlacementKind} / {structure.Type}\n{structure.PluginDirectoryName}/{structure.Id}");
            wrap.Children.Add(preview);
        }

        panel.Children.Add(new ScrollViewer
        {
            Content = wrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        return panel;
    }

    private Control BuildSpriteContributionBrowser()
    {
        IReadOnlyList<SpriteContribution> loadableSprites = plugins.Sprites
            .Where(sprite => sprite.IsLoadable)
            .Take(600)
            .ToList()
            .AsReadOnly();

        DockPanel panel = new()
        {
            Margin = new Thickness(10)
        };

        TextBlock summary = new()
        {
            Text = $"{loadableSprites.Count} loadable sprite contributions shown | {plugins.Sprites.Count} parsed",
            Foreground = TextBrush,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(summary, Dock.Top);
        panel.Children.Add(summary);

        WrapPanel wrap = new()
        {
            ItemWidth = 164,
            ItemHeight = 144
        };

        foreach (SpriteContribution sprite in loadableSprites)
        {
            SpriteContributionPreview preview = new(sprite);
            ToolTip.SetTip(preview, $"{sprite.DisplayName}\n{sprite.Type}\n{sprite.PluginDirectoryName}/{sprite.Id}");
            wrap.Children.Add(preview);
        }

        panel.Children.Add(new ScrollViewer
        {
            Content = wrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        return panel;
    }

    private Control BuildRoadContributionBrowser()
    {
        IReadOnlyList<RoadContribution> loadableRoads = plugins.Roads
            .Where(road => road.IsLoadable)
            .Take(600)
            .ToList()
            .AsReadOnly();

        DockPanel panel = new()
        {
            Margin = new Thickness(10)
        };

        TextBlock summary = new()
        {
            Text = $"{loadableRoads.Count} loadable road contributions shown | {plugins.Roads.Count} parsed",
            Foreground = TextBrush,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(summary, Dock.Top);
        panel.Children.Add(summary);

        WrapPanel wrap = new()
        {
            ItemWidth = 172,
            ItemHeight = 152
        };

        foreach (RoadContribution road in loadableRoads)
        {
            RoadContributionPreview preview = new(road);
            ToolTip.SetTip(preview, $"{road.DisplayName}\n{road.Kind}\n{road.PluginDirectoryName}/{road.Id}");
            wrap.Children.Add(preview);
        }

        panel.Children.Add(new ScrollViewer
        {
            Content = wrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        return panel;
    }

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

        Vector scrollOffset = buildCatalogScrollViewer.Offset;
        buildCatalogList.ItemsSource = items;
        buildCatalogScrollViewer.Offset = scrollOffset;
        Dispatcher.UIThread.Post(() => buildCatalogScrollViewer.Offset = scrollOffset, DispatcherPriority.Loaded);
        buildCatalogStateKey = nextStateKey;
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
                        StationColorSwatches(target.Station, palette.Label));
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
        foreach (IGrouping<string, SpriteContribution> group in mapViewport.StructureContributions.GroupBy(StructureCatalogGroupKey))
        {
            yield return CreateStructureCatalogItem(group.ToList(), activeKey);
        }
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
        string colorDetail = active && mapViewport.ActiveStructureColorVariantCount > 1
            ? $" | {mapViewport.ActiveStructureColorVariantDescription}"
            : mapViewport.GetStructureColorVariantCount(displayStructure) > 1
                ? $" | {mapViewport.GetStructureColorVariantCount(displayStructure)} colors"
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
                            () =>
                            {
                                mapViewport.SelectStructure(displayStructure);
                                mapViewport.SelectStructureFrameVariant(capturedFrame);
                            });
                    })
                    .ToList()
                    .AsReadOnly()));
        }

        int colorVariantCount = mapViewport.GetStructureColorVariantCount(displayStructure);
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
                            () =>
                            {
                                mapViewport.SelectStructure(displayStructure);
                                mapViewport.SelectStructureColorVariant(capturedColor);
                            },
                            StructureColorSwatches(displayStructure, color));
                    })
                    .ToList()
                    .AsReadOnly()));
        }

        return groups.AsReadOnly();
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

    private sealed class BuildCatalogItem
    {
        public BuildCatalogItem(
            string key,
            string title,
            string detail,
            string source,
            string searchText,
            bool isActive,
            Func<Control> createPreview,
            Action select,
            IReadOnlyList<BuildCatalogOptionGroup>? optionGroups = null)
        {
            Key = key;
            Title = string.IsNullOrWhiteSpace(title) ? "(unnamed)" : title;
            Detail = detail;
            Source = source;
            SearchText = searchText;
            IsActive = isActive;
            CreatePreview = createPreview;
            Select = select;
            OptionGroups = optionGroups ?? Array.Empty<BuildCatalogOptionGroup>();
        }

        public string Key { get; }
        public string Title { get; }
        public string Detail { get; }
        public string Source { get; }
        public string SearchText { get; }
        public bool IsActive { get; }
        public Func<Control> CreatePreview { get; }
        public Action Select { get; }
        public IReadOnlyList<BuildCatalogOptionGroup> OptionGroups { get; }

        public bool Matches(string query)
        {
            return SearchText.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class BuildCatalogOptionGroup
    {
        public BuildCatalogOptionGroup(string title, IReadOnlyList<BuildCatalogOption> options)
        {
            Title = title;
            Options = options;
        }

        public string Title { get; }
        public IReadOnlyList<BuildCatalogOption> Options { get; }
    }

    private sealed class BuildCatalogOption
    {
        public BuildCatalogOption(
            string title,
            string detail,
            bool isActive,
            Action select,
            IReadOnlyList<Color>? swatches = null)
        {
            Label = title;
            Detail = detail;
            IsActive = isActive;
            Select = select;
            Swatches = swatches ?? Array.Empty<Color>();
        }

        public string Label { get; }
        public string Detail { get; }
        public bool IsActive { get; }
        public Action Select { get; }
        public IReadOnlyList<Color> Swatches { get; }
        public string ToolTip => string.IsNullOrWhiteSpace(Detail) ? Label : $"{Label}\n{Detail}";
    }

    private sealed record StationVariantLayout(
        IReadOnlyList<StationVariantItem> Items,
        IReadOnlyList<StationVariantAxisValue> Palettes,
        IReadOnlyList<StationVariantAxisValue> Directions,
        IReadOnlyList<StationVariantAxisValue> Styles,
        StationVariantItem Active);

    private sealed record StationVariantItem(
        StationContribution Station,
        int OriginalIndex,
        string PaletteKey,
        string DirectionKey,
        string StyleKey);

    private sealed record StationVariantAxisValue(
        string Key,
        string Label,
        StationVariantItem FirstItem);

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
