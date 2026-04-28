namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
    public bool AddRailTile(TileLocation location)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        if (!IsBuildableSurface(location)
            || !IsReusable(key)
            || Transport.HasRoad(location.H, location.V))
        {
            return false;
        }

        RemoveReusableEntityAt(ToVoxelKey(location));
        bool changed = Transport.AddRailTile(location.H, location.V, location.Z);
        if (changed)
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(location), "Rail tile added.");
        }

        return changed;
    }

    public bool AddRoadTile(TileLocation location, RoadContribution contribution)
    {
        if (!IsTransportBuildableSurface(location))
        {
            return false;
        }

        RemoveReusableEntityAt(ToVoxelKey(location));
        bool changed = Transport.AddRoadTile(location.H, location.V, contribution);
        if (changed)
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(location), "Road tile added.");
        }

        return changed;
    }

    public int AddRailLine(TileLocation from, TileLocation to)
    {
        return AddRailLine(from, to, ModernSpecialRailKind.Normal);
    }

    public int AddRailLine(TileLocation from, TileLocation to, ModernSpecialRailKind specialKind)
    {
        if (!TryCreateRailRoute(from, to, out IReadOnlyList<TileLocation> route)
            || !CanApplyRailRoute(route, specialKind, out IReadOnlyDictionary<(int H, int V), byte> railMasks))
        {
            return 0;
        }

        ReclaimTransportRoute(route);
        for (int i = 0; i < route.Count; i++)
        {
            ApplySpecialRailBuildEffects(route[i], specialKind, i);
        }

        int changed = 0;
        foreach (TileLocation location in route)
        {
            if (Transport.AddRailTile(location.H, location.V, location.Z, specialKind, railMasks[(location.H, location.V)]))
            {
                changed++;
            }
            else if (Transport.SetRailDirectionMask(location.H, location.V, railMasks[(location.H, location.V)]))
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            SynchronizeTrafficVoxelsAlong(route);
            Spend(CalculateRailBuildCost(route, specialKind), ModernAccountGenre.Railway, $"Built {KindNameForCost(specialKind)}.");
            string kindName = specialKind == ModernSpecialRailKind.Normal ? "Rail" : $"{specialKind} rail";
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(to), $"{kindName} line changed {changed} tile(s).");
        }

        return changed;
    }

    public int AddRoadLine(TileLocation from, TileLocation to, RoadContribution contribution)
    {
        if (!CanBuildRoadLine(from, to))
        {
            return 0;
        }

        ReclaimTransportLine(from, to);
        int changed = Transport.AddRoadLine((from.H, from.V), (to.H, to.V), contribution);
        if (changed > 0)
        {
            SynchronizeTrafficVoxelsAlong(from, to);
            Publish(ModernWorldChangeKind.Transport, ToVoxelKey(to), $"Road line changed {changed} tile(s).");
        }

        return changed;
    }

    public bool RemoveTransportAt(TileLocation location)
    {
        if (RemoveRailServiceAt(location))
        {
            return true;
        }

        ModernVoxelKey key = ToVoxelKey(location);
        if (GetEntityAt(key) is { } entity && RemoveEntity(entity.EntityId))
        {
            return true;
        }

        bool hadRail = Transport.HasRail(location.H, location.V);
        TileLocation railLocation = hadRail ? location with { Z = GetRailLevel(location.H, location.V) } : location;
        ModernSpecialRailKind removedRailKind = Transport.SpecialRailTiles.GetValueOrDefault((location.H, location.V), ModernSpecialRailKind.Normal);
        if (hadRail && IsTrainOccupyingRailTile(location.H, location.V))
        {
            return false;
        }

        if (hadRail)
        {
            ApplySpecialRailRemoveEffects(railLocation, removedRailKind);
            DetachRailFromNeighbors(railLocation);
        }

        bool changed = Transport.RemoveAt(location.H, location.V);
        if (changed)
        {
            SynchronizeTrafficVoxelsAround(location.H, location.V);
            if (hadRail)
            {
                Spend(CalculateRailDestroyCost(railLocation, removedRailKind), ModernAccountGenre.Railway, $"Removed {KindNameForCost(removedRailKind)}.");
            }

            Publish(ModernWorldChangeKind.Transport, key, "Transport removed.");
        }

        return changed;
    }

    private void DetachRailFromNeighbors(TileLocation location)
    {
        ModernLocation railLocation = ToLocation(location.H, location.V, location.Z);
        foreach (ModernDirection direction in ModernDirection.All)
        {
            ModernLocation neighborLocation = railLocation + direction;
            (int h, int v) = ToHv(neighborLocation);
            if (!IsInside(h, v) || !Transport.HasRail(h, v))
            {
                continue;
            }

            byte mask = GetRawRailMask(h, v);
            byte next = (byte)(mask & ~(1 << direction.Opposite.Index));
            if (next != mask)
            {
                Transport.SetRailDirectionMask(h, v, next);
            }
        }
    }

    public bool CanBuildRailLine(TileLocation from, TileLocation to)
    {
        return CanBuildRailLine(from, to, ModernSpecialRailKind.Normal);
    }

    public bool CanBuildRailLine(TileLocation from, TileLocation to, ModernSpecialRailKind specialKind)
    {
        return TryCreateRailRoute(from, to, out IReadOnlyList<TileLocation> route)
            && CanApplyRailRoute(route, specialKind, out _);
    }

    public IReadOnlyList<TileLocation> PreviewRailLine(TileLocation from, TileLocation to)
    {
        return TryCreateRailRoute(from, to, out IReadOnlyList<TileLocation> route)
            ? route
            : Array.Empty<TileLocation>();
    }

    private bool TryCreateRailRoute(TileLocation from, TileLocation to, out IReadOnlyList<TileLocation> route)
    {
        List<TileLocation> locations = new();
        route = locations;
        if (!IsInside(from.H, from.V) || !IsInside(to.H, to.V) || from.Z < 0 || from.Z >= Depth)
        {
            return false;
        }

        ModernLocation current = ToLocation(from.H, from.V, from.Z);
        ModernLocation target = ToLocation(to.H, to.V, from.Z);
        int guard = Width * Height * 2;
        while (true)
        {
            (int h, int v) = ToHv(current);
            if (!IsInside(h, v))
            {
                return false;
            }

            locations.Add(new TileLocation(h, v, from.Z));
            if (current == target)
            {
                return true;
            }

            if (guard-- <= 0)
            {
                locations.Clear();
                return false;
            }

            current = current.Toward(target);
        }
    }

    private bool CanApplyRailRoute(
        IReadOnlyList<TileLocation> route,
        ModernSpecialRailKind specialKind,
        out IReadOnlyDictionary<(int H, int V), byte> railMasks)
    {
        Dictionary<(int H, int V), byte> masks = new();
        railMasks = masks;
        if (route.Count == 0)
        {
            return false;
        }

        if (!CanApplySpecialRailRoute(route, specialKind))
        {
            return false;
        }

        foreach (TileLocation location in route)
        {
            if (!CanBuildRailTile(location, specialKind))
            {
                return false;
            }
        }

        for (int i = 0; i < route.Count; i++)
        {
            TileLocation location = route[i];
            byte routeMask = RouteDirectionMask(route, i);
            byte existingMask = Transport.HasRail(location.H, location.V)
                ? GetRawRailMask(location.H, location.V)
                : (byte)0;
            if (!TryMergeRailMask(location, existingMask, routeMask, out byte candidate))
            {
                return false;
            }

            if (candidate == 0)
            {
                candidate = ModernRailPattern.DirectionMask(0, 4);
            }

            byte renderMask = NormalizeRailMask(candidate);
            if (CountBits(renderMask) > 3 || ModernRailPattern.FromDirectionMask(renderMask) is null)
            {
                return false;
            }

            masks[(location.H, location.V)] = candidate;
        }

        return true;
    }

    private bool CanApplySpecialRailRoute(IReadOnlyList<TileLocation> route, ModernSpecialRailKind specialKind)
    {
        if (specialKind == ModernSpecialRailKind.Normal)
        {
            return true;
        }

        if (route.Count < 2)
        {
            return false;
        }

        ModernLocation first = ToLocation(route[0].H, route[0].V, route[0].Z);
        ModernLocation second = ToLocation(route[1].H, route[1].V, route[1].Z);
        ModernDirection direction = first.GetDirectionTo(second);
        bool hasMountainCut = false;
        for (int i = 0; i < route.Count; i++)
        {
            TileLocation location = route[i];
            if (specialKind == ModernSpecialRailKind.Tunnel && !GetTerrainTile(location.H, location.V).IsFlat)
            {
                hasMountainCut = true;
            }

            if (specialKind == ModernSpecialRailKind.Bridge && !CanBuildBridgePiers(location, everyOtherTile: true, routeIndex: i))
            {
                return false;
            }

            if (specialKind == ModernSpecialRailKind.SteelSupported && !CanBuildBridgePiers(location, everyOtherTile: false, routeIndex: i))
            {
                return false;
            }

            if (Transport.HasRail(location.H, location.V))
            {
                byte existingMask = GetRawRailMask(location.H, location.V);
                if ((existingMask & (1 << direction.Index)) == 0
                    || (existingMask & (1 << direction.Opposite.Index)) == 0)
                {
                    return false;
                }
            }
        }

        return specialKind != ModernSpecialRailKind.Tunnel || hasMountainCut;
    }

    private bool TryMergeRailMask(TileLocation location, byte existingMask, byte routeMask, out byte candidate)
    {
        candidate = (byte)(existingMask | routeMask);
        if (existingMask == 0 || routeMask == 0 || (routeMask & ~existingMask) == 0)
        {
            return true;
        }

        byte newBits = (byte)(routeMask & ~existingMask);
        if (CountBits(routeMask) > 1)
        {
            return false;
        }

        if (CountBits(newBits) != 1)
        {
            return false;
        }

        int newDirection = FirstSetDirection(newBits);
        if (CountBits(existingMask) == 2 && !IsRailWellConnected(location.H, location.V, existingMask))
        {
            foreach (ModernDirection existingDirection in ModernDirection.All)
            {
                if ((existingMask & (1 << existingDirection.Index)) == 0)
                {
                    continue;
                }

                ModernDirection addedDirection = ModernDirection.FromIndex(newDirection);
                if (ModernDirection.Angle(existingDirection, addedDirection) >= 3)
                {
                    candidate = ModernRailPattern.DirectionMask(existingDirection.Index, addedDirection.Index);
                    return true;
                }
            }

            return false;
        }

        return true;
    }

    private bool IsRailWellConnected(int h, int v, byte mask)
    {
        ModernLocation location = ToLocation(h, v, GetRailLevel(h, v));
        int connected = 0;
        foreach (ModernDirection direction in ModernDirection.All)
        {
            if ((mask & (1 << direction.Index)) == 0)
            {
                continue;
            }

            ModernLocation neighbor = location + direction;
            (int neighborH, int neighborV) = ToHv(neighbor);
            if (!IsInside(neighborH, neighborV) || !Transport.HasRail(neighborH, neighborV))
            {
                continue;
            }

            byte neighborMask = GetRawRailMask(neighborH, neighborV);
            if ((neighborMask & (1 << direction.Opposite.Index)) != 0)
            {
                connected++;
            }
        }

        return connected >= 2;
    }

    private byte RouteDirectionMask(IReadOnlyList<TileLocation> route, int index)
    {
        TileLocation location = route[index];
        ModernLocation current = ToLocation(location.H, location.V, location.Z);
        byte mask = 0;
        if (index > 0)
        {
            TileLocation previous = route[index - 1];
            ModernDirection direction = current.GetDirectionTo(ToLocation(previous.H, previous.V, location.Z));
            mask |= (byte)(1 << direction.Index);
        }

        if (index < route.Count - 1)
        {
            TileLocation next = route[index + 1];
            ModernDirection direction = current.GetDirectionTo(ToLocation(next.H, next.V, location.Z));
            mask |= (byte)(1 << direction.Index);
        }

        if (mask == 0)
        {
            return ModernRailPattern.DirectionMask(0, 4);
        }

        if (CountBits(mask) == 1 && !Transport.HasRail(location.H, location.V))
        {
            int direction = FirstSetDirection(mask);
            mask |= (byte)(1 << ((direction + 4) % 8));
        }

        return mask;
    }

    private bool CanBuildRailTile(TileLocation location, ModernSpecialRailKind specialKind)
    {
        ModernVoxelKey key = ToVoxelKey(location);
        bool canReuseVoxel = IsReusable(key) || Transport.HasRail(location.H, location.V);
        ModernSpecialRailKind existingKind = Transport.SpecialRailTiles.GetValueOrDefault((location.H, location.V), ModernSpecialRailKind.Normal);
        bool specialPurposeConflict = existingKind != ModernSpecialRailKind.Normal && existingKind != specialKind;
        if (!IsInside(location.H, location.V)
            || location.Z < 0
            || location.Z >= Depth
            || !canReuseVoxel
            || Transport.HasRoad(location.H, location.V)
            || specialPurposeConflict)
        {
            return false;
        }

        TerrainTilePreview terrain = GetTerrainTile(location.H, location.V);
        bool onSurface = terrain.IsFlat && terrain.SurfaceLevel == location.Z;
        bool overWaterBridge = specialKind == ModernSpecialRailKind.Bridge
            && terrain.IsFlat
            && IsWaterSurfaceLevel(terrain.SurfaceLevel)
            && location.Z > WaterLevel;
        bool elevatedSpecialRail = specialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported
            && terrain.IsFlat
            && location.Z > terrain.SurfaceLevel;
        bool tunnelCut = specialKind == ModernSpecialRailKind.Tunnel
            && terrain.SurfaceLevel == location.Z
            && IsDrySurfaceLevel(terrain.SurfaceLevel);
        if (!onSurface && !overWaterBridge && !elevatedSpecialRail && !tunnelCut)
        {
            return false;
        }

        return specialKind switch
        {
            ModernSpecialRailKind.Bridge => IsWaterSurfaceLevel(terrain.SurfaceLevel) || location.Z > terrain.SurfaceLevel,
            ModernSpecialRailKind.SteelSupported => location.Z > terrain.SurfaceLevel
                && IsDrySurfaceLevel(terrain.SurfaceLevel),
            ModernSpecialRailKind.Tunnel => IsDrySurfaceLevel(terrain.SurfaceLevel),
            ModernSpecialRailKind.Garage => terrain.IsFlat && terrain.SurfaceLevel == location.Z,
            ModernSpecialRailKind.Unsupported => false,
            _ => IsDrySurfaceLevel(terrain.SurfaceLevel)
        };
    }

    private void ApplySpecialRailBuildEffects(TileLocation location, ModernSpecialRailKind specialKind, int routeIndex)
    {
        ModernSpecialRailKind existingKind = Transport.SpecialRailTiles.GetValueOrDefault((location.H, location.V), ModernSpecialRailKind.Normal);
        if (existingKind == ModernSpecialRailKind.Tunnel && specialKind != ModernSpecialRailKind.Tunnel)
        {
            ApplySpecialRailRemoveEffects(location, existingKind);
        }
        else if (existingKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported
            && existingKind != specialKind)
        {
            ApplySpecialRailRemoveEffects(location, existingKind);
        }

        if (specialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported)
        {
            BuildBridgePiers(location, specialKind == ModernSpecialRailKind.Bridge, routeIndex);
            return;
        }

        if (specialKind != ModernSpecialRailKind.Tunnel || tunnelTerrainBackups.ContainsKey((location.H, location.V)))
        {
            return;
        }

        TerrainTilePreview terrain = GetTerrainTile(location.H, location.V);
        if (terrain.IsFlat)
        {
            return;
        }

        tunnelTerrainBackups[(location.H, location.V)] = GetTileFineHeights(location.H, location.V);
        int flatFineHeight = terrain.SurfaceLevel * 4;
        SetTileFineHeights(location.H, location.V, flatFineHeight, flatFineHeight, flatFineHeight, flatFineHeight);
        RebuildTilesFromFineHeights();
        RebuildTrafficVoxels();
    }

    private void ApplySpecialRailRemoveEffects(TileLocation location, ModernSpecialRailKind specialKind)
    {
        if (specialKind is ModernSpecialRailKind.Bridge or ModernSpecialRailKind.SteelSupported)
        {
            RemoveBridgePiers(location);
            return;
        }

        if (specialKind != ModernSpecialRailKind.Tunnel
            || !tunnelTerrainBackups.Remove((location.H, location.V), out int[]? backup)
            || backup.Length != 4)
        {
            return;
        }

        SetTileFineHeights(location.H, location.V, backup[0], backup[1], backup[2], backup[3]);
        RebuildTilesFromFineHeights();
        RebuildTrafficVoxels();
    }

    private bool CanBuildBridgePiers(TileLocation location, bool everyOtherTile, int routeIndex = 0)
    {
        if (everyOtherTile && (routeIndex & 1) != 0)
        {
            return true;
        }

        int ground = GetGroundLevel(location.H, location.V);
        if (location.Z <= ground)
        {
            return true;
        }

        for (int z = ground; z < location.Z; z++)
        {
            ModernVoxelKey key = new(location.H, location.V, z);
            ModernVoxelOccupancy? occupancy = GetVoxel(key);
            if (occupancy is not null && !bridgePierVoxels.Contains(key))
            {
                return false;
            }
        }

        return true;
    }

    private void BuildBridgePiers(TileLocation location, bool everyOtherTile, int routeIndex = 0)
    {
        if (!CanBuildBridgePiers(location, everyOtherTile, routeIndex) || everyOtherTile && (routeIndex & 1) != 0)
        {
            return;
        }

        int ground = GetGroundLevel(location.H, location.V);
        for (int z = ground; z < location.Z; z++)
        {
            ModernVoxelKey key = new(location.H, location.V, z);
            bridgePierVoxels.Add(key);
            voxels[key] = new ModernVoxelOccupancy(key, ModernVoxelKind.Structure, $"BridgePier:{location.H}:{location.V}:{z}", null);
        }
    }

    private void RemoveBridgePiers(TileLocation location)
    {
        foreach (ModernVoxelKey key in bridgePierVoxels
            .Where(key => key.H == location.H && key.V == location.V && key.Z < location.Z)
            .ToArray())
        {
            bridgePierVoxels.Remove(key);
            if (voxels[key]?.EntityId == $"BridgePier:{key.H}:{key.V}:{key.Z}")
            {
                voxels.Remove(key);
            }
        }
    }

    private int[] GetTileFineHeights(int h, int v)
    {
        (int topX, int topY) = GetCornerVertex(h, v, TerrainCorner.Top);
        (int rightX, int rightY) = GetCornerVertex(h, v, TerrainCorner.Right);
        (int bottomX, int bottomY) = GetCornerVertex(h, v, TerrainCorner.Bottom);
        (int leftX, int leftY) = GetCornerVertex(h, v, TerrainCorner.Left);
        return
        [
            fineHeights[topX, topY],
            fineHeights[rightX, rightY],
            fineHeights[bottomX, bottomY],
            fineHeights[leftX, leftY]
        ];
    }

    private void SetTileFineHeights(int h, int v, int top, int right, int bottom, int left)
    {
        (int topX, int topY) = GetCornerVertex(h, v, TerrainCorner.Top);
        (int rightX, int rightY) = GetCornerVertex(h, v, TerrainCorner.Right);
        (int bottomX, int bottomY) = GetCornerVertex(h, v, TerrainCorner.Bottom);
        (int leftX, int leftY) = GetCornerVertex(h, v, TerrainCorner.Left);
        fineHeights[topX, topY] = Math.Clamp(top, 0, MaxFineHeight);
        fineHeights[rightX, rightY] = Math.Clamp(right, 0, MaxFineHeight);
        fineHeights[bottomX, bottomY] = Math.Clamp(bottom, 0, MaxFineHeight);
        fineHeights[leftX, leftY] = Math.Clamp(left, 0, MaxFineHeight);
    }

    private long CalculateRailBuildCost(TileLocation from, TileLocation to, ModernSpecialRailKind specialKind)
    {
        return TryCreateRailRoute(from, to, out IReadOnlyList<TileLocation> route)
            ? CalculateRailBuildCost(route, specialKind)
            : 0;
    }

    private long CalculateRailBuildCost(IEnumerable<TileLocation> route, ModernSpecialRailKind specialKind)
    {
        long unit = specialKind == ModernSpecialRailKind.Garage ? 6_500_000L : 6_000_000L;
        return route.Sum(location => unit * RailCostMultiplier(location, specialKind));
    }

    private long CalculateRailDestroyCost(TileLocation location, ModernSpecialRailKind specialKind)
    {
        long unit = specialKind == ModernSpecialRailKind.Garage ? 200_000L : 2_000_000L;
        return unit * RailCostMultiplier(location, specialKind);
    }

    private int RailCostMultiplier(TileLocation location, ModernSpecialRailKind specialKind)
    {
        if (specialKind == ModernSpecialRailKind.Tunnel)
        {
            return 2;
        }

        int height = location.Z - GetGroundLevel(location.H, location.V);
        return height < 0 ? height * -2 : height + 1;
    }

    private static string KindNameForCost(ModernSpecialRailKind specialKind)
    {
        return specialKind switch
        {
            ModernSpecialRailKind.Bridge => "bridge rail",
            ModernSpecialRailKind.SteelSupported => "steel-supported rail",
            ModernSpecialRailKind.Tunnel => "tunnel rail",
            ModernSpecialRailKind.Garage => "train garage rail",
            _ => "rail"
        };
    }

    public bool CanBuildRoadLine(TileLocation from, TileLocation to)
    {
        if (from.H != to.H && from.V != to.V)
        {
            return false;
        }

        return EnumerateLine(from, to).All(IsTransportBuildableSurface);
    }

    public IReadOnlyList<MapRailObject> CreateRailObjects()
    {
        return Transport.RailTiles
            .Select(tile => CreateRailObject(tile.H, tile.V))
            .Where(rail => rail is not null)
            .Cast<MapRailObject>()
            .ToArray();
    }

    public IReadOnlyList<ModernRailRoad> CreateRailRoads()
    {
        return Transport.RailTiles
            .Select(tile => CreateRailRoad(tile.H, tile.V))
            .Where(rail => rail is not null)
            .Cast<ModernRailRoad>()
            .ToArray();
    }

    private MapRailObject? CreateRailObject(int h, int v)
    {
        byte mask = GetLegacyRailMask(h, v);
        return ModernRailPattern.FromDirectionMask(mask) is { } pattern
            ? new MapRailObject(
                h,
                v,
                GetRailLevel(h, v),
                pattern,
                Transport.SpecialRailTiles.GetValueOrDefault((h, v), ModernSpecialRailKind.Normal))
            : null;
    }

    private ModernRailRoad? CreateRailRoad(int h, int v)
    {
        byte mask = GetLegacyRailMask(h, v);
        RailPatternDefinition? pattern = ModernRailPattern.FromDirectionMask(mask);
        ModernRailRoadKind kind = ModernRailPattern.KindFromDirectionMask(mask);
        return kind == ModernRailRoadKind.Unsupported
            ? null
            : new ModernRailRoad(new ModernVoxelKey(h, v, GetRailLevel(h, v)), mask, kind, pattern);
    }

    public IReadOnlyList<MapRoadObject> CreateRoadObjects()
    {
        return Transport.CreateRoadObjects();
    }

    public IReadOnlyList<ModernRoadSegment> CreateRoadSegments()
    {
        return Transport.CreateRoadSegments(GetGroundLevel);
    }

    private bool TryStepOnRail(ModernVoxelKey location, ModernDirection direction, out ModernVoxelKey next)
    {
        ModernLocation current = ToLocation(location);
        ModernLocation nextLocation = current + direction;
        (int h, int v) = ToHv(nextLocation);
        next = new ModernVoxelKey(h, v, IsInside(h, v) ? GetRailLevel(h, v) : location.Z);
        if (!IsInside(h, v) || !Transport.HasRail(location.H, location.V) || !Transport.HasRail(h, v))
        {
            return false;
        }

        byte currentMask = GetRawRailMask(location.H, location.V);
        byte nextMask = GetRawRailMask(h, v);
        return (currentMask & (1 << direction.Index)) != 0
            && (nextMask & (1 << direction.Opposite.Index)) != 0;
    }

    private byte GetLegacyRailMask(int h, int v)
    {
        if (!IsInside(h, v) || !Transport.HasRail(h, v))
        {
            return 0;
        }

        return NormalizeRailMask(GetRawRailMask(h, v));
    }

    private byte GetRawRailMask(int h, int v)
    {
        if (Transport.RailDirectionMasks.TryGetValue((h, v), out byte explicitMask))
        {
            return explicitMask;
        }

        ModernLocation location = ToLocation(h, v, GetRailLevel(h, v));
        byte mask = 0;
        foreach (ModernDirection direction in ModernDirection.All)
        {
            ModernLocation neighbor = location + direction;
            (int neighborH, int neighborV) = ToHv(neighbor);
            if (IsInside(neighborH, neighborV) && Transport.HasRail(neighborH, neighborV))
            {
                mask |= (byte)(1 << direction.Index);
            }
        }

        return mask;
    }

    private static byte NormalizeRailMask(byte mask)
    {
        if (mask == 0)
        {
            mask = ModernRailPattern.DirectionMask(0, 4);
        }

        if (CountBits(mask) == 1)
        {
            int direction = FirstSetDirection(mask);
            mask |= (byte)(1 << ((direction + 4) % 8));
        }
        else if (CountBits(mask) > 3)
        {
            mask = KeepFirstDirections(mask, 3);
        }

        return mask;
    }

    private void ReclaimTransportLine(TileLocation from, TileLocation to)
    {
        foreach (TileLocation location in EnumerateLine(from, to))
        {
            RemoveReusableEntityAt(ToVoxelKey(location));
        }
    }

    private void ReclaimTransportRoute(IEnumerable<TileLocation> route)
    {
        foreach (TileLocation location in route)
        {
            RemoveReusableEntityAt(ToVoxelKey(location));
        }
    }

    private static IEnumerable<TileLocation> EnumerateLine(TileLocation from, TileLocation to)
    {
        int h = from.H;
        int v = from.V;
        while (true)
        {
            yield return new TileLocation(h, v, from.Z);
            if (h == to.H && v == to.V)
            {
                yield break;
            }

            h += Math.Sign(to.H - h);
            v += Math.Sign(to.V - v);
        }
    }

}
