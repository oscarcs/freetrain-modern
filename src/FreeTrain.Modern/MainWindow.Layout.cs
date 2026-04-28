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
            ItemTemplate = new FuncDataTemplate<BuildCatalogItem>((item, _) => BuildCatalogItemRow(item)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            ItemsSource = buildCatalogItems
        };
        ScrollViewer.SetBringIntoViewOnFocusChange(buildCatalogList, false);

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
            BringIntoViewOnFocusChange = false,
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
                    Focusable = false,
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

}
