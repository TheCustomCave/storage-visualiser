namespace StorageVisualiser.Core.Treemap;

public enum TreemapBias : byte
{
    Equal = 0,
    Horizontal = 1,
    Vertical = 2
}

public sealed record TreemapOptions
{
    public double MinItemFraction { get; init; } = 0.005; // 0.5%
    public double MinPixelDimension { get; init; } = 14.0; // 14px minimum to display an item
    public double MinFolderContentDimension { get; init; } = 34.0; // 34px minimum to subdivide a folder
    public double FolderHeaderHeight { get; init; } = 16.0;
    public double BorderPadding { get; init; } = 1.5;
    public bool UseAllocatedSize { get; init; }
    public bool ShowFreeSpace { get; init; } = true;
    public TreemapBias Bias { get; init; } = TreemapBias.Equal;
    public int MaxDepth { get; init; } = 8;
}
