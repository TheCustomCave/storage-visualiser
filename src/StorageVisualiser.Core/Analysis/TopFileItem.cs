using System;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Analysis;

public sealed record TopFileItem
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required string Extension { get; init; }
    public long Size { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public double PercentageOfTotal { get; init; }
    public required StorageNode Node { get; init; }

    public string FormattedSize => SizeFormatter.Format(Size);
    public string FormattedPercentage => $"{PercentageOfTotal:F2}%";
    public string FormattedModified => LastModified?.LocalDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
}
