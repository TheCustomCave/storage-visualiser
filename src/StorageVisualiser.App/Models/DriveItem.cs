using System;
using System.IO;
using StorageVisualiser.Core.Formatting;

namespace StorageVisualiser.App.Models;

public sealed record DriveItem(
    string Name,
    string VolumeLabel,
    DriveType DriveType,
    long TotalBytes,
    long FreeBytes)
{
    public string DisplayText =>
        string.IsNullOrWhiteSpace(VolumeLabel)
            ? $"{Name} [{DriveType}] ({SizeFormatter.Format(FreeBytes)} free of {SizeFormatter.Format(TotalBytes)})"
            : $"{Name} ({VolumeLabel}) [{DriveType}] ({SizeFormatter.Format(FreeBytes)} free of {SizeFormatter.Format(TotalBytes)})";
}
