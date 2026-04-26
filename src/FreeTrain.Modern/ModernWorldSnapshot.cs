namespace FreeTrain.Modern;

public sealed record ModernWorldSnapshot(
    string Name,
    int Width,
    int Height,
    int WaterLevel,
    ModernWorldClock Clock,
    ModernAccountState Account,
    int FineHeightWidth,
    int FineHeightHeight,
    int[] FineHeights,
    IReadOnlyList<ModernRailSnapshot> Rails,
    IReadOnlyList<ModernRoadSnapshot> Roads,
    IReadOnlyList<ModernCarSnapshot> Cars,
    IReadOnlyList<ModernEntitySnapshot> Entities)
{
    public const int CurrentVersion = 1;
    public int Version { get; init; } = CurrentVersion;
}

public sealed record ModernRailSnapshot(int H, int V);

public sealed record ModernRoadSnapshot(int H, int V, string RoadContributionId);

public sealed record ModernCarSnapshot(
    string CarId,
    ModernCarKind Kind,
    ModernCarPlacementKind Placement,
    int? H,
    int? V,
    int? Z,
    int? DirectionIndex);

public sealed record ModernEntitySnapshot(
    string EntityId,
    ModernEntityKind Kind,
    int H,
    int V,
    int Z,
    int FootprintH,
    int FootprintV,
    int FootprintZ,
    string ContributionId,
    string? ResolvedContributionId,
    bool IsOwned,
    bool IsSilentlyReclaimable,
    long EntityValue);
