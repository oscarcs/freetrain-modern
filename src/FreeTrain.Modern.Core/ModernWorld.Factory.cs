namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
    public static ModernWorld CreateSample(IReadOnlyList<RoadContribution> roads, int width = 34, int height = 34, int waterLevel = 0)
    {
        ModernWorld world = new(
            "Flat rail loop test world",
            width,
            height,
            waterLevel,
            BuildFlatFineHeights(width, height),
            ModernWorldClock.Default,
            ModernAccountState.Default);

        world.AddFlatRailLoop();

        world.RebuildTrafficVoxels();
        return world;
    }

    public static ModernWorld CreateNew(ModernWorldCreationOptions options)
    {
        ModernWorldCreationOptions normalized = options.Normalize();
        int[,] heights = normalized.TerrainKind switch
        {
            ModernWorldTerrainKind.Flat => BuildFlatFineHeights(
                normalized.Width,
                normalized.Height,
                normalized.WaterLevel <= 0 ? 0 : normalized.WaterLevel + 1),
            _ => BuildRepresentableFineHeights(normalized.Width, normalized.Height, normalized.WaterLevel)
        };

        ModernWorld world = new(
            normalized.Name,
            normalized.Width,
            normalized.Height,
            normalized.WaterLevel,
            heights,
            normalized.Clock ?? ModernWorldClock.Default,
            new ModernAccountState(normalized.InitialCash));

        world.RebuildTrafficVoxels();
        world.Publish(ModernWorldChangeKind.Reset, null, "New world created.");
        return world;
    }

    private void AddFlatRailLoop()
    {
        ModernLocation[] loop =
        {
            new(20, 18, 0),
            new(30, 18, 0),
            new(34, 22, 0),
            new(34, 27, 0),
            new(30, 31, 0),
            new(20, 31, 0),
            new(16, 27, 0),
            new(16, 22, 0)
        };

        for (int i = 0; i < loop.Length; i++)
        {
            AddRailLocationLine(loop[i], loop[(i + 1) % loop.Length]);
        }
    }

    private void AddRailLocationLine(ModernLocation from, ModernLocation to)
    {
        ModernLocation current = from;
        AddRailAtLocation(current);
        int guard = 0;
        while (current != to && guard++ < 512)
        {
            current = current.Toward(to);
            AddRailAtLocation(current);
        }
    }

    private void AddRailAtLocation(ModernLocation location)
    {
        (int h, int v) = ToHv(location);
        if (IsInside(h, v))
        {
            Transport.AddRailTile(h, v);
        }
    }

    private static RoadContribution? SelectInitialRoad(IReadOnlyList<RoadContribution> roads)
    {
        return roads
            .Where(road => road.IsLoadable)
            .OrderByDescending(road => road.Kind == RoadContributionKind.Standard)
            .ThenByDescending(road => road.Style.MajorType.Equals("street", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(road => road.Style.Lanes)
            .ThenBy(road => road.PluginDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(road => road.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

}
