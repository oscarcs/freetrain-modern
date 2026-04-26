namespace FreeTrain.Modern;

public readonly record struct TileLocation(int H, int V, int Z, TerrainCorner Corner = TerrainCorner.Top);

public enum TerrainCorner
{
    Top,
    Right,
    Bottom,
    Left
}

public readonly record struct TerrainTilePreview(int BaseLevel, int SurfaceLevel, int Top, int Right, int Bottom, int Left)
{
    public bool IsFlat => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;
    public int MaxCornerHeight => Math.Max(Math.Max(Top, Right), Math.Max(Bottom, Left));
    public bool HasMountain => !IsFlat;
}
