namespace FreeTrain.Modern;

public readonly record struct TileLocation(int H, int V, int Z, TerrainCorner Corner = TerrainCorner.Top);

public enum TerrainCorner
{
    Top,
    Right,
    Bottom,
    Left
}

public readonly record struct TerrainTilePreview(int BaseLevel, int SurfaceLevel, int Top, int Right, int Bottom, int Left)
{
    public bool IsFlat => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;
    public int MaxCornerHeight => Math.Max(Math.Max(Top, Right), Math.Max(Bottom, Left));
    public bool HasMountain => !IsFlat;
}

public enum ModernWorldChangeKind
{
    Terrain,
    Transport,
    Voxel,
    Entity,
    Clock,
    Economy,
    Reset
}

public enum ModernVoxelKind
{
    Traffic,
    Land,
    Structure
}

public enum ModernTransportKind
{
    Rail,
    Road
}

public enum ModernTrafficAccessoryKind
{
    RailRoadCrossing
}

public enum ModernRailRoadCrossingOrientation
{
    RailNorthSouth,
    RailEastWest
}

public readonly record struct ModernVoxelKey(int H, int V, int Z);

public sealed record ModernWorldChangedEventArgs(
    ModernWorldChangeKind Kind,
    ModernVoxelKey? Location = null,
    string? Description = null);

public sealed record ModernTrafficAccessory(
    ModernTrafficAccessoryKind Kind,
    ModernRailRoadCrossingOrientation? CrossingOrientation = null);

public sealed record ModernTrafficVoxel(
    ModernVoxelKey Location,
    byte RailDirectionMask,
    byte RoadDirectionMask,
    string? RoadContributionId,
    string? CarId = null,
    ModernTrafficAccessory? Accessory = null)
{
    public ModernVoxelKind Kind => ModernVoxelKind.Traffic;
    public bool HasRail => RailDirectionMask != 0;
    public bool HasRoad => RoadDirectionMask != 0;
    public bool IsEmpty => !HasRail && !HasRoad;
    public bool IsOccupied => !string.IsNullOrWhiteSpace(CarId);
    public bool HasRailRoadCrossing => Accessory?.Kind == ModernTrafficAccessoryKind.RailRoadCrossing;
}

