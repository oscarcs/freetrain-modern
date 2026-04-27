using FreeTrain.Modern;

namespace FreeTrain.Modern.Tests;

public sealed class ModernWorldCreationTests
{
    [Fact]
    public void CreateNewStartsWithEmptyPlayerBuiltWorld()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Port Test",
            32,
            28,
            1,
            123_456_789,
            ModernWorldTerrainKind.Flat));

        Assert.Equal("Port Test", world.Name);
        Assert.Equal(32, world.Width);
        Assert.Equal(28, world.Height);
        Assert.Equal(1, world.WaterLevel);
        Assert.Equal(123_456_789, world.Account.Cash);
        Assert.Empty(world.Transport.RailTiles);
        Assert.Empty(world.Transport.RoadTiles);
        Assert.Empty(world.Stations);
        Assert.Empty(world.Platforms);
        Assert.Empty(world.Trains);
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void CreationOptionsClampUnsafeValues()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            " ",
            1,
            999,
            99,
            -10,
            ModernWorldTerrainKind.Flat));

        Assert.Equal(ModernWorldCreationOptions.Default.Name, world.Name);
        Assert.Equal(ModernWorldCreationOptions.MinSize, world.Width);
        Assert.Equal(ModernWorldCreationOptions.MaxSize, world.Height);
        Assert.Equal(7, world.WaterLevel);
        Assert.Equal(0, world.Account.Cash);
    }

    [Fact]
    public void SnapshotRoundTripPreservesNewWorldCoreState()
    {
        ModernWorld original = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Snapshot World",
            24,
            24,
            0,
            9000,
            ModernWorldTerrainKind.Flat));
        Assert.True(original.AddRailLine(new TileLocation(3, 3, 0), new TileLocation(7, 3, 0)) > 0);
        original.AdvanceClock(ModernWorldClock.HourLength * 2);

        ModernWorldSnapshot snapshot = original.ToSnapshot();
        ModernWorld restored = ModernWorld.FromSnapshot(
            snapshot,
            new Dictionary<string, RoadContribution>(),
            new Dictionary<string, LandContribution>(),
            new Dictionary<string, SpriteContribution>(),
            new Dictionary<string, StationContribution>(),
            new Dictionary<string, TrainContribution>());

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Width, restored.Width);
        Assert.Equal(original.Clock, restored.Clock);
        Assert.Equal(original.Account.Cash, restored.Account.Cash);
        Assert.Equal(original.Transport.RailTiles.Count, restored.Transport.RailTiles.Count);
    }
}
