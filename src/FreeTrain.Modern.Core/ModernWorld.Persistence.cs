namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
    public ModernWorldSnapshot ToSnapshot()
    {
        int fineWidth = fineHeights.GetLength(0);
        int fineHeight = fineHeights.GetLength(1);
        int[] serializedFineHeights = new int[fineWidth * fineHeight];
        for (int y = 0; y < fineHeight; y++)
        {
            for (int x = 0; x < fineWidth; x++)
            {
                serializedFineHeights[y * fineWidth + x] = fineHeights[x, y];
            }
        }

        ModernRoadSnapshot[] roads = Transport.RoadTiles
            .Select(entry => new ModernRoadSnapshot(entry.Key.H, entry.Key.V, entry.Value.Id))
            .ToArray();

        ModernRailSnapshot[] rails = Transport.RailTiles
            .Select(tile => new ModernRailSnapshot(
                tile.H,
                tile.V,
                Transport.SpecialRailTiles.GetValueOrDefault(tile, ModernSpecialRailKind.Normal),
                tunnelTerrainBackups.TryGetValue(tile, out int[]? backup) ? backup : null,
                GetRailLevel(tile.H, tile.V),
                GetRawRailMask(tile.H, tile.V)))
            .ToArray();

        ModernEntitySnapshot[] entitySnapshots = entities.Values
            .Select(entity => new ModernEntitySnapshot(
                entity.EntityId,
                entity.Kind,
                entity.H,
                entity.V,
                entity.Z,
                entity.FootprintH,
                entity.FootprintV,
                entity.FootprintZ,
                entity.ContributionId,
                entity.ResolvedContributionId,
                entity.IsOwned,
                entity.IsSilentlyReclaimable,
                entity.EntityValue,
                entity.StructureColorVariantIndex))
            .ToArray();

        ModernCarSnapshot[] carSnapshots = cars.Values
            .Select(car => new ModernCarSnapshot(
                car.CarId,
                car.Kind,
                car.State.Placement,
                car.State.Location?.H,
                car.State.Location?.V,
                car.State.Location?.Z,
                car.State.DirectionIndex))
            .ToArray();

        ModernStationSnapshot[] stationSnapshots = stations.Values
            .Select(station => new ModernStationSnapshot(
                station.StationId,
                station.H,
                station.V,
                station.Z,
                station.Contribution.Id,
                station.Name,
                station.Stats))
            .ToArray();

        ModernPlatformSnapshot[] platformSnapshots = platforms.Values
            .Select(platform => new ModernPlatformSnapshot(
                platform.PlatformId,
                platform.H,
                platform.V,
                platform.Z,
                platform.DirectionIndex,
                platform.Length,
                platform.Style,
                platform.StationId))
            .ToArray();

        ModernTrainSnapshot[] trainSnapshots = trains.Values
            .Select(train => new ModernTrainSnapshot(
                train.TrainId,
                train.Contribution.Id,
                train.Cars.Select(car => new ModernTrainCarPlacementSnapshot(
                    car.CarContributionId,
                    car.Location.H,
                    car.Location.V,
                    car.Location.Z,
                    car.DirectionIndex)).ToArray(),
                train.MinuteAccumulator,
                train.State,
                train.StopRemainingMinutes,
                train.MoveCount,
                train.PassengerCapacity,
                train.PassengerSeatedCapacity,
                train.PassengerCount,
                train.PassengerSourceLocation,
                train.CurrentStopPlatformId,
                train.LastStoppedPlatformId,
                train.GarageLocation,
                train.GarageDirectionIndex))
            .ToArray();

        return new ModernWorldSnapshot(
            Name,
            Width,
            Height,
            WaterLevel,
            Clock,
            Account,
            fineWidth,
            fineHeight,
            serializedFineHeights,
            rails,
            roads,
            carSnapshots,
            entitySnapshots,
            stationSnapshots,
            platformSnapshots,
            trainSnapshots);
    }

    public static ModernWorld FromSnapshot(
        ModernWorldSnapshot snapshot,
        IReadOnlyDictionary<string, RoadContribution> roadContributions,
        IReadOnlyDictionary<string, LandContribution> landContributions,
        IReadOnlyDictionary<string, SpriteContribution> spriteContributions,
        IReadOnlyDictionary<string, StationContribution> stationContributions,
        IReadOnlyDictionary<string, TrainContribution> trainContributions)
    {
        int[,] fineHeights = new int[snapshot.FineHeightWidth, snapshot.FineHeightHeight];
        for (int y = 0; y < snapshot.FineHeightHeight; y++)
        {
            for (int x = 0; x < snapshot.FineHeightWidth; x++)
            {
                int index = y * snapshot.FineHeightWidth + x;
                fineHeights[x, y] = index < snapshot.FineHeights.Length
                    ? Math.Clamp(snapshot.FineHeights[index], 0, MaxFineHeight)
                    : 0;
            }
        }

        ModernWorld world = new(
            snapshot.Name,
            snapshot.Width,
            snapshot.Height,
            snapshot.WaterLevel,
            fineHeights,
            snapshot.Clock,
            snapshot.Account);

        foreach (ModernRailSnapshot rail in snapshot.Rails)
        {
            int z = rail.Z ?? world.GetGroundLevel(rail.H, rail.V);
            world.Transport.AddRailTile(rail.H, rail.V, z, rail.SpecialKind, rail.DirectionMask);
            if (rail.SpecialKind == ModernSpecialRailKind.Tunnel && rail.TerrainFineHeights is { Count: 4 })
            {
                world.tunnelTerrainBackups[(rail.H, rail.V)] = rail.TerrainFineHeights.ToArray();
            }
            else if (rail.SpecialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported)
            {
                world.BuildBridgePiers(new TileLocation(rail.H, rail.V, z), rail.SpecialKind == ModernSpecialRailKind.Bridge);
            }
        }

        foreach (ModernRoadSnapshot road in snapshot.Roads)
        {
            if (roadContributions.TryGetValue(road.RoadContributionId, out RoadContribution? contribution))
            {
                world.Transport.AddRoadTile(road.H, road.V, contribution);
            }
        }

        foreach (ModernEntitySnapshot entity in snapshot.Entities)
        {
            ModernPlacedEntity? placed = entity.Kind switch
            {
                ModernEntityKind.Land => RestoreLandEntity(entity, landContributions),
                ModernEntityKind.Structure => RestoreStructureEntity(entity, spriteContributions),
                _ => null
            };

            if (placed is not null)
            {
                world.AddEntity(placed);
            }
        }

        foreach (ModernCarSnapshot carSnapshot in snapshot.Cars)
        {
            ModernCar car = new(carSnapshot.CarId, carSnapshot.Kind, ModernCarState.Unplaced);
            if (world.AddCar(car)
                && carSnapshot.Placement == ModernCarPlacementKind.InsideMap
                && carSnapshot.H is { } h
                && carSnapshot.V is { } v
                && carSnapshot.Z is { } z
                && carSnapshot.DirectionIndex is { } directionIndex)
            {
                world.PlaceCar(car.CarId, new ModernVoxelKey(h, v, z), ModernDirection.FromIndex(directionIndex));
            }
        }

        foreach (ModernStationSnapshot stationSnapshot in snapshot.Stations)
        {
            if (stationContributions.TryGetValue(stationSnapshot.ContributionId, out StationContribution? contribution))
            {
                world.AddStation(new ModernStation(
                    stationSnapshot.StationId,
                    stationSnapshot.H,
                    stationSnapshot.V,
                    stationSnapshot.Z,
                    contribution,
                    stationSnapshot.Stats)
                {
                    Name = stationSnapshot.Name
                });
            }
        }

        foreach (ModernPlatformSnapshot platformSnapshot in snapshot.Platforms)
        {
            world.AddPlatform(new ModernPlatform(
                platformSnapshot.PlatformId,
                platformSnapshot.H,
                platformSnapshot.V,
                platformSnapshot.Z,
                platformSnapshot.DirectionIndex,
                platformSnapshot.Length,
                platformSnapshot.Style,
                platformSnapshot.StationId));
        }

        foreach (ModernTrainSnapshot trainSnapshot in snapshot.Trains)
        {
            if (trainContributions.TryGetValue(trainSnapshot.ContributionId, out TrainContribution? contribution))
            {
                ModernTrainCarPlacement[] placements = trainSnapshot.Cars
                    .Select(car => new ModernTrainCarPlacement(
                        car.CarContributionId,
                        new ModernVoxelKey(car.H, car.V, car.Z),
                        car.DirectionIndex))
                    .ToArray();
                world.AddTrain(new ModernTrain(
                    trainSnapshot.TrainId,
                    contribution,
                    placements,
                    trainSnapshot.MinuteAccumulator,
                    trainSnapshot.State,
                    trainSnapshot.StopRemainingMinutes,
                    trainSnapshot.MoveCount,
                    trainSnapshot.PassengerCapacity,
                    trainSnapshot.PassengerSeatedCapacity,
                    trainSnapshot.PassengerCount,
                    trainSnapshot.PassengerSourceLocation,
                    trainSnapshot.CurrentStopPlatformId,
                    trainSnapshot.LastStoppedPlatformId,
                    trainSnapshot.GarageLocation,
                    trainSnapshot.GarageDirectionIndex));
            }
        }

        world.RebuildTrafficVoxels();
        world.Publish(ModernWorldChangeKind.Reset, null, "World loaded from snapshot.");
        return world;
    }

    private static ModernPlacedEntity? RestoreLandEntity(
        ModernEntitySnapshot snapshot,
        IReadOnlyDictionary<string, LandContribution> landContributions)
    {
        if (!landContributions.TryGetValue(snapshot.ContributionId, out LandContribution? contribution))
        {
            return null;
        }

        LandContribution? resolved = null;
        if (!string.IsNullOrWhiteSpace(snapshot.ResolvedContributionId))
        {
            landContributions.TryGetValue(snapshot.ResolvedContributionId, out resolved);
        }

        return ModernPlacedEntity.Land(snapshot.H, snapshot.V, snapshot.Z, contribution, resolved);
    }

    private static ModernPlacedEntity? RestoreStructureEntity(
        ModernEntitySnapshot snapshot,
        IReadOnlyDictionary<string, SpriteContribution> spriteContributions)
    {
        if (!spriteContributions.TryGetValue(snapshot.ContributionId, out SpriteContribution? contribution))
        {
            return null;
        }

        SpriteFrame? frame = contribution.Frames.FirstOrDefault(candidate => candidate.IsLoadable);
        return frame is null
            ? null
            : ModernPlacedEntity.Structure(snapshot.H, snapshot.V, snapshot.Z, contribution, frame, snapshot.StructureColorVariantIndex);
    }

}
