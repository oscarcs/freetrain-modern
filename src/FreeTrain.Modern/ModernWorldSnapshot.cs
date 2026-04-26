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
    IReadOnlyList<ModernStationSnapshot> Stations,
    IReadOnlyList<ModernPlatformSnapshot> Platforms,
    IReadOnlyList<ModernTrainSnapshot> Trains)
{
    public const int CurrentVersion = 3;
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
    string Name,
    ModernStationStats Stats);

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
    int PassengerCapacity = 0,
    int PassengerSeatedCapacity = 0,
    int PassengerCount = 0,
    ModernVoxelKey? PassengerSourceLocation = null,
    string? CurrentStopPlatformId = null,
    string? LastStoppedPlatformId = null);
