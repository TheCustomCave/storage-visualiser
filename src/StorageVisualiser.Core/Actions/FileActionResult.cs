using System;

namespace StorageVisualiser.Core.Actions;

public interface IRecycleBinProvider
{
    bool SendToRecycleBin(string path, out string? errorMessage);
}

public sealed record FileActionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public required string Path { get; init; }
    public long Size { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
