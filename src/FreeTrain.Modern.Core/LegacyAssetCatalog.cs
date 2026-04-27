using System.Collections.ObjectModel;
using System.IO;

namespace FreeTrain.Modern;

public sealed record LegacyAsset(string Name, string Kind, string Path);

public sealed class LegacyAssetCatalog
{
    public LegacyAssetCatalog()
    {
        RepositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        CoreResourceDirectory = Path.Combine(RepositoryRoot, "core", "res");
        PluginDirectory = Path.Combine(RepositoryRoot, "plugins");

        Assets = LoadAssets();
        PluginCount = Directory.Exists(PluginDirectory)
            ? Directory.EnumerateDirectories(PluginDirectory).Count()
            : 0;
    }

    public string RepositoryRoot { get; }
    public string CoreResourceDirectory { get; }
    public string PluginDirectory { get; }
    public int PluginCount { get; }
    public IReadOnlyList<LegacyAsset> Assets { get; }

    public string? FindResource(string name)
    {
        string path = Path.Combine(CoreResourceDirectory, name);
        if (File.Exists(path))
        {
            return path;
        }

        return Directory.Exists(CoreResourceDirectory)
            ? Directory.EnumerateFiles(CoreResourceDirectory)
                .FirstOrDefault(candidate => string.Equals(Path.GetFileName(candidate), name, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private ReadOnlyCollection<LegacyAsset> LoadAssets()
    {
        if (!Directory.Exists(CoreResourceDirectory))
        {
            return Array.Empty<LegacyAsset>().AsReadOnly();
        }

        return Directory.EnumerateFiles(CoreResourceDirectory)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new LegacyAsset(Path.GetFileName(path), Classify(path), path))
            .ToList()
            .AsReadOnly();
    }

    private static string Classify(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".bmp" or ".gif" or ".jpg" or ".jpeg" or ".png" or ".ico" => "Image",
            ".wav" => "Sound",
            ".html" or ".xml" or ".xsl" => "Document",
            _ => "Other"
        };
    }

    private static string FindRepositoryRoot(string start)
    {
        DirectoryInfo? current = new(start);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "FreeTrain.2008.sln"))
                && Directory.Exists(Path.Combine(current.FullName, "core"))
                && Directory.Exists(Path.Combine(current.FullName, "plugins")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine(start, "..", "..", ".."));
    }
}
