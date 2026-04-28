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
        Assert.Equal(2, world.GetGroundLevel(0, 0));
        Assert.True(world.IsBuildableSurface(new TileLocation(0, 0, 2)));
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
    public void HeightmapGenerationCreatesTerracedBuildableLandAndWater()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Terraced Heightmap",
            64,
            64,
            1,
            100_000_000,
            ModernWorldTerrainKind.Heightmap));

        int waterTiles = 0;
        int dryFlatTiles = 0;
        int shoreTransitionTiles = 0;
        HashSet<int> levels = new();
        for (int v = 0; v < world.Height; v++)
        {
            for (int h = 0; h < world.Width; h++)
            {
                TerrainTilePreview terrain = world.GetTerrainTile(h, v);
                levels.Add(terrain.SurfaceLevel);
                if (terrain.SurfaceLevel <= world.WaterLevel)
                {
                    waterTiles++;
                }

                if (terrain.IsFlat && terrain.SurfaceLevel > world.WaterLevel)
                {
                    dryFlatTiles++;
                }

                if (terrain.BaseLevel <= world.WaterLevel
                    && terrain.BaseLevel * 4 + terrain.MaxCornerHeight > world.WaterLevel * 4)
                {
                    shoreTransitionTiles++;
                }
            }
        }

        int tileCount = world.Width * world.Height;
        Assert.True(waterTiles > tileCount / 12);
        Assert.True(dryFlatTiles > tileCount / 5);
        Assert.True(shoreTransitionTiles > 0);
        Assert.True(levels.Count >= 4);
        Assert.True(world.MaxGroundLevel >= world.WaterLevel + 2);
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
        Assert.NotEqual(0, snapshot.Rails.Single(rail => rail.H == 3 && rail.V == 3).DirectionMask);
        Assert.Equal(ModernSpecialRailKind.Garage, restored.Transport.SpecialRailTiles[(3, 3)]);
        Assert.Equal(
            snapshot.Rails.Single(rail => rail.H == 3 && rail.V == 3).DirectionMask,
            restored.Transport.RailDirectionMasks[(3, 3)]);
    }

    [Fact]
    public void RailLinesStoreContiguousDirectionalConnections()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Rail Mask World",
            24,
            24,
            0,
            100_000_000,
            ModernWorldTerrainKind.Flat));

        TileLocation from = new(3, 3, 0);
        TileLocation to = new(7, 3, 0);
        IReadOnlyList<TileLocation> route = world.PreviewRailLine(from, to);

        Assert.True(world.AddRailLine(from, to) > 0);
        Assert.Equal(route.Count, world.Transport.RailTiles.Count);
        Assert.Equal(route.Count, world.CreateRailObjects().Count);

        for (int i = 0; i < route.Count - 1; i++)
        {
            TileLocation current = route[i];
            TileLocation next = route[i + 1];
            ModernDirection direction = world.ToLocation(current.H, current.V, current.Z)
                .GetDirectionTo(world.ToLocation(next.H, next.V, next.Z));
            byte currentMask = world.Transport.RailDirectionMasks[(current.H, current.V)];
            byte nextMask = world.Transport.RailDirectionMasks[(next.H, next.V)];

            Assert.NotEqual(0, currentMask & (1 << direction.Index));
            Assert.NotEqual(0, nextMask & (1 << direction.Opposite.Index));
        }
    }

    [Fact]
    public void ParallelRailLinesDoNotAccidentallyConnectToAdjacentTiles()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Parallel Rail World",
            24,
            24,
            0,
            100_000_000,
            ModernWorldTerrainKind.Flat));

        Assert.True(world.AddRailLine(new TileLocation(3, 3, 0), new TileLocation(7, 3, 0)) > 0);
        Assert.True(world.AddRailLine(new TileLocation(3, 4, 0), new TileLocation(7, 4, 0)) > 0);

        ModernDirection adjacentDirection = world.ToLocation(3, 3, 0).GetDirectionTo(world.ToLocation(3, 4, 0));
        byte mask = world.Transport.RailDirectionMasks[(3, 3)];

        Assert.Equal(10, world.Transport.RailTiles.Count);
        Assert.Equal(0, mask & (1 << adjacentDirection.Index));
    }

    [Fact]
    public void ExtendingLooseRailEndpointReplacesTheStubDirection()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Endpoint Rail World",
            24,
            24,
            0,
            100_000_000,
            ModernWorldTerrainKind.Flat));

        Assert.True(world.AddRailLine(new TileLocation(3, 3, 0), new TileLocation(7, 3, 0)) > 0);
        Assert.True(world.AddRailLine(new TileLocation(7, 3, 0), new TileLocation(8, 2, 0)) > 0);

        byte mask = world.Transport.RailDirectionMasks[(7, 3)];

        Assert.Equal(2, CountBits(mask));
        Assert.NotNull(ModernRailPattern.FromDirectionMask(mask));
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
            new TileLocation(5, 6, world.GetGroundLevel(5, 5)),
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
    public void RailwayInspectionFindsGarageTrainAndRemoveTrainClearsIt()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Rail Ops World",
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
        ModernTrain train = new("train:ops", trainContribution, Array.Empty<ModernTrainCarPlacement>(), 0);
        Assert.True(world.AddTrain(train));
        Assert.True(world.PlaceTrain(train.TrainId, new ModernVoxelKey(3, 3, 0), ModernDirection.East, cars));
        Assert.True(world.StoreTrainInGarage(train.TrainId));
        ModernTrain stored = Assert.Single(world.Trains);
        Assert.NotNull(stored.GarageLocation);

        ModernRailwayTileInspection inspection = world.InspectRailwayAt(new TileLocation(stored.GarageLocation!.Value.H, stored.GarageLocation.Value.V, stored.GarageLocation.Value.Z));

        Assert.Equal(ModernSpecialRailKind.Garage, inspection.SpecialRailKind);
        Assert.Equal("train:ops", inspection.Train?.TrainId);
        Assert.True(world.CanDispatchTrainFromGarage(train.TrainId, cars));
        Assert.True(world.RemoveTrain(train.TrainId));
        Assert.Null(world.InspectRailwayAt(new TileLocation(stored.GarageLocation.Value.H, stored.GarageLocation.Value.V, stored.GarageLocation.Value.Z)).Train);
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

    [Fact]
    public void SpecialRailsFollowLegacyLineAndSupportRules()
    {
        ModernWorld world = ModernWorld.CreateNew(new ModernWorldCreationOptions(
            "Special Rail Rule World",
            24,
            24,
            0,
            100_000_000,
            ModernWorldTerrainKind.Flat));

        Assert.Equal(0, world.AddRailLine(
            new TileLocation(3, 3, 0),
            new TileLocation(3, 3, 0),
            ModernSpecialRailKind.Garage));
        Assert.Equal(0, world.AddRailLine(
            new TileLocation(3, 3, 0),
            new TileLocation(5, 3, 0),
            ModernSpecialRailKind.Tunnel));

        Assert.True(world.AddRailLine(
            new TileLocation(3, 4, 2),
            new TileLocation(5, 4, 2),
            ModernSpecialRailKind.Bridge) > 0);
        Assert.Contains(new ModernVoxelKey(3, 4, 0), world.BridgePierVoxels);
        Assert.DoesNotContain(new ModernVoxelKey(4, 4, 0), world.BridgePierVoxels);
        Assert.Contains(new ModernVoxelKey(5, 4, 0), world.BridgePierVoxels);

        Assert.True(world.AddRailLine(
            new TileLocation(8, 4, 2),
            new TileLocation(10, 4, 2),
            ModernSpecialRailKind.SteelSupported) > 0);
        Assert.Contains(new ModernVoxelKey(8, 4, 0), world.BridgePierVoxels);
        Assert.Contains(new ModernVoxelKey(9, 4, 0), world.BridgePierVoxels);
        Assert.Contains(new ModernVoxelKey(10, 4, 0), world.BridgePierVoxels);
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

    private static int CountBits(byte value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= (byte)(value - 1);
            count++;
        }

        return count;
    }
}
