namespace FreeTrain.Modern;

public enum ModernCarKind
{
    TrainCar,
    Bus
}

public enum ModernCarPlacementKind
{
    Unplaced,
    InsideMap,
    OutsideMap
}

public sealed record ModernCarState(
    ModernCarPlacementKind Placement,
    ModernVoxelKey? Location,
    int? DirectionIndex)
{
    public static ModernCarState Unplaced { get; } = new(ModernCarPlacementKind.Unplaced, null, null);

    public static ModernCarState Inside(ModernVoxelKey location, ModernDirection direction)
    {
        return new ModernCarState(ModernCarPlacementKind.InsideMap, location, direction.Index);
    }
}

public sealed record ModernCar(
    string CarId,
    ModernCarKind Kind,
    ModernCarState State)
{
    public ModernDirection? Direction => State.DirectionIndex is { } index
        ? ModernDirection.FromIndex(index)
        : null;

    public ModernCar Place(ModernVoxelKey location, ModernDirection direction)
    {
        return this with { State = ModernCarState.Inside(location, direction) };
    }

    public ModernCar Remove()
    {
        return this with { State = ModernCarState.Unplaced };
    }
}
