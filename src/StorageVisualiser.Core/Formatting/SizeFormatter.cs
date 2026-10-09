using System;

namespace StorageVisualiser.Core.Formatting;

public enum UnitSystem : byte
{
    Windows = 0, // 1024 base: B, KB, MB, GB, TB
    Iec = 1,     // 1024 base: B, KiB, MiB, GiB, TiB
    Si = 2       // 1000 base: B, kB, MB, GB, TB
}

public static class SizeFormatter
{
    private static readonly string[] WindowsUnits = ["bytes", "KB", "MB", "GB", "TB", "PB"];
    private static readonly string[] IecUnits = ["bytes", "KiB", "MiB", "GiB", "TiB", "PiB"];
    private static readonly string[] SiUnits = ["bytes", "kB", "MB", "GB", "TB", "PB"];

    public static UnitSystem DefaultUnitSystem { get; set; } = UnitSystem.Windows;

    public static string Format(long bytes, UnitSystem? system = null, bool exact = false)
    {
        var activeSystem = system ?? DefaultUnitSystem;
        if (exact)
        {
            return $"{bytes:N0} bytes";
        }

        if (bytes < 0)
        {
            return "-" + Format(-bytes, activeSystem, false);
        }

        if (bytes < 1024 && activeSystem != UnitSystem.Si)
        {
            return $"{bytes} bytes";
        }

        if (bytes < 1000 && activeSystem == UnitSystem.Si)
        {
            return $"{bytes} bytes";
        }

        double divisor = activeSystem == UnitSystem.Si ? 1000.0 : 1024.0;
        var units = activeSystem switch
        {
            UnitSystem.Iec => IecUnits,
            UnitSystem.Si => SiUnits,
            _ => WindowsUnits
        };

        double value = bytes;
        int unitIndex = 0;

        while (value >= divisor && unitIndex < units.Length - 1)
        {
            value /= divisor;
            unitIndex++;
        }

        return value >= 100 ? $"{value:F0} {units[unitIndex]}" :
               value >= 10 ? $"{value:F1} {units[unitIndex]}" :
                             $"{value:F2} {units[unitIndex]}";
    }
}
