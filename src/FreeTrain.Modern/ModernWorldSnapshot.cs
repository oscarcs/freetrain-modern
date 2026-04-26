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
    IReadOnlyList<ModernEntitySnapshot> Entities,
    IReadOnlyList<ModernStationSnapshot>? Stations = null,
    IReadOnlyList<ModernPlatformSnapshot>? Platforms = null,
    IReadOnlyList<ModernTrainSnapshot>? Trains = null)
{
    public const int CurrentVersion = 2;
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

public sealed record ModernStationSnapshot(
    string StationId,
    int H,
    int V,
    int Z,
    string ContributionId,
    string Name);

public sealed record ModernPlatformSnapshot(
    string PlatformId,
    int H,
    int V,
    int Z,
    int DirectionIndex,
    int Length,
    PlatformStyle Style,
    string? StationId);

public sealed record ModernTrainCarPlacementSnapshot(
    string CarContributionId,
    int H,
    int V,
    int Z,
    int DirectionIndex);

public sealed record ModernTrainSnapshot(
    string TrainId,
    string ContributionId,
    IReadOnlyList<ModernTrainCarPlacementSnapshot> Cars,
    long MinuteAccumulator,
    ModernTrainState State = ModernTrainState.Unplaced,
    long StopRemainingMinutes = 0,
    int MoveCount = 0,
    int PassengerCount = 0,
    string? CurrentStopPlatformId = null,
    string? LastStoppedPlatformId = null);
