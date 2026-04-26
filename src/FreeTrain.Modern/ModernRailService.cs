using System.Text.Json.Serialization;

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

public sealed record ModernTransportLog(
    int Today = 0,
    int Yesterday = 0,
    double ThisWeek = 0,
    double LastWeekPerDay = 0)
{
    private const double LogFactor = 5;

    public ModernTransportLog AddAmount(int amount)
    {
        return amount <= 0 ? this : this with { Today = Today + amount };
    }

    public ModernTransportLog DailyReset(int dayOfWeek)
    {
        double nextThisWeek = ThisWeek + Math.Pow(Math.Max(0, Today), 1 / LogFactor);
        double nextLastWeekPerDay = LastWeekPerDay;
        if (dayOfWeek == 6)
        {
            nextLastWeekPerDay = Math.Pow(nextThisWeek / 7, LogFactor);
            nextThisWeek = 0;
        }

        return this with
        {
            Today = 0,
            Yesterday = Today,
            ThisWeek = nextThisWeek,
            LastWeekPerDay = nextLastWeekPerDay
        };
    }
}

public sealed record ModernStationStats(
    int GonePassengers,
    int AccumulatedLoadedPassengers,
    int AccumulatedUnloadedPassengers,
    ModernTransportLog Trains,
    ModernTransportLog Imported,
    ModernTransportLog Exported,
    double DevelopmentQuantity)
{
    private const float AveragePassengerRatio = 0.9996f;
    private const float AveragePassengerPerDayFactor = 24.0f * (1.0f - AveragePassengerRatio);

    public ModernStationStats()
        : this(0, 0, 0, new ModernTransportLog(), new ModernTransportLog(), new ModernTransportLog(), 0)
    {
    }

    [JsonIgnore]
    public int AverageLoadedPassengers => (int)(AccumulatedLoadedPassengers * AveragePassengerPerDayFactor);

    [JsonIgnore]
    public int AverageUnloadedPassengers => (int)(AccumulatedUnloadedPassengers * AveragePassengerPerDayFactor);

    [JsonIgnore]
    public int LoadedToday => Exported.Today;

    [JsonIgnore]
    public int LoadedYesterday => Exported.Yesterday;

    [JsonIgnore]
    public int UnloadedToday => Imported.Today;

    [JsonIgnore]
    public int UnloadedYesterday => Imported.Yesterday;

    [JsonIgnore]
    public int TrainsToday => Trains.Today;

    [JsonIgnore]
    public int TrainsYesterday => Trains.Yesterday;

    [JsonIgnore]
    public double ScoreImported => Imported.LastWeekPerDay;

    [JsonIgnore]
    public double ScoreExported => Exported.LastWeekPerDay;

    [JsonIgnore]
    public double ScoreTrains => Trains.LastWeekPerDay;

    [JsonIgnore]
    public double DevelopmentStrength => (ScoreImported + ScoreExported * 0.1) * 0.2;

    public int WaitingPassengers(int population)
    {
        return Math.Max(0, population - GonePassengers);
    }

    public ModernStationStats HourlyDecay()
    {
        return this with
        {
            GonePassengers = (int)(GonePassengers * 0.8f),
            AccumulatedLoadedPassengers = (int)(AccumulatedLoadedPassengers * AveragePassengerRatio),
            AccumulatedUnloadedPassengers = (int)(AccumulatedUnloadedPassengers * AveragePassengerRatio)
        };
    }

    public ModernStationStats DailyReset(int dayOfWeek)
    {
        return this with
        {
            Trains = Trains.DailyReset(dayOfWeek),
            Imported = Imported.DailyReset(dayOfWeek),
            Exported = Exported.DailyReset(dayOfWeek)
        };
    }

    public ModernStationStats RecordArrival(int passengerCount, double developmentQuantity)
    {
        return this with
        {
            Trains = Trains.AddAmount(1),
            Imported = Imported.AddAmount(passengerCount),
            AccumulatedUnloadedPassengers = AccumulatedUnloadedPassengers + passengerCount,
            DevelopmentQuantity = DevelopmentQuantity + developmentQuantity
        };
    }

    public ModernStationStats RecordDeparture(int passengerCount)
    {
        return this with
        {
            Trains = Trains.AddAmount(1),
            Exported = Exported.AddAmount(passengerCount),
            GonePassengers = GonePassengers + passengerCount,
            AccumulatedLoadedPassengers = AccumulatedLoadedPassengers + passengerCount,
            DevelopmentQuantity = DevelopmentQuantity + passengerCount
        };
    }
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
    StationContribution Contribution,
    ModernStationStats Stats)
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

    public ModernStation(
        string stationId,
        int h,
        int v,
        int z,
        StationContribution contribution)
        : this(stationId, h, v, z, contribution, new ModernStationStats())
    {
    }
}

public sealed record ModernStationDevelopmentSignal(
    string StationId,
    string StationName,
    ModernVoxelKey Location,
    int Population,
    int WaitingPassengers,
    double ImportedScore,
    double ExportedScore,
    double TrainScore,
    double Strength,
    double DevelopmentQuantity);

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
    int PassengerCapacity = 0,
    int PassengerSeatedCapacity = 0,
    int PassengerCount = 0,
    ModernVoxelKey? PassengerSourceLocation = null,
    string? CurrentStopPlatformId = null,
    string? LastStoppedPlatformId = null)
{
    public bool IsPlaced => Cars.Count > 0;
    public ModernTrainCarPlacement? Head => Cars.Count == 0 ? null : Cars[0];
    public int Length => Math.Max(1, Cars.Count);
    public int EffectivePassengerCapacity => PassengerCapacity > 0 ? PassengerCapacity : Length * 100;
    public int EffectivePassengerSeatedCapacity => PassengerSeatedCapacity > 0 ? PassengerSeatedCapacity : Length * 50;
}
