using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

string[] contributionTextElements =
[
    "name",
    "group",
    "company",
    "type",
    "description"
];

JsonSerializerOptions jsonOptions = new()
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true
};

JsonSerializerOptions readJsonOptions = new()
{
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
};

string pluginDirectory = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal)) ?? "plugins";
string language = ReadOption(args, "--language") ?? "en";
bool includeAllText = args.Contains("--all", StringComparer.OrdinalIgnoreCase);

pluginDirectory = Path.GetFullPath(pluginDirectory);
if (!Directory.Exists(pluginDirectory))
{
    Console.Error.WriteLine($"Plugin directory not found: {pluginDirectory}");
    return 1;
}

ExtractionSummary summary = ExtractPluginText(pluginDirectory, language, includeAllText);
Console.WriteLine($"Scanned {summary.ManifestCount} plugin manifests.");
Console.WriteLine($"Wrote {summary.SidecarCount} plugin.{language}.json files with {summary.TextCount} text entries.");
if (summary.ErrorCount > 0)
{
    Console.WriteLine($"Skipped {summary.ErrorCount} manifests that could not be parsed.");
}

return summary.ErrorCount == 0 ? 0 : 2;

ExtractionSummary ExtractPluginText(string pluginDirectory, string language, bool includeAllText)
{
    int manifests = 0;
    int sidecars = 0;
    int textEntries = 0;
    int errors = 0;

    foreach (string manifestPath in Directory.EnumerateFiles(pluginDirectory, "plugin.xml", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
    {
        manifests++;

        XDocument document;
        try
        {
            document = LoadXml(manifestPath);
        }
        catch (Exception ex) when (ex is IOException or XmlException or DecoderFallbackException)
        {
            errors++;
            Console.Error.WriteLine($"Could not parse {manifestPath}: {ex.Message}");
            continue;
        }

        XElement? root = document.Root;
        if (root is null)
        {
            errors++;
            Console.Error.WriteLine($"Could not parse {manifestPath}: missing document root.");
            continue;
        }

        string pluginName = Path.GetFileName(Path.GetDirectoryName(manifestPath)) ?? "";
        List<PluginTextTranslation> texts = ExtractTexts(root, includeAllText);
        if (texts.Count == 0)
        {
            continue;
        }

        string sidecarPath = Path.Combine(Path.GetDirectoryName(manifestPath) ?? pluginDirectory, $"plugin.{language}.json");
        IReadOnlyDictionary<string, PluginTextTranslation> existingTexts = LoadExistingTranslations(sidecarPath);
        List<PluginTextTranslation> mergedTexts = texts
            .Select(text => MergeExistingTranslation(text, existingTexts))
            .ToList();

        PluginTranslationFile sidecar = new()
        {
            Language = language,
            Plugin = pluginName,
            SourceManifest = "plugin.xml",
            Texts = mergedTexts
        };

        File.WriteAllText(sidecarPath, JsonSerializer.Serialize(sidecar, jsonOptions) + Environment.NewLine, new UTF8Encoding(false));
        sidecars++;
        textEntries += mergedTexts.Count;
    }

    return new ExtractionSummary(manifests, sidecars, textEntries, errors);
}

List<PluginTextTranslation> ExtractTexts(XElement root, bool includeAllText)
{
    List<PluginTextTranslation> texts = [];
    AddElementText(texts, "plugin.title", root.Element("title"), includeAllText);

    int anonymousContributionIndex = 0;
    foreach (XElement contribution in root.Elements("contribution"))
    {
        string id = contribution.Attribute("id")?.Value.Trim() ?? "";
        string keyPrefix = string.IsNullOrWhiteSpace(id)
            ? $"contribution[{anonymousContributionIndex}]"
            : $"contribution.{id}";

        foreach (string elementName in contributionTextElements)
        {
            AddElementText(texts, $"{keyPrefix}.{elementName}", contribution.Element(elementName), includeAllText);
        }

        anonymousContributionIndex++;
    }

    return texts;
}

void AddElementText(List<PluginTextTranslation> texts, string key, XElement? element, bool includeAllText)
{
    if (element is null)
    {
        return;
    }

    string source = element.Value.Trim();
    if (string.IsNullOrWhiteSpace(source))
    {
        return;
    }

    if (!includeAllText && !ContainsJapanese(source))
    {
        return;
    }

    texts.Add(new PluginTextTranslation
    {
        Key = key,
        Source = source,
        Translation = ""
    });
}

XDocument LoadXml(string path)
{
    XmlReaderSettings settings = new()
    {
        CheckCharacters = false,
        DtdProcessing = DtdProcessing.Parse,
        XmlResolver = null,
        IgnoreComments = true
    };

    using FileStream stream = File.OpenRead(path);
    using XmlReader reader = XmlReader.Create(stream, settings, path);
    return XDocument.Load(reader, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
}

IReadOnlyDictionary<string, PluginTextTranslation> LoadExistingTranslations(string path)
{
    if (!File.Exists(path))
    {
        return new Dictionary<string, PluginTextTranslation>();
    }

    try
    {
        PluginTranslationFile? existing = JsonSerializer.Deserialize<PluginTranslationFile>(File.ReadAllText(path), readJsonOptions);
        return existing?.Texts
            .Where(text => !string.IsNullOrWhiteSpace(text.Key))
            .GroupBy(text => text.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, PluginTextTranslation>();
    }
    catch (JsonException)
    {
        return new Dictionary<string, PluginTextTranslation>();
    }
}

PluginTextTranslation MergeExistingTranslation(
    PluginTextTranslation extracted,
    IReadOnlyDictionary<string, PluginTextTranslation> existingTexts)
{
    if (!existingTexts.TryGetValue(extracted.Key, out PluginTextTranslation? existing)
        || !string.Equals(existing.Source, extracted.Source, StringComparison.Ordinal))
    {
        return extracted;
    }

    return extracted with { Translation = existing.Translation };
}

bool ContainsJapanese(string text)
{
    foreach (Rune rune in text.EnumerateRunes())
    {
        int value = rune.Value;
        if ((value >= 0x3040 && value <= 0x30ff)
            || (value >= 0x3400 && value <= 0x9fff)
            || (value >= 0xf900 && value <= 0xfaff))
        {
            return true;
        }
    }

    return false;
}

string? ReadOption(string[] args, string name)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }

        if (args[i].StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
        {
            return args[i][(name.Length + 1)..];
        }
    }

    return null;
}

public sealed record ExtractionSummary(int ManifestCount, int SidecarCount, int TextCount, int ErrorCount);

public sealed record PluginTranslationFile
{
    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";

    [JsonPropertyName("plugin")]
    public string Plugin { get; init; } = "";

    [JsonPropertyName("sourceManifest")]
    public string SourceManifest { get; init; } = "plugin.xml";

    [JsonPropertyName("texts")]
    public IReadOnlyList<PluginTextTranslation> Texts { get; init; } = [];
}

public sealed record PluginTextTranslation
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = "";

    [JsonPropertyName("source")]
    public string Source { get; init; } = "";

    [JsonPropertyName("translation")]
    public string Translation { get; init; } = "";
}
