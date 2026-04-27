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

    [Fact]
    public void SnapshotRoundTripPreservesSpecialRailKind()
    {
        ModernWorld original = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Special Rail World",
            24,
            24,
            0,
            9000,
            ModernWorldTerrainKind.Flat));
        Assert.True(original.AddRailLine(
            new TileLocation(3, 3, 0),
            new TileLocation(7, 3, 0),
            ModernSpecialRailKind.Garage) > 0);

        ModernWorldSnapshot snapshot = original.ToSnapshot();
        ModernWorld restored = ModernWorld.FromSnapshot(
            snapshot,
            new Dictionary<string, RoadContribution>(),
            new Dictionary<string, LandContribution>(),
            new Dictionary<string, SpriteContribution>(),
            new Dictionary<string, StationContribution>(),
            new Dictionary<string, TrainContribution>());

        Assert.Equal(ModernSpecialRailKind.Garage, snapshot.Rails.Single(rail => rail.H == 3 && rail.V == 3).SpecialKind);
        Assert.Equal(ModernSpecialRailKind.Garage, restored.Transport.SpecialRailTiles[(3, 3)]);
    }

    [Fact]
    public void TunnelRailCutsAndRestoresTerrain()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Tunnel World",
            24,
            24,
            0,
            100_000_000,
            ModernWorldTerrainKind.Flat));
        Assert.True(world.RaiseCorner(5, 5, TerrainCorner.Top));
        Assert.False(world.GetTerrainTile(5, 5).IsFlat);

        Assert.True(world.AddRailLine(
            new TileLocation(5, 5, world.GetGroundLevel(5, 5)),
            new TileLocation(5, 5, world.GetGroundLevel(5, 5)),
            ModernSpecialRailKind.Tunnel) > 0);

        Assert.True(world.GetTerrainTile(5, 5).IsFlat);
        Assert.True(world.RemoveTransportAt(new TileLocation(5, 5, world.GetGroundLevel(5, 5))));
        Assert.False(world.GetTerrainTile(5, 5).IsFlat);
    }

    [Fact]
    public void SpecialRailBuildAndRemovalChargeLegacyStyleCosts()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Cost World",
            24,
            24,
            0,
            100_000_000,
            ModernWorldTerrainKind.Flat));

        Assert.True(world.AddRailLine(
            new TileLocation(3, 3, 0),
            new TileLocation(7, 3, 0),
            ModernSpecialRailKind.Garage) > 0);
        Assert.Equal(100_000_000 - 6_500_000 * 5, world.Account.Cash);

        Assert.True(world.RemoveTransportAt(new TileLocation(3, 3, 0)));
        Assert.Equal(100_000_000 - 6_500_000 * 5 - 200_000, world.Account.Cash);
    }

    [Fact]
    public void GarageStoresAndDispatchesTrains()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Garage World",
            24,
            24,
            0,
            100_000_000,
            ModernWorldTerrainKind.Flat));
        Assert.True(world.AddRailLine(
            new TileLocation(3, 3, 0),
            new TileLocation(12, 3, 0),
            ModernSpecialRailKind.Garage) > 0);

        TrainContribution trainContribution = TestTrainContribution();
        Dictionary<string, TrainCarContribution> cars = TestTrainCars();
        ModernTrain train = new("train:test", trainContribution, Array.Empty<ModernTrainCarPlacement>(), 0);
        Assert.True(world.AddTrain(train));
        Assert.True(world.PlaceTrain(train.TrainId, new ModernVoxelKey(3, 3, 0), ModernDirection.East, cars));

        world.AdvanceClock(1);

        ModernTrain stored = Assert.Single(world.Trains);
        Assert.Equal(ModernTrainState.InGarage, stored.State);
        Assert.Empty(stored.Cars);
        Assert.NotNull(stored.GarageLocation);
        Assert.False(world.RemoveTransportAt(new TileLocation(stored.GarageLocation!.Value.H, stored.GarageLocation.Value.V, 0)));

        Assert.True(world.DispatchTrainFromGarage(stored.TrainId, cars));
        ModernTrain dispatched = Assert.Single(world.Trains);
        Assert.Equal(ModernTrainState.Moving, dispatched.State);
        Assert.Equal(3, dispatched.Cars.Count);
    }

    [Fact]
    public void BridgeRailsElectAndRemovePierColumns()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Bridge World",
            24,
            24,
            0,
            100_000_000,
            ModernWorldTerrainKind.Flat));

        Assert.True(world.AddRailLine(
            new TileLocation(4, 4, 2),
            new TileLocation(6, 4, 2),
            ModernSpecialRailKind.Bridge) > 0);

        Assert.Contains(new ModernVoxelKey(4, 4, 0), world.BridgePierVoxels);
        Assert.Contains(new ModernVoxelKey(4, 4, 1), world.BridgePierVoxels);
        Assert.DoesNotContain(new ModernVoxelKey(5, 4, 0), world.BridgePierVoxels);
        Assert.Contains(new ModernVoxelKey(6, 4, 1), world.BridgePierVoxels);
        Assert.Equal(2, world.GetRailLevel(4, 4));

        Assert.True(world.RemoveTransportAt(new TileLocation(4, 4, 2)));
        Assert.DoesNotContain(new ModernVoxelKey(4, 4, 0), world.BridgePierVoxels);
        Assert.DoesNotContain(new ModernVoxelKey(4, 4, 1), world.BridgePierVoxels);
    }

    private static TrainContribution TestTrainContribution()
    {
        return new TrainContribution(
            "test",
            "Test Trains",
            "train:test",
            "Modern",
            "Test",
            "Test Train",
            "",
            "",
            10,
            1_000_000,
            100,
            "",
            1,
            "car:test",
            "car:test",
            "car:test",
            null);
    }

    private static Dictionary<string, TrainCarContribution> TestTrainCars()
    {
        TrainCarContribution car = new(
            "test",
            "Test Trains",
            "car:test",
            "Test Car",
            100,
            50,
            false,
            new Dictionary<int, SpriteFrame>(),
            null);
        return new Dictionary<string, TrainCarContribution>
        {
            [car.Id] = car
        };
    }
}
