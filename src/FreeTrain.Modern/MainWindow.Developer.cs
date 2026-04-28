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

}
