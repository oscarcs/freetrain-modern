using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace FreeTrain.Modern;

public sealed partial class MainWindow : Window
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
    private readonly Dictionary<string, int> structureColorVariantCountCache = new(StringComparer.Ordinal);
    private readonly ObservableCollection<BuildCatalogItem> buildCatalogItems = new();
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
    private IReadOnlyList<IReadOnlyList<SpriteContribution>>? structureCatalogGroups;

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
}
