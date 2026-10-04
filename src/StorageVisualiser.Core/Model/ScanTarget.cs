using System.IO;

namespace StorageVisualiser.Core.Model;

public sealed record ScanTarget
{
    public required string RootPath { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public DriveType DriveType { get; init; } = DriveType.Unknown;
    public string FileSystemName { get; init; } = string.Empty;
    public long TotalSizeBytes { get; init; }
    public long FreeSizeBytes { get; init; }
    public bool IsNetwork => DriveType == DriveType.Network || RootPath.StartsWith(@"\\", StringComparison.Ordinal);
    public bool IsDriveRoot { get; init; }
}
