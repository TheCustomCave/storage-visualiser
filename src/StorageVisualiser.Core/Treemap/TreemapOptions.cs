namespace StorageVisualiser.Core.Treemap;

public enum TreemapBias : byte
{
    Equal = 0,
    Horizontal = 1,
    Vertical = 2
}

public sealed record TreemapOptions
{
    public double MinItemFraction { get; init; } = 0.003; // 0.3%
    public double MinPixelDimension { get; init; } = 4.0;
    public double FolderHeaderHeight { get; init; } = 18.0;
    public double BorderPadding { get; init; } = 2.0;
    public bool UseAllocatedSize { get; init; }
    public TreemapBias Bias { get; init; } = TreemapBias.Equal;
    public int MaxDepth { get; init; } = 10;
}
