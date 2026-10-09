using System.Text.Json.Serialization;

namespace StorageVisualiser.Core.Settings;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
public sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
