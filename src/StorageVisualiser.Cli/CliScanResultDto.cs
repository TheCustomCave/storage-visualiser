using System;
using System.Collections.Generic;

namespace StorageVisualiser.Cli;

public sealed record CliScanResultDto
{
    public required string TargetPath { get; init; }
    public required string MachineName { get; init; }
    public DateTimeOffset ScanTimeUtc { get; init; }
    public long ScanDurationMs { get; init; }
    public required string ScannerUsed { get; init; }
    public long TotalFiles { get; init; }
    public long TotalDirectories { get; init; }
    public long TotalSizeBytes { get; init; }
    public required string FormattedTotalSize { get; init; }
    public long AllocatedSizeBytes { get; init; }
    public required string FormattedAllocatedSize { get; init; }
    public long? DriveCapacityBytes { get; init; }
    public long? DriveFreeBytes { get; init; }
    public double? DriveFreePercentage { get; init; }
    public List<CliTopFileDto> TopFiles { get; init; } = [];
    public List<CliFileTypeDto> FileTypeBreakdown { get; init; } = [];
    public bool ThresholdExceeded { get; init; }
    public List<string> ThresholdAlerts { get; init; } = [];
}

public sealed record CliTopFileDto
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string Extension { get; init; }
    public long SizeBytes { get; init; }
    public required string FormattedSize { get; init; }
    public double PercentageOfTotal { get; init; }
    public string? LastModified { get; init; }
}

public sealed record CliFileTypeDto
{
    public required string Extension { get; init; }
    public required string Category { get; init; }
    public int FileCount { get; init; }
    public long TotalSizeBytes { get; init; }
    public required string FormattedTotalSize { get; init; }
    public double PercentageOfTotal { get; init; }
}
