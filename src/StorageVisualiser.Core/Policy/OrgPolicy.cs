using System.Text.Json.Serialization;

namespace StorageVisualiser.Core.Policy;

public enum PolicyDeleteMode
{
    Disabled,
    RecycleBinOnly,
    AllowPermanent
}

public enum ProtectedPathsMode
{
    Append,
    Replace
}

public sealed record OrgPolicy
{
    [JsonPropertyName("deleteMode")]
    public PolicyDeleteMode? DeleteMode { get; init; }

    [JsonPropertyName("protectedPaths")]
    public List<string>? ProtectedPaths { get; init; }

    [JsonPropertyName("protectedPathsMode")]
    public ProtectedPathsMode? ProtectedPathsMode { get; init; }

    [JsonPropertyName("allowSnapshots")]
    public bool? AllowSnapshots { get; init; }

    [JsonPropertyName("snapshotEncryption")]
    public string? SnapshotEncryption { get; init; }

    [JsonPropertyName("allowExports")]
    public bool? AllowExports { get; init; }

    [JsonPropertyName("redactPathsInExports")]
    public bool? RedactPathsInExports { get; init; }

    [JsonPropertyName("allowElevation")]
    public bool? AllowElevation { get; init; }

    [JsonPropertyName("allowNetworkPaths")]
    public bool? AllowNetworkPaths { get; init; }

    [JsonIgnore]
    public bool IsEnforced => DeleteMode.HasValue ||
                              ProtectedPaths != null ||
                              ProtectedPathsMode.HasValue ||
                              AllowSnapshots.HasValue ||
                              AllowExports.HasValue ||
                              RedactPathsInExports.HasValue ||
                              AllowElevation.HasValue ||
                              AllowNetworkPaths.HasValue;
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(JsonStringEnumConverter<PolicyDeleteMode>), typeof(JsonStringEnumConverter<ProtectedPathsMode>)])]
[JsonSerializable(typeof(OrgPolicy))]
public partial class PolicyJsonContext : JsonSerializerContext
{
}
