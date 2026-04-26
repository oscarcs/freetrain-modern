namespace FreeTrain.Modern;

public enum PlatformStyle
{
    ThinNoRoof,
    ThinRoof,
    Fat
}

public enum ModernTrainState
{
    Unplaced,
    Moving,
    StoppingAtStation,
    EmergencyStopping
}

public sealed record StationContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Group,
    string Name,
    int SizeH,
    int SizeV,
    int OperationCost,
    SpriteFrame? Frame,
    ModernSpriteSet2D? SpriteSet2D,
    string? Error)
{
    public bool IsLoadable => Error is null && (Frame?.IsLoadable == true || SpriteSet2D?.IsLoadable == true);
    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : !string.IsNullOrWhiteSpace(Group)
            ? Group
            : string.IsNullOrWhiteSpace(PluginTitle)
                ? PluginDirectoryName
                : PluginTitle;
}

public sealed record TrainCarContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Name,
    int Capacity,
    int SeatedCapacity,
    bool IsAsymmetric,
    IReadOnlyDictionary<int, SpriteFrame> DirectionFrames,
    string? Error)
{
    public bool IsLoadable => Error is null && DirectionFrames.Values.Any(frame => frame.IsLoadable);
    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : string.IsNullOrWhiteSpace(PluginTitle)
            ? PluginDirectoryName
            : PluginTitle;

    public SpriteFrame? FrameForAngle(int angle)
    {
        int key = IsAsymmetric ? angle & 15 : angle & 7;
        return DirectionFrames.TryGetValue(key, out SpriteFrame? frame)
            ? frame
            : DirectionFrames.Values.FirstOrDefault(candidate => candidate.IsLoadable);
    }
}

public sealed record TrainContribution(
    string PluginDirectoryName,
    string PluginTitle,
    string Id,
    string Company,
    string TypeName,
    string Name,
    string Author,
    string Description,
    int Fare,
    int Price,
    int Amenity,
    string TripRange,
    int MinutesPerVoxel,
    string? HeadCarId,
    string? BodyCarId,
    string? TailCarId,
    string? Error)
{
    public bool IsLoadable => Error is null && !string.IsNullOrWhiteSpace(BodyCarId);
    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : !string.IsNullOrWhiteSpace(TypeName)
            ? TypeName
            : string.IsNullOrWhiteSpace(PluginTitle)
                ? PluginDirectoryName
                : PluginTitle;

    public IReadOnlyList<string> CreateCarIds(int requestedLength)
    {
        int length = Math.Max(1, requestedLength);
        string body = BodyCarId ?? HeadCarId ?? TailCarId ?? "";
        string[] cars = Enumerable.Repeat(body, length).ToArray();
        if (!string.IsNullOrWhiteSpace(HeadCarId))
        {
            cars[0] = HeadCarId;
        }

        if (!string.IsNullOrWhiteSpace(TailCarId))
        {
            cars[^1] = TailCarId;
        }

        return cars;
    }
}

public sealed record ModernStation(
    string StationId,
    int H,
    int V,
    int Z,
    StationContribution Contribution)
{
    public string Name { get; init; } = $"Station {Math.Abs(StationId.GetHashCode()) % 1000:000}";
    public int OperationCost => Contribution.OperationCost;
    public int FootprintH => Math.Max(1, Contribution.SizeH);
    public int FootprintV => Math.Max(1, Contribution.SizeV);

    public IEnumerable<ModernVoxelKey> OccupiedVoxels
    {
        get
        {
            for (int v = 0; v < FootprintV; v++)
            {
                for (int h = 0; h < FootprintH; h++)
                {
                    yield return new ModernVoxelKey(H + h, V + v, Z);
                }
            }
        }
    }
}

public sealed record ModernPlatform(
    string PlatformId,
    int H,
    int V,
    int Z,
    int DirectionIndex,
    int Length,
    PlatformStyle Style,
    string? StationId)
{
    public ModernDirection Direction => ModernDirection.FromIndex(DirectionIndex);

    public IEnumerable<ModernVoxelKey> OccupiedVoxels
    {
        get
        {
            ModernDirection direction = Direction;
            for (int i = 0; i < Math.Max(1, Length); i++)
            {
                yield return new ModernVoxelKey(H + direction.OffsetX * i, V + direction.OffsetY * i, Z);
            }
        }
    }
}

public sealed record ModernTrainCarPlacement(
    string CarContributionId,
    ModernVoxelKey Location,
    int DirectionIndex)
{
    public ModernDirection Direction => ModernDirection.FromIndex(DirectionIndex);
}

public readonly record struct ModernTrainCarRenderPose(int Angle, int OffsetX, int OffsetY);

public sealed record ModernTrain(
    string TrainId,
    TrainContribution Contribution,
    IReadOnlyList<ModernTrainCarPlacement> Cars,
    long MinuteAccumulator,
    ModernTrainState State = ModernTrainState.Unplaced,
    long StopRemainingMinutes = 0,
    int MoveCount = 0,
    int PassengerCount = 0,
    string? CurrentStopPlatformId = null,
    string? LastStoppedPlatformId = null)
{
    public bool IsPlaced => Cars.Count > 0;
    public ModernTrainCarPlacement? Head => Cars.Count == 0 ? null : Cars[0];
    public int Length => Math.Max(1, Cars.Count);
}
