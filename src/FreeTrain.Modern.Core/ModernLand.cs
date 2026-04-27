namespace FreeTrain.Modern;

public enum LandContributionKind
{
    Static,
    Random,
    Forest,
    Unsupported
}

public sealed record LandContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    LandContributionKind Kind,
    SpriteFrame? StaticSprite,
    IReadOnlyList<string> RandomLandIds,
    ForestSpriteSet? Forest,
    string? Error)
{
    public bool IsLoadable => Kind switch
    {
        LandContributionKind.Static => StaticSprite?.IsLoadable == true,
        LandContributionKind.Random => RandomLandIds.Count > 0,
        LandContributionKind.Forest => Forest?.IsLoadable == true,
        _ => false
    };

    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : string.IsNullOrWhiteSpace(PluginTitle)
            ? PluginDirectoryName
            : PluginTitle;
}

public sealed record ForestSpriteSet(
    IReadOnlyList<SpriteFrame> TreeSprites,
    SpriteFrame? Ground,
    int Density)
{
    public bool IsLoadable => TreeSprites.Any(sprite => sprite.IsLoadable);
}

public sealed record MapLandObject(int H, int V, LandContribution Contribution, LandContribution? ResolvedStaticLand = null);
