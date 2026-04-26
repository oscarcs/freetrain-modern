namespace FreeTrain.Modern;

public sealed record MapViewportStatus(
    string WorldName,
    TileLocation? HoverLocation,
    TileLocation? SelectedLocation,
    TileLocation? BuildAnchorLocation,
    MapEditMode EditMode,
    string ActiveRoadName,
    ModernWorldClock Clock,
    long Cash,
    long TotalDebt,
    int EntityCount,
    int TrafficCount,
    int RailTileCount,
    int RoadTileCount,
    int CarCount,
    double Zoom,
    int MaxVisibleLevel,
    int WorldMaxHeightCutLevel,
    bool ShowGrid,
    bool UseNightView,
    string InteractionHint,
    string LastMessage);
