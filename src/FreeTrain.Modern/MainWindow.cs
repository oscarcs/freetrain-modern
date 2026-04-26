using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FreeTrain.Modern;

public sealed class MainWindow : Window
{
    private readonly LegacyAssetCatalog assets;
    private readonly PluginManifestCatalog plugins;
    private readonly Image previewImage;
    private readonly TextBlock selectionText;
    private readonly MapViewport mapViewport;
    private readonly TextBlock mapStatusText;

    public MainWindow(LegacyAssetCatalog assets)
    {
        this.assets = assets;
        plugins = new PluginManifestCatalog(assets.PluginDirectory);

        Title = "FreeTrain Modern";
        MinWidth = 960;
        MinHeight = 640;
        Width = 1180;
        Height = 760;

        previewImage = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        selectionText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        };
        mapStatusText = new TextBlock
        {
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "No tile | nothing selected"
        };
        mapViewport = new MapViewport(assets, plugins);
        mapViewport.StatusChanged += status => mapStatusText.Text = status;

        Content = BuildLayout();
        LoadInitialPreview();
    }

    private Control BuildLayout()
    {
        DockPanel root = new();

        Menu menu = new();
        MenuItem file = new() { Header = "_File" };
        file.Items.Add(new MenuItem
        {
            Header = "_Open Legacy Resource Folder",
            Command = MiniCommand.Create(OpenResourceFolder)
        });
        file.Items.Add(new Separator());
        file.Items.Add(new MenuItem
        {
            Header = "E_xit",
            Command = MiniCommand.Create(Close)
        });
        menu.Items.Add(file);
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);

        Grid body = new()
        {
            ColumnDefinitions = new ColumnDefinitions("300,*"),
            RowDefinitions = new RowDefinitions("*")
        };

        body.Children.Add(BuildSidebar());

        TabControl tabs = new()
        {
            Items =
            {
                new TabItem
                {
                    Header = "Map",
                    Content = BuildMapViewport()
                },
                new TabItem
                {
                    Header = "Assets",
                    Content = BuildAssetPreview()
                },
                new TabItem
                {
                    Header = "Pictures",
                    Content = BuildPictureContributionBrowser()
                },
                new TabItem
                {
                    Header = "Sprites",
                    Content = BuildSpriteContributionBrowser()
                },
                new TabItem
                {
                    Header = "Plugins",
                    Content = BuildPluginBrowser()
                }
            }
        };
        Grid.SetColumn(tabs, 1);
        body.Children.Add(tabs);

        root.Children.Add(body);
        return root;
    }

    private Control BuildMapViewport()
    {
        DockPanel panel = new();
        DockPanel toolbar = BuildMapToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        panel.Children.Add(toolbar);

        DockPanel.SetDock(mapStatusText, Dock.Bottom);
        mapStatusText.Margin = new Avalonia.Thickness(12, 6);
        panel.Children.Add(mapStatusText);

        panel.Children.Add(new ScrollViewer
        {
            Content = mapViewport,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        return new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Avalonia.Thickness(1),
            Background = Brushes.White,
            Child = panel
        };
    }

    private DockPanel BuildMapToolbar()
    {
        DockPanel toolbar = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 247)),
            LastChildFill = true,
            Margin = new Avalonia.Thickness(0, 0, 0, 1)
        };

        StackPanel controls = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Avalonia.Thickness(8, 6)
        };

        controls.Children.Add(ToolButton("-", (_, _) => mapViewport.Zoom -= 0.25, "Zoom out"));
        controls.Children.Add(ToolButton("+", (_, _) => mapViewport.Zoom += 0.25, "Zoom in"));
        controls.Children.Add(ToolButton("1:1", (_, _) => mapViewport.Zoom = 1.0, "Reset zoom"));
        controls.Children.Add(ToolButton("Raise", (_, _) => mapViewport.RaiseSelectedTerrain(), "Raise selected terrain"));
        controls.Children.Add(ToolButton("Lower", (_, _) => mapViewport.LowerSelectedTerrain(), "Lower selected terrain"));

        CheckBox grid = new()
        {
            Content = "Grid",
            IsChecked = mapViewport.ShowGrid,
            Foreground = Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.IsCheckedChanged += (_, _) => mapViewport.ShowGrid = grid.IsChecked == true;
        controls.Children.Add(grid);

        CheckBox night = new()
        {
            Content = "Night",
            IsChecked = mapViewport.UseNightView,
            Foreground = Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center
        };
        night.IsCheckedChanged += (_, _) => mapViewport.UseNightView = night.IsChecked == true;
        controls.Children.Add(night);

        controls.Children.Add(new TextBlock
        {
            Text = "Height",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Black,
            Margin = new Avalonia.Thickness(8, 0, 0, 0)
        });

        Slider heightCut = new()
        {
            Minimum = 0,
            Maximum = mapViewport.WorldMaxHeightCutLevel,
            Value = mapViewport.MaxVisibleLevel,
            Width = 140,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        heightCut.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                mapViewport.MaxVisibleLevel = (int)Math.Round(heightCut.Value);
            }
        };
        controls.Children.Add(heightCut);

        toolbar.Children.Add(controls);
        return toolbar;
    }

    private static Button ToolButton(string content, EventHandler<RoutedEventArgs> click, string tip)
    {
        Button button = new()
        {
            Content = content,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            MinWidth = 34,
            MinHeight = 28,
            Padding = new Avalonia.Thickness(8, 2)
        };
        button.Click += click;
        ToolTip.SetTip(button, tip);
        return button;
    }

    private Control BuildAssetPreview()
    {
        return new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Avalonia.Thickness(1),
            Background = Brushes.White,
            Padding = new Avalonia.Thickness(16),
            Child = new ScrollViewer
            {
                Content = previewImage,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
    }

    private Control BuildSidebar()
    {
        DockPanel sidebar = new()
        {
            Margin = new Avalonia.Thickness(12),
            LastChildFill = true
        };

        TextBlock title = new()
        {
            Text = "FreeTrain Modern",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(title, Dock.Top);
        sidebar.Children.Add(title);

        TextBlock summary = new()
        {
            Text = $"{assets.Assets.Count} core assets | {plugins.LoadedCount} plugins | {plugins.ErrorCount} plugin errors",
            Margin = new Avalonia.Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(summary, Dock.Top);
        sidebar.Children.Add(summary);

        selectionText.Text = assets.CoreResourceDirectory;
        selectionText.Margin = new Avalonia.Thickness(0, 0, 0, 12);
        DockPanel.SetDock(selectionText, Dock.Top);
        sidebar.Children.Add(selectionText);

        ListBox list = new()
        {
            ItemsSource = assets.Assets.Where(asset => asset.Kind == "Image").ToList()
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
                Margin = new Avalonia.Thickness(0, 4),
                Children =
                {
                    new TextBlock { Text = asset?.Name, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = asset?.Kind, Foreground = Brushes.Gray, FontSize = 12 }
                }
            });
        sidebar.Children.Add(list);

        return sidebar;
    }

    private Control BuildPluginBrowser()
    {
        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("320,*"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Avalonia.Thickness(12)
        };

        TextBlock summary = new()
        {
            Text = $"{plugins.LoadedCount} manifests parsed | {plugins.ErrorCount} errors | {plugins.ContributionTypeCounts.Values.Sum()} contributions",
            FontWeight = FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 10)
        };
        Grid.SetColumnSpan(summary, 2);
        grid.Children.Add(summary);

        ListBox list = new()
        {
            ItemsSource = plugins.Plugins
        };
        list.ItemTemplate = new FuncDataTemplate<PluginManifest>((plugin, _) =>
            new StackPanel
            {
                Spacing = 2,
                Margin = new Avalonia.Thickness(0, 4),
                Children =
                {
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(plugin?.Title) ? plugin?.DirectoryName : plugin?.Title,
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
                        Foreground = plugin?.IsLoaded == true ? Brushes.Gray : Brushes.Firebrick,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            });
        Grid.SetRow(list, 1);
        grid.Children.Add(list);

        ContentControl details = new();
        list.SelectionChanged += (_, _) =>
        {
            details.Content = list.SelectedItem is PluginManifest plugin
                ? BuildPluginDetails(plugin)
                : null;
        };

        Grid.SetColumn(details, 1);
        Grid.SetRow(details, 1);
        grid.Children.Add(details);

        if (plugins.Plugins.Count > 0)
        {
            list.SelectedIndex = 0;
        }

        return grid;
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
            Margin = new Avalonia.Thickness(12)
        };

        TextBlock summary = new()
        {
            Text = $"{loadablePictures.Count} loadable picture contributions shown | {plugins.Pictures.Count} parsed",
            FontWeight = FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 10)
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
            Margin = new Avalonia.Thickness(12)
        };

        TextBlock summary = new()
        {
            Text = $"{loadableSprites.Count} loadable sprite contributions shown | {plugins.Sprites.Count} parsed",
            FontWeight = FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 10)
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

    private static Control BuildPluginDetails(PluginManifest plugin)
    {
        StackPanel panel = new()
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(16, 0, 0, 0)
        };

        panel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(plugin.Title) ? plugin.DirectoryName : plugin.Title,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock { Text = $"Folder: {plugin.DirectoryName}", TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(plugin.Author))
        {
            panel.Children.Add(new TextBlock { Text = $"Author: {plugin.Author}", TextWrapping = TextWrapping.Wrap });
        }

        if (!string.IsNullOrWhiteSpace(plugin.Version))
        {
            panel.Children.Add(new TextBlock { Text = $"Version: {plugin.Version}", TextWrapping = TextWrapping.Wrap });
        }

        if (!string.IsNullOrWhiteSpace(plugin.Homepage))
        {
            panel.Children.Add(new TextBlock { Text = plugin.Homepage, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
        }

        if (plugin.Error is not null)
        {
            panel.Children.Add(new TextBlock { Text = plugin.Error, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap });
        }

        string contributionSummary = string.Join("\n", plugin.Contributions
            .GroupBy(contribution => contribution.Type)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}: {group.Count()}"));

        panel.Children.Add(new TextBlock
        {
            Text = contributionSummary.Length == 0 ? "No parsed contributions." : contributionSummary,
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            TextWrapping = TextWrapping.Wrap
        });

        return new ScrollViewer { Content = panel };
    }

    private void LoadInitialPreview()
    {
        string? splash = assets.FindResource("splash.jpg");
        if (splash is not null)
        {
            ShowAsset(new LegacyAsset("splash.jpg", "Image", splash));
        }
    }

    private void ShowAsset(LegacyAsset asset)
    {
        try
        {
            previewImage.Source = new Bitmap(asset.Path);
            selectionText.Text = asset.Path;
        }
        catch (Exception ex)
        {
            previewImage.Source = null;
            selectionText.Text = $"{asset.Path}\n{ex.Message}";
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
}
