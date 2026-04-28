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

        public bool HasSameRenderedState(BuildCatalogItem other)
        {
            return Key.Equals(other.Key, StringComparison.Ordinal)
                && Title.Equals(other.Title, StringComparison.Ordinal)
                && Detail.Equals(other.Detail, StringComparison.Ordinal)
                && Source.Equals(other.Source, StringComparison.Ordinal)
                && IsActive == other.IsActive
                && HaveSameOptionGroups(OptionGroups, other.OptionGroups);
        }

        private static bool HaveSameOptionGroups(IReadOnlyList<BuildCatalogOptionGroup> left, IReadOnlyList<BuildCatalogOptionGroup> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!left[i].HasSameRenderedState(right[i]))
                {
                    return false;
                }
            }

            return true;
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

        public bool HasSameRenderedState(BuildCatalogOptionGroup other)
        {
            if (!Title.Equals(other.Title, StringComparison.Ordinal) || Options.Count != other.Options.Count)
            {
                return false;
            }

            for (int i = 0; i < Options.Count; i++)
            {
                if (!Options[i].HasSameRenderedState(other.Options[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class BuildCatalogOption
    {
        private readonly Func<IReadOnlyList<Color>>? createSwatches;
        private IReadOnlyList<Color>? swatches;

        public BuildCatalogOption(
            string title,
            string detail,
            bool isActive,
            Action select,
            IReadOnlyList<Color>? swatches = null,
            Func<IReadOnlyList<Color>>? createSwatches = null)
        {
            Label = title;
            Detail = detail;
            IsActive = isActive;
            Select = select;
            this.swatches = swatches;
            this.createSwatches = createSwatches;
        }

        public string Label { get; }
        public string Detail { get; }
        public bool IsActive { get; }
        public Action Select { get; }
        public IReadOnlyList<Color> Swatches => swatches ??= createSwatches?.Invoke() ?? Array.Empty<Color>();
        public string ToolTip => string.IsNullOrWhiteSpace(Detail) ? Label : $"{Label}\n{Detail}";

        public bool HasSameRenderedState(BuildCatalogOption other)
        {
            return Label.Equals(other.Label, StringComparison.Ordinal)
                && Detail.Equals(other.Detail, StringComparison.Ordinal)
                && IsActive == other.IsActive
                && HasSwatches == other.HasSwatches;
        }

        private bool HasSwatches => createSwatches is not null || swatches?.Count > 0;
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
}
