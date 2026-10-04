using System;

namespace StorageVisualiser.Core.Model;

public readonly record struct ScanProgress(
    long FilesScanned,
    long DirectoriesScanned,
    long BytesScanned,
    string CurrentDirectory,
    TimeSpan ElapsedTime);
