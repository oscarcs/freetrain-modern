using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace FreeTrain.Modern;

public sealed class PluginLocalizationCatalog
{
    public const string DefaultLanguage = "en";

    private static readonly string[] ContributionTextElements =
    {
        "name",
        "group",
        "company",
        "type",
        "description"
    };

    private readonly IReadOnlyDictionary<string, PluginTranslationSet> plugins;

    private PluginLocalizationCatalog(
        string language,
        IReadOnlyDictionary<string, PluginTranslationSet> plugins)
    {
        Language = language;
        this.plugins = plugins;
        TranslationFileCount = plugins.Count;
        AvailableTranslationCount = plugins.Values.Sum(plugin => plugin.AvailableTranslationCount);
    }

    public string Language { get; }
    public int TranslationFileCount { get; }
    public int AvailableTranslationCount { get; }

    public static PluginLocalizationCatalog Load(string pluginDirectory, string? language)
    {
        string normalizedLanguage = NormalizeLanguage(language);
        if (string.Equals(normalizedLanguage, "ja", StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(pluginDirectory))
        {
            return Empty(normalizedLanguage);
        }

        Dictionary<string, PluginTranslationSet> loadedPlugins = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(pluginDirectory, $"plugin.{normalizedLanguage}.json", SearchOption.AllDirectories))
        {
            PluginTranslationSet? translationSet = PluginTranslationSet.Load(path);
            if (translationSet is null)
            {
                continue;
            }

            string pluginName = string.IsNullOrWhiteSpace(translationSet.Plugin)
                ? Path.GetFileName(Path.GetDirectoryName(path)) ?? ""
                : translationSet.Plugin;
            if (!string.IsNullOrWhiteSpace(pluginName))
            {
                loadedPlugins[pluginName] = translationSet;
            }
        }

        return new PluginLocalizationCatalog(normalizedLanguage, loadedPlugins);
    }

    public static PluginLocalizationCatalog Empty(string? language = null)
    {
        return new PluginLocalizationCatalog(NormalizeLanguage(language), new Dictionary<string, PluginTranslationSet>());
    }

    public void ApplyToManifest(string pluginDirectoryName, XElement root)
    {
        if (!plugins.TryGetValue(pluginDirectoryName, out PluginTranslationSet? translations))
        {
            return;
        }

        ApplyElement(translations, "plugin.title", root.Element("title"));

        int anonymousContributionIndex = 0;
        foreach (XElement contribution in root.Elements("contribution"))
        {
            string id = contribution.Attribute("id")?.Value.Trim() ?? "";
            string keyPrefix = string.IsNullOrWhiteSpace(id)
                ? $"contribution[{anonymousContributionIndex}]"
                : $"contribution.{id}";

            foreach (string elementName in ContributionTextElements)
            {
                ApplyElement(translations, $"{keyPrefix}.{elementName}", contribution.Element(elementName));
            }

            anonymousContributionIndex++;
        }
    }

    private static void ApplyElement(PluginTranslationSet translations, string key, XElement? element)
    {
        if (element is null)
        {
            return;
        }

        string source = element.Value.Trim();
        string? translated = translations.Translate(key, source);
        if (!string.IsNullOrWhiteSpace(translated))
        {
            element.Value = translated;
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultLanguage;
        }

        int separator = language.IndexOfAny(['-', '_']);
        return separator > 0
            ? language[..separator].ToLowerInvariant()
            : language.ToLowerInvariant();
    }
}

public sealed class PluginTranslationSet
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly IReadOnlyDictionary<string, PluginTextTranslation> translationsByKey;

    public PluginTranslationSet(
        string language,
        string plugin,
        string sourceManifest,
        IReadOnlyList<PluginTextTranslation> texts)
    {
        Language = language;
        Plugin = plugin;
        SourceManifest = string.IsNullOrWhiteSpace(sourceManifest) ? "plugin.xml" : sourceManifest;
        Texts = texts;
        translationsByKey = texts
            .Where(text => !string.IsNullOrWhiteSpace(text.Key))
            .GroupBy(text => text.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        AvailableTranslationCount = texts.Count(text => !string.IsNullOrWhiteSpace(text.Translation));
    }

    public string Language { get; }
    public string Plugin { get; }
    public string SourceManifest { get; }
    public IReadOnlyList<PluginTextTranslation> Texts { get; }
    public int AvailableTranslationCount { get; }

    public static PluginTranslationSet? Load(string path)
    {
        PluginTranslationFile? file;
        try
        {
            file = JsonSerializer.Deserialize<PluginTranslationFile>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (file is null)
        {
            return null;
        }

        string plugin = string.IsNullOrWhiteSpace(file.Plugin)
            ? Path.GetFileName(Path.GetDirectoryName(path)) ?? ""
            : file.Plugin;

        return new PluginTranslationSet(
            file.Language,
            plugin,
            file.SourceManifest,
            file.Texts);
    }

    public string? Translate(string key, string source)
    {
        if (!translationsByKey.TryGetValue(key, out PluginTextTranslation? translation)
            || string.IsNullOrWhiteSpace(translation.Translation))
        {
            return null;
        }

        return string.Equals(translation.Source, source, StringComparison.Ordinal)
            ? translation.Translation.Trim()
            : null;
    }
}

public sealed class PluginTranslationFile
{
    [JsonPropertyName("language")]
    public string Language { get; init; } = PluginLocalizationCatalog.DefaultLanguage;

    [JsonPropertyName("plugin")]
    public string Plugin { get; init; } = "";

    [JsonPropertyName("sourceManifest")]
    public string SourceManifest { get; init; } = "plugin.xml";

    [JsonPropertyName("texts")]
    public IReadOnlyList<PluginTextTranslation> Texts { get; init; } = [];
}

public sealed class PluginTextTranslation
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = "";

    [JsonPropertyName("source")]
    public string Source { get; init; } = "";

    [JsonPropertyName("translation")]
    public string Translation { get; init; } = "";
}
