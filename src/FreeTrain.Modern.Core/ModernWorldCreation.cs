namespace FreeTrain.Modern;

public enum ModernWorldTerrainKind
{
    Flat,
    Heightmap
}

public sealed record ModernWorldCreationOptions(
    string Name,
    int Width,
    int Height,
    int WaterLevel,
    long InitialCash,
    ModernWorldTerrainKind TerrainKind,
    ModernWorldClock? Clock = null)
{
    public const int MinSize = 16;
    public const int MaxSize = 256;

    public static ModernWorldCreationOptions Default { get; } = new(
        "New FreeTrain World",
        56,
        224,
        1,
        ModernAccountState.Default.Cash,
        ModernWorldTerrainKind.Heightmap);

    public ModernWorldCreationOptions Normalize()
    {
        string normalizedName = string.IsNullOrWhiteSpace(Name)
            ? Default.Name
            : Name.Trim();

        return this with
        {
            Name = normalizedName,
            Width = Math.Clamp(Width, MinSize, MaxSize),
            Height = Math.Clamp(Height, MinSize, MaxSize),
            WaterLevel = Math.Clamp(WaterLevel, 0, 7),
            InitialCash = Math.Max(0, InitialCash),
            Clock = Clock ?? ModernWorldClock.Default
        };
    }
}
