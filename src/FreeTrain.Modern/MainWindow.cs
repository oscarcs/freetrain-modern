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
    private readonly int[] simulationSpeeds = { 10, 30, 60, 180, 360 };

    private ColumnDefinition developerColumn = null!;
    private Border developerDrawer = null!;
    private Button playPauseButton = null!;
    private TextBlock simulationStepValue = null!;
    private Border placementPreviewPanel = null!;
    private ContentControl placementPreviewContent = null!;
    private TextBlock placementPreviewTitle = null!;
    private TextBlock placementPreviewDetail = null!;
    private Border selectContextPanel = null!;
    private Border railContextPanel = null!;
    private Border roadContextPanel = null!;
    private Border stationContextPanel = null!;
    private Border platformContextPanel = null!;
    private Border trainContextPanel = null!;
    private Border terrainContextPanel = null!;
    private Border eraseContextPanel = null!;
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
        StackPanel row = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Top
        };

        foreach (Control control in controls)
        {
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
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 3, 8, 6)
        };

        Grid contextGrid = new()
        {
            Margin = new Thickness(8, 0, 8, 8),
            VerticalAlignment = VerticalAlignment.Top
        };
        selectContextPanel = ContextPanel("Selection");
        railContextPanel = ContextPanel("Rail");
        roadContextPanel = ContextPanel("Road",
            ToolButton("<", (_, _) => mapViewport.SelectPreviousRoad(), "Previous road contribution"),
            ToolButton(">", (_, _) => mapViewport.SelectNextRoad(), "Next road contribution"));
        stationContextPanel = ContextPanel("Station Building",
            ToolButton("<", (_, _) => mapViewport.SelectPreviousStation(), "Previous station contribution"),
            ToolButton(">", (_, _) => mapViewport.SelectNextStation(), "Next station contribution"));
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
            Width = 148,
            Child = previewStack
        };
        return placementPreviewPanel;
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
                new TabItem { Header = "Roads", Content = BuildRoadContributionBrowser() },
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

        panel.Children.Add(new TextBlock
        {
            Text = contributionSummary.Length == 0 ? "No parsed contributions." : contributionSummary,
            Foreground = TextBrush,
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        return new Border
        {
            Background = ChromeRaisedBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = new ScrollViewer { Content = panel }
        };
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

        UpdateModeButtons(status.EditMode);
        UpdateContextPanels(status.EditMode);
        UpdateSimulationText();
    }

    private void UpdatePlacementPreview(MapViewportStatus status)
    {
        (string PlatformTitle, string PlatformDetail) = PlatformPreviewLabels(status.ActivePlatformDescription);
        (string Key, string Title, string Detail, Func<Control> Create) preview = status.EditMode switch
        {
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

        bool hasPreview = status.EditMode is MapEditMode.Road
            or MapEditMode.Station
            or MapEditMode.Platform
            or MapEditMode.Train
            or MapEditMode.Terrain;
        placementPreviewPanel.IsVisible = hasPreview;
        placementPreviewPanel.Margin = new Thickness(8, ToolOverlayTop(status.EditMode), 0, 0);
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
        placementPreviewContent.IsVisible = status.EditMode != MapEditMode.Terrain;
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

    private static string RoadDetail(RoadContribution road)
    {
        return road.Style.Lanes > 0
            ? $"{road.Style.MajorType}, {road.Style.Lanes} lane(s)"
            : road.Kind.ToString();
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
            MapEditMode.Platform => 4,
            MapEditMode.Train => 5,
            MapEditMode.Terrain => 6,
            MapEditMode.Erase => 7,
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
        return $"Hover {FormatCompactLocation(status.HoverLocation)} | Selected {FormatCompactLocation(status.SelectedLocation)}{anchor} | {status.EditMode} | Road {status.ActiveRoadName} | Station {status.ActiveStationName} | Platform {status.ActivePlatformDescription} | Train {status.ActiveTrainName} | Zoom {status.Zoom:0.##}x | Cut {status.MaxVisibleLevel}/{status.WorldMaxHeightCutLevel} | Rail {status.RailTileCount:N0} Road {status.RoadTileCount:N0} Stations {status.StationCount:N0} Platforms {status.PlatformCount:N0} Trains {status.TrainCount:N0} | Pop {status.StationPopulation:N0} Waiting {status.WaitingPassengers:N0} Loaded {status.LoadedPassengersToday:N0} Unloaded {status.UnloadedPassengersToday:N0} Stops {status.TrainStopsToday:N0}";
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

    private enum ToolGlyphKind
    {
        Pointer,
        Road,
        Rail,
        Station,
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
