namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
    public bool AddCar(ModernCar car)
    {
        if (cars.ContainsKey(car.CarId))
        {
            return false;
        }

        cars[car.CarId] = car;
        Publish(ModernWorldChangeKind.Entity, car.State.Location, "Car added.");
        return true;
    }

    public bool AddTrain(ModernTrain train)
    {
        if (trains.ContainsKey(train.TrainId))
        {
            return false;
        }

        trains[train.TrainId] = train;
        Publish(ModernWorldChangeKind.Entity, train.Head?.Location, "Train added.");
        return true;
    }

    public bool PlaceTrain(string trainId, ModernVoxelKey location, ModernDirection direction, IReadOnlyDictionary<string, TrainCarContribution> trainCars)
    {
        if (!trains.TryGetValue(trainId, out ModernTrain? train) || !Transport.HasRail(location.H, location.V))
        {
            return false;
        }

        IReadOnlyList<string> carIds = train.Contribution.CreateCarIds(3);
        ModernVoxelKey[] locations = new ModernVoxelKey[carIds.Count];
        ModernDirection[] directions = new ModernDirection[carIds.Count];
        ModernVoxelKey current = location;
        ModernDirection? currentDirection = null;

        for (int index = carIds.Count - 1; index >= 0; index--)
        {
            ModernRailRoad? railRoad = CreateRailRoad(current.H, current.V);
            if (!trainCars.ContainsKey(carIds[index])
                || railRoad is null
                || IsTrainOccupying(current))
            {
                return false;
            }

            currentDirection ??= railRoad.Dir1;
            locations[index] = current;
            directions[index] = currentDirection;
            currentDirection = railRoad.Guide(currentDirection);

            if (index > 0 && !TryStepOnRail(current, currentDirection, out current))
            {
                return false;
            }
        }

        for (int i = 0; i < locations.Length - 1; i++)
        {
            for (int j = i + 1; j < locations.Length; j++)
            {
                if (locations[i] == locations[j])
                {
                    return false;
                }
            }
        }

        ModernTrainCarPlacement[] placements = carIds
            .Select((carId, index) => new ModernTrainCarPlacement(carId, locations[index], directions[index].Index))
            .ToArray();
        int passengerCapacity = carIds.Sum(carId => trainCars.TryGetValue(carId, out TrainCarContribution? car) ? Math.Max(0, car.Capacity) : 0);
        int passengerSeatedCapacity = carIds.Sum(carId => trainCars.TryGetValue(carId, out TrainCarContribution? car) ? Math.Max(0, car.SeatedCapacity) : 0);

        trains[trainId] = train with
        {
            Cars = placements,
            MinuteAccumulator = 0,
            State = ModernTrainState.Moving,
            StopRemainingMinutes = 0,
            PassengerCapacity = passengerCapacity,
            PassengerSeatedCapacity = passengerSeatedCapacity,
            PassengerCount = 0,
            PassengerSourceLocation = null,
            CurrentStopPlatformId = null,
            LastStoppedPlatformId = null,
            GarageLocation = null,
            GarageDirectionIndex = null
        };
        Publish(ModernWorldChangeKind.Entity, location, "Train placed.");
        return true;
    }

    public bool StoreTrainInGarage(string trainId)
    {
        if (!trains.TryGetValue(trainId, out ModernTrain? train)
            || train.Head is not { } head
            || !IsGarageRail(head.Location.H, head.Location.V)
            || !train.Cars.All(car => IsGarageRail(car.Location.H, car.Location.V)))
        {
            return false;
        }

        trains[trainId] = train with
        {
            Cars = Array.Empty<ModernTrainCarPlacement>(),
            State = ModernTrainState.InGarage,
            MinuteAccumulator = 0,
            StopRemainingMinutes = 0,
            CurrentStopPlatformId = null,
            GarageLocation = head.Location,
            GarageDirectionIndex = head.DirectionIndex
        };
        Publish(ModernWorldChangeKind.Entity, head.Location, "Train stored in garage.");
        return true;
    }

    public bool DispatchTrainFromGarage(string trainId, IReadOnlyDictionary<string, TrainCarContribution> trainCars)
    {
        if (!trains.TryGetValue(trainId, out ModernTrain? train)
            || train.State != ModernTrainState.InGarage
            || train.GarageLocation is not { } garageLocation)
        {
            return false;
        }

        return PlaceTrain(
            trainId,
            garageLocation,
            ModernDirection.FromIndex(train.GarageDirectionIndex ?? 2),
            trainCars);
    }

    public bool PlaceCar(string carId, ModernVoxelKey location, ModernDirection direction)
    {
        if (!cars.TryGetValue(carId, out ModernCar? car))
        {
            return false;
        }

        if (!CanPlaceCar(car.Kind, location))
        {
            return false;
        }

        RemoveCarFromTraffic(car);
        ModernCar placed = car.Place(location, direction);
        cars[carId] = placed;
        trafficCars[location] = carId;
        SynchronizeTrafficVoxel(location.H, location.V);
        Publish(ModernWorldChangeKind.Entity, location, "Car placed.");
        return true;
    }

    public bool RemoveCar(string carId)
    {
        if (!cars.TryGetValue(carId, out ModernCar? car))
        {
            return false;
        }

        ModernVoxelKey? previousLocation = car.State.Location;
        RemoveCarFromTraffic(car);
        cars[carId] = car.Remove();
        if (previousLocation is { } location)
        {
            SynchronizeTrafficVoxel(location.H, location.V);
        }

        Publish(ModernWorldChangeKind.Entity, previousLocation, "Car removed.");
        return true;
    }

    public ModernTrainCarRenderPose GetTrainCarRenderPose(ModernTrainCarPlacement car)
    {
        ModernRailRoad? railRoad = CreateRailRoad(car.Location.H, car.Location.V);
        if (railRoad is null)
        {
            return new ModernTrainCarRenderPose((car.DirectionIndex * 2) & 15, 0, 0);
        }

        int d1 = car.DirectionIndex;
        int d2 = railRoad.Guide(car.Direction).Index;
        if (d1 == d2)
        {
            return new ModernTrainCarRenderPose((d1 * 2) & 15, 0, 0);
        }

        int diff = (d2 - d1) & 7;
        if (diff == 7)
        {
            diff = -1;
        }

        int dd = (d2 * 2 + diff * 3) & 15;
        int offsetX = 2 < dd && dd < 10 ? 3 : -3;
        int offsetY = 6 < dd && dd <= 14 ? 2 : -2;
        int angle = (d1 * 2 + diff) & 15;
        return new ModernTrainCarRenderPose(angle, offsetX, offsetY);
    }

    private void AdvanceTrains(long minutes)
    {
        if (minutes <= 0 || trains.Count == 0)
        {
            return;
        }

        foreach (ModernTrain train in trains.Values.ToArray())
        {
            if (!train.IsPlaced)
            {
                continue;
            }

            long accumulator = train.MinuteAccumulator + minutes;
            ModernTrain current = train;
            int guard = 0;
            while (accumulator > 0 && guard++ < 256)
            {
                if (current.State == ModernTrainState.StoppingAtStation)
                {
                    long consumed = Math.Min(accumulator, Math.Max(1, current.StopRemainingMinutes));
                    accumulator -= consumed;
                    current = current with { StopRemainingMinutes = Math.Max(0, current.StopRemainingMinutes - consumed) };
                    if (current.StopRemainingMinutes > 0)
                    {
                        break;
                    }

                    current = LoadPassengersAndResume(current);
                    continue;
                }

                int stepMinutes = Math.Max(1, current.Contribution.MinutesPerVoxel);
                if (accumulator < stepMinutes)
                {
                    break;
                }

                ModernTrain? stopping = TryBeginStationStop(current);
                if (stopping is not null)
                {
                    current = stopping;
                    continue;
                }

                accumulator -= stepMinutes;
                current = MoveTrainOneTile(current);
            }

            trains[current.TrainId] = current with { MinuteAccumulator = accumulator };
        }
    }

    private ModernTrain MoveTrainOneTile(ModernTrain train)
    {
        if (train.Head is not { } head)
        {
            return train;
        }

        ModernRailRoad? railRoad = CreateRailRoad(head.Location.H, head.Location.V);
        if (railRoad is null)
        {
            return train with { State = ModernTrainState.EmergencyStopping };
        }

        ModernDirection direction = railRoad.Guide(head.Direction);
        if (!TryStepOnRail(head.Location, direction, out ModernVoxelKey next))
        {
            return ReverseTrain(train) with { State = ModernTrainState.EmergencyStopping };
        }
        if (IsTrainOccupying(next, train.TrainId))
        {
            return train with { State = ModernTrainState.EmergencyStopping };
        }

        List<ModernTrainCarPlacement> moved = new()
        {
            new ModernTrainCarPlacement(head.CarContributionId, next, direction.Index)
        };

        for (int i = 1; i < train.Cars.Count; i++)
        {
            ModernTrainCarPlacement previous = train.Cars[i - 1];
            moved.Add(new ModernTrainCarPlacement(
                train.Cars[i].CarContributionId,
                previous.Location,
                previous.DirectionIndex));
        }

        int moveCount = train.MoveCount + 1;
        if ((train.MoveCount & 3) == 0)
        {
            Spend((train.Length * 20L + train.PassengerCount / 20L) * 2_000L, ModernAccountGenre.Railway, "Train running cost.");
        }

        string? lastStoppedPlatformId = GetPlatformIdAt(next) == train.LastStoppedPlatformId
            ? train.LastStoppedPlatformId
            : null;

        if (moved.All(car => IsGarageRail(car.Location.H, car.Location.V)))
        {
            return train with
            {
                Cars = Array.Empty<ModernTrainCarPlacement>(),
                State = ModernTrainState.InGarage,
                MinuteAccumulator = 0,
                StopRemainingMinutes = 0,
                MoveCount = moveCount,
                CurrentStopPlatformId = null,
                LastStoppedPlatformId = lastStoppedPlatformId,
                GarageLocation = next,
                GarageDirectionIndex = direction.Index
            };
        }

        return train with
        {
            Cars = moved,
            State = ModernTrainState.Moving,
            MoveCount = moveCount,
            LastStoppedPlatformId = lastStoppedPlatformId,
            GarageLocation = null,
            GarageDirectionIndex = null
        };
    }

    private ModernTrain ReverseTrain(ModernTrain train)
    {
        ModernTrainCarPlacement[] reversed = train.Cars
            .Reverse()
            .Select(car => car with { DirectionIndex = car.Direction.Opposite.Index })
            .ToArray();
        return train with { Cars = reversed };
    }

    private ModernTrain? TryBeginStationStop(ModernTrain train)
    {
        if (train.Head is not { } head)
        {
            return null;
        }

        PlatformStop? stop = FindPlatformStop(train, head);
        if (stop is null || stop.Value.Platform.StationId is null || stop.Value.Platform.PlatformId == train.LastStoppedPlatformId)
        {
            return null;
        }

        return train with
        {
            State = ModernTrainState.StoppingAtStation,
            StopRemainingMinutes = 30,
            CurrentStopPlatformId = stop.Value.Platform.PlatformId,
            LastStoppedPlatformId = stop.Value.Platform.PlatformId,
            PassengerCount = UnloadPassengers(train, stop.Value.Platform)
        };
    }

    private ModernTrain LoadPassengersAndResume(ModernTrain train)
    {
        int passengers = 0;
        ModernVoxelKey? sourceLocation = null;
        if (train.CurrentStopPlatformId is { } platformId
            && platforms.TryGetValue(platformId, out ModernPlatform? platform)
            && platform.StationId is { } stationId
            && stations.TryGetValue(stationId, out ModernStation? station)
            && train.Head is { } head)
        {
            passengers = LoadPassengers(station, train, GetTrainPassengerPackingCapacity(train));
            sourceLocation = passengers > 0 ? head.Location : null;
        }

        return train with
        {
            State = ModernTrainState.Moving,
            StopRemainingMinutes = 0,
            PassengerCount = passengers,
            PassengerSourceLocation = sourceLocation,
            CurrentStopPlatformId = null
        };
    }

    private int UnloadPassengers(ModernTrain train, ModernPlatform platform)
    {
        if (platform.StationId is not { } stationId
            || !stations.TryGetValue(stationId, out ModernStation? station))
        {
            return train.PassengerCount;
        }

        int unloaded = Math.Max(0, train.PassengerCount);
        double developmentQuantity = unloaded;
        if (train.PassengerSourceLocation is { } source && train.Head is { } head)
        {
            int distance = Math.Max(1, Math.Abs(source.H - head.Location.H) + Math.Abs(source.V - head.Location.V) + Math.Abs(source.Z - head.Location.Z));
            Earn(unloaded * train.Contribution.Fare * distance * 2L, ModernAccountGenre.Railway, "Passenger fare income.");
            developmentQuantity = Math.Min(station.Stats.ScoreImported / 24.0, unloaded);
        }

        stations[stationId] = station with { Stats = station.Stats.RecordArrival(unloaded, developmentQuantity) };
        return 0;
    }

    private int LoadPassengers(ModernStation station, ModernTrain train, int passengerPackingCapacity)
    {
        int population = GetStationPopulation(station);
        ModernStationStats stats = station.Stats;
        int passengerCount = 0;
        if (population > 0)
        {
            int available = stats.WaitingPassengers(population);
            passengerCount = Math.Min(
                Math.Max(0, passengerPackingCapacity),
                (int)(available * Math.Max(0, train.Contribution.Amenity) * 0.01f * 0.3f));
        }

        stations[station.StationId] = station with { Stats = stats.RecordDeparture(passengerCount) };
        return passengerCount;
    }

    private int GetTrainPassengerPackingCapacity(ModernTrain train)
    {
        int capacity = train.EffectivePassengerCapacity;
        int seatedCapacity = train.EffectivePassengerSeatedCapacity;
        int seated = seatedCapacity > capacity ? capacity : Math.Max(0, seatedCapacity);
        return seated + (capacity - seated) * 2;
    }

    private PlatformStop? FindPlatformStop(ModernTrain train, ModernTrainCarPlacement head)
    {
        if (!platformVoxels.TryGetValue(head.Location, out string? platformId)
            || !platforms.TryGetValue(platformId, out ModernPlatform? platform)
            || platform.StationId is null)
        {
            return null;
        }

        ModernVoxelKey[] platformVoxelsForPlatform = EnumeratePlatformVoxels(platform).ToArray();
        int index = Array.IndexOf(platformVoxelsForPlatform, head.Location);
        if (index < 0)
        {
            return null;
        }

        if (platform.Direction != head.Direction && platform.Direction != head.Direction.Opposite)
        {
            return null;
        }

        int stopIndex = platform.Direction == head.Direction
            ? (platform.Length + train.Length) / 2 - 1
            : (platform.Length - train.Length) / 2;

        return stopIndex == index && stopIndex >= 0 && stopIndex < platform.Length
            ? new PlatformStop(platform, index)
            : null;
    }

    private string? GetPlatformIdAt(ModernVoxelKey key)
    {
        return platformVoxels.TryGetValue(key, out string? platformId)
            ? platformId
            : null;
    }

    private bool IsTrainOccupying(ModernVoxelKey key)
    {
        return trains.Values
            .SelectMany(train => train.Cars)
            .Any(car => car.Location == key);
    }

    private bool IsTrainOccupying(ModernVoxelKey key, string exceptTrainId)
    {
        return trains.Values
            .Where(train => !string.Equals(train.TrainId, exceptTrainId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(train => train.Cars)
            .Any(car => car.Location == key);
    }

    private bool IsTrainOccupyingRailTile(int h, int v)
    {
        return trains.Values.Any(train =>
            train.Cars.Any(car => car.Location.H == h && car.Location.V == v)
            || train.GarageLocation is { } garageLocation && garageLocation.H == h && garageLocation.V == v);
    }

    private bool IsGarageRail(int h, int v)
    {
        return Transport.SpecialRailTiles.GetValueOrDefault((h, v), ModernSpecialRailKind.Normal) == ModernSpecialRailKind.Garage;
    }

    private void RemoveCarFromTraffic(ModernCar car)
    {
        if (car.State.Location is { } location && trafficCars.TryGetValue(location, out string? carId) && carId == car.CarId)
        {
            trafficCars.Remove(location);
        }
    }

}
