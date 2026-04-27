namespace FreeTrain.Modern;

public enum ModernEntityKind
{
    Land,
    Structure
}

public static class ModernEntityKindExtensions
{
    public static ModernVoxelKind ToVoxelKind(this ModernEntityKind kind)
    {
        return kind switch
        {
            ModernEntityKind.Land => ModernVoxelKind.Land,
            ModernEntityKind.Structure => ModernVoxelKind.Structure,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported entity kind.")
        };
    }
}

public sealed record ModernPlacedEntity(
    string EntityId,
    ModernEntityKind Kind,
    int H,
    int V,
    int Z,
    int FootprintH,
    int FootprintV,
    int FootprintZ,
    string ContributionId,
    string? ResolvedContributionId,
    LandContribution? LandContribution,
    LandContribution? ResolvedStaticLand,
    SpriteContribution? StructureContribution,
    SpriteFrame? StructureFrame,
    bool IsOwned,
    bool IsSilentlyReclaimable,
    long EntityValue)
{
    public event EventHandler? Removed;

    public IEnumerable<ModernVoxelKey> OccupiedVoxels
    {
        get
        {
            for (int z = 0; z < Math.Max(1, FootprintZ); z++)
            {
                for (int v = 0; v < Math.Max(1, FootprintV); v++)
                {
                    for (int h = 0; h < Math.Max(1, FootprintH); h++)
                    {
                        yield return new ModernVoxelKey(H + h, V + v, Z + z);
                    }
                }
            }
        }
    }

    public static ModernPlacedEntity Land(
        int h,
        int v,
        int z,
        LandContribution contribution,
        LandContribution? resolvedStaticLand = null)
    {
        return new ModernPlacedEntity(
            CreateEntityId(ModernEntityKind.Land, h, v, contribution.Id, resolvedStaticLand?.Id),
            ModernEntityKind.Land,
            h,
            v,
            z,
            1,
            1,
            1,
            contribution.Id,
            resolvedStaticLand?.Id,
            contribution,
            resolvedStaticLand,
            null,
            null,
            false,
            true,
            0);
    }

    public static ModernPlacedEntity Structure(
        int h,
        int v,
        int z,
        SpriteContribution contribution,
        SpriteFrame frame)
    {
        int footprintH = Math.Max(1, contribution.SpriteSet3D?.SizeX ?? contribution.SizeX);
        int footprintV = Math.Max(1, contribution.SpriteSet3D?.SizeY ?? contribution.SizeY);
        int footprintZ = Math.Max(1, contribution.SpriteSet3D?.SizeZ ?? contribution.Height);

        return new ModernPlacedEntity(
            CreateEntityId(ModernEntityKind.Structure, h, v, contribution.Id, null),
            ModernEntityKind.Structure,
            h,
            v,
            z,
            footprintH,
            footprintV,
            footprintZ,
            contribution.Id,
            null,
            null,
            null,
            contribution,
            frame,
            true,
            false,
            EstimateStructureValue(contribution));
    }

    internal void PublishRemoved()
    {
        Removed?.Invoke(this, EventArgs.Empty);
    }

    private static string CreateEntityId(ModernEntityKind kind, int h, int v, string contributionId, string? resolvedContributionId)
    {
        string resolved = string.IsNullOrWhiteSpace(resolvedContributionId) ? "" : $":{resolvedContributionId}";
        return $"{kind}:{h}:{v}:{contributionId}{resolved}";
    }

    private static long EstimateStructureValue(SpriteContribution contribution)
    {
        int footprint = Math.Max(1, contribution.SizeX) * Math.Max(1, contribution.SizeY);
        int height = Math.Max(1, contribution.Height);
        return footprint * height * 1_000_000L;
    }
}
