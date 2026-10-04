using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StorageVisualiser.Core.Export;

public sealed class ReportMetadata
{
    public string HostName { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string GeneratedAt { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public string FormattedTotalSize { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public string ScanDuration { get; set; } = string.Empty;
}

public sealed class ExportNodeDto
{
    [JsonPropertyName("n")]
    public required string Name { get; set; }

    [JsonPropertyName("s")]
    public long Size { get; set; }

    [JsonPropertyName("k")]
    public byte Kind { get; set; } // 0 = File, 1 = Directory, 2 = DriveFreeSpace, 4 = OtherGroup

    [JsonPropertyName("c")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ExportNodeDto>? Children { get; set; }
}

public sealed class ExportTopFileDto
{
    public required string Name { get; set; }
    public required string FullPath { get; set; }
    public required string Extension { get; set; }
    public long Size { get; set; }
    public required string FormattedSize { get; set; }
    public double Percentage { get; set; }
    public required string FormattedPercentage { get; set; }
    public required string Modified { get; set; }
}

public sealed class ExportFileTypeDto
{
    public required string Extension { get; set; }
    public required string Category { get; set; }
    public long TotalSize { get; set; }
    public required string FormattedTotalSize { get; set; }
    public double Percentage { get; set; }
    public required string FormattedPercentage { get; set; }
    public int FileCount { get; set; }
    public required string FormattedFileCount { get; set; }
}

public sealed class FullReportData
{
    public required ReportMetadata Metadata { get; set; }
    public required ExportNodeDto TreeRoot { get; set; }
    public required List<ExportTopFileDto> TopFiles { get; set; }
    public required List<ExportFileTypeDto> FileTypes { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FullReportData))]
[JsonSerializable(typeof(ReportMetadata))]
[JsonSerializable(typeof(ExportNodeDto))]
[JsonSerializable(typeof(ExportTopFileDto))]
[JsonSerializable(typeof(ExportFileTypeDto))]
public partial class ReportJsonContext : JsonSerializerContext
{
}
