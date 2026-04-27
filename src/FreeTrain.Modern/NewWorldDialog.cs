using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace FreeTrain.Modern;

public sealed class NewWorldDialog : Window
{
    private readonly TextBox nameBox;
    private readonly NumericUpDown widthBox;
    private readonly NumericUpDown heightBox;
    private readonly NumericUpDown waterLevelBox;
    private readonly NumericUpDown cashBox;
    private readonly ComboBox terrainBox;
    private readonly TextBlock validationText;

    public NewWorldDialog(ModernWorldCreationOptions initial)
    {
        ModernWorldCreationOptions normalized = initial.Normalize();

        Title = "New World";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        nameBox = new TextBox { Text = normalized.Name };
        widthBox = NumberBox(normalized.Width, ModernWorldCreationOptions.MinSize, ModernWorldCreationOptions.MaxSize);
        heightBox = NumberBox(normalized.Height, ModernWorldCreationOptions.MinSize, ModernWorldCreationOptions.MaxSize);
        waterLevelBox = NumberBox(normalized.WaterLevel, 0, 7);
        cashBox = NumberBox(normalized.InitialCash, 0, 99_999_999_999);
        terrainBox = new ComboBox
        {
            ItemsSource = Enum.GetValues<ModernWorldTerrainKind>(),
            SelectedItem = normalized.TerrainKind,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        validationText = new TextBlock
        {
            Foreground = Brushes.IndianRed,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        Content = BuildContent();
    }

    private Control BuildContent()
    {
        Grid fields = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto")
        };

        AddField(fields, 0, "Name", nameBox);
        AddField(fields, 1, "Width", widthBox);
        AddField(fields, 2, "Height", heightBox);
        AddField(fields, 3, "Water", waterLevelBox);
        AddField(fields, 4, "Cash", cashBox);
        AddField(fields, 5, "Terrain", terrainBox);

        Button create = new()
        {
            Content = "Create",
            MinWidth = 88
        };
        create.Click += CreateClicked;

        Button cancel = new()
        {
            Content = "Cancel",
            MinWidth = 88
        };
        cancel.Click += (_, _) => Close(null);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, create }
        };

        StackPanel root = new()
        {
            Spacing = 14,
            Margin = new Thickness(18),
            Children =
            {
                fields,
                validationText,
                buttons
            }
        };

        return root;
    }

    private void CreateClicked(object? sender, RoutedEventArgs e)
    {
        ModernWorldCreationOptions options = new(
            nameBox.Text ?? "",
            IntValue(widthBox, ModernWorldCreationOptions.Default.Width),
            IntValue(heightBox, ModernWorldCreationOptions.Default.Height),
            IntValue(waterLevelBox, ModernWorldCreationOptions.Default.WaterLevel),
            LongValue(cashBox, ModernWorldCreationOptions.Default.InitialCash),
            terrainBox.SelectedItem is ModernWorldTerrainKind terrain ? terrain : ModernWorldCreationOptions.Default.TerrainKind);

        options = options.Normalize();
        if (options.Width <= 0 || options.Height <= 0)
        {
            validationText.Text = "World dimensions must be positive.";
            validationText.IsVisible = true;
            return;
        }

        Close(options);
    }

    private static NumericUpDown NumberBox(long value, long minimum, long maximum)
    {
        return new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            Increment = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static int IntValue(NumericUpDown input, int fallback)
    {
        return input.Value is { } value ? (int)value : fallback;
    }

    private static long LongValue(NumericUpDown input, long fallback)
    {
        return input.Value is { } value ? (long)value : fallback;
    }

    private static void AddField(Grid grid, int row, string label, Control input)
    {
        TextBlock text = new()
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 8)
        };
        Grid.SetRow(text, row);
        grid.Children.Add(text);

        input.Margin = new Thickness(0, 0, 0, 8);
        Grid.SetColumn(input, 1);
        Grid.SetRow(input, row);
        grid.Children.Add(input);
    }
}
