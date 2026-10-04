using StorageVisualiser.Core.Formatting;

namespace StorageVisualiser.Core.Analysis;

public sealed record FileTypeSummary
{
    public required string Extension { get; init; }
    public required string Category { get; init; }
    public long TotalSize { get; init; }
    public int FileCount { get; init; }
    public double PercentageOfTotal { get; init; }

    public string FormattedTotalSize => SizeFormatter.Format(TotalSize);
    public string FormattedPercentage => $"{PercentageOfTotal:F2}%";
    public string FormattedFileCount => $"{FileCount:N0}";
}
