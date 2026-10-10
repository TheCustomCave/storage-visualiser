using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Treemap;

namespace StorageVisualiser.Core.Settings;

public sealed record AppSettings
{
    public TreemapColorMode ColorMode { get; init; } = TreemapColorMode.DepthRainbow;
    public bool ColorBlindSafe { get; init; }
    public UnitSystem UnitSystem { get; init; } = UnitSystem.Windows;
    public bool UseAllocatedSize { get; init; }
    public int DefaultDetailLevel { get; init; } = 3;
    public TreemapBias LayoutBias { get; init; } = TreemapBias.Equal;
    public bool ShowFreeSpace { get; init; } = true;
    public bool TreePercentageRelativeToTotal { get; init; } = true;
    public bool ConfirmBeforeDelete { get; init; } = true;
    public bool AutoRescanAfterDelete { get; init; } = true;
    public bool RedactPathsInExports { get; init; }
}
