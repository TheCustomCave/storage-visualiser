using System.Text.Json.Serialization;

namespace StorageVisualiser.Cli;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CliScanResultDto))]
public sealed partial class CliJsonContext : JsonSerializerContext
{
}
