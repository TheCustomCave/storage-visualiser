using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace StorageVisualiser.Core.Model;

public enum StorageItemKind : byte
{
    File = 0,
    Directory = 1,
    DriveFreeSpace = 2,
    Inaccessible = 3,
    OtherGroup = 4
}

[Flags]
public enum StorageItemAttributes : ushort
{
    None = 0,
    ReparsePoint = 1 << 0,
    CloudPlaceholder = 1 << 1,
    Compressed = 1 << 2,
    Encrypted = 1 << 3,
    Hidden = 1 << 4,
    System = 1 << 5,
    ReadOnly = 1 << 6
}

public sealed class StorageNode : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public required string Name { get; init; }
    public StorageItemKind Kind { get; init; }
    public StorageItemAttributes Attributes { get; init; }
    public long Size { get; set; }
    public long AllocatedSize { get; set; }
    public DateTimeOffset? LastModified { get; init; }
    public StorageNode? Parent { get; set; }

    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }

    private List<StorageNode>? _children;
    public List<StorageNode> Children => _children ??= [];
    public bool HasChildren => _children != null && _children.Count > 0;
    public static bool ShowFreeSpaceInTree { get; set; } = true;
    public static string? ActiveExtensionFilter { get; set; }

    public bool MatchesFilter { get; set; } = true;
    public long MatchingSize { get; set; }
    public int MatchingFileCount { get; set; }

    public static (bool Matches, long MatchingSize, int MatchingCount) UpdateFilterMatching(StorageNode node, string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
        {
            node.MatchesFilter = true;
            node.MatchingSize = node.Size;
            node.MatchingFileCount = node.FileCount;
            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    UpdateFilterMatching(child, null);
                }
            }
            return (true, node.Size, node.FileCount);
        }

        if (node.Kind == StorageItemKind.DriveFreeSpace)
        {
            node.MatchesFilter = false;
            node.MatchingSize = 0;
            node.MatchingFileCount = 0;
            return (false, 0, 0);
        }

        if (node.Kind == StorageItemKind.File)
        {
            var nodeExt = System.IO.Path.GetExtension(node.Name);
            var normalized = string.IsNullOrEmpty(nodeExt) ? "(none)" : nodeExt.ToLowerInvariant();
            var filterNorm = ext.StartsWith('.') ? ext.ToLowerInvariant() : (ext == "(none)" ? "(none)" : "." + ext.ToLowerInvariant());
            bool matches = string.Equals(normalized, filterNorm, StringComparison.OrdinalIgnoreCase);
            node.MatchesFilter = matches;
            node.MatchingSize = matches ? node.Size : 0;
            node.MatchingFileCount = matches ? 1 : 0;
            return (matches, node.MatchingSize, node.MatchingFileCount);
        }

        long sumSize = 0;
        int sumCount = 0;
        bool anyMatches = false;
        if (node.HasChildren)
        {
            foreach (var child in node.Children)
            {
                var res = UpdateFilterMatching(child, ext);
                if (res.Matches)
                {
                    anyMatches = true;
                    sumSize += res.MatchingSize;
                    sumCount += res.MatchingCount;
                }
            }
        }
        node.MatchesFilter = anyMatches;
        node.MatchingSize = sumSize;
        node.MatchingFileCount = sumCount;
        return (anyMatches, sumSize, sumCount);
    }

    public IEnumerable<StorageNode> SortedChildren
    {
        get
        {
            if (_children == null) return [];
            var items = (IEnumerable<StorageNode>)_children;
            if (!ShowFreeSpaceInTree)
            {
                items = System.Linq.Enumerable.Where(items, c => c.Kind != StorageItemKind.DriveFreeSpace);
            }
            if (!string.IsNullOrWhiteSpace(ActiveExtensionFilter))
            {
                items = System.Linq.Enumerable.Where(items, c => c.MatchesFilter);
            }
            return System.Linq.Enumerable.OrderByDescending(items, c => !string.IsNullOrWhiteSpace(ActiveExtensionFilter) ? c.MatchingSize : c.Size);
        }
    }

    public string FormattedSize => Formatting.SizeFormatter.Format(Size);

    public static bool TreePercentageRelativeToTotal { get; set; } = true;
    public static long RootTotalSize { get; set; }
    public static long RootDriveCapacity { get; set; }
    public static long RootDriveUsedBytes { get; set; }
    public static bool IsRootDrive { get; set; }

    public double PercentageOfTotal
    {
        get
        {
            if (Parent == null)
            {
                if (IsRootDrive && RootDriveCapacity > 0)
                {
                    long used = RootDriveUsedBytes > 0 ? RootDriveUsedBytes : Size;
                    return Math.Clamp((double)used / RootDriveCapacity * 100.0, 0.0, 100.0);
                }
                return 100.0;
            }

            if (Kind == StorageItemKind.DriveFreeSpace)
            {
                long capacity = (IsRootDrive && RootDriveCapacity > 0)
                    ? RootDriveCapacity
                    : (Parent != null ? Parent.Size + Size : Size);
                return capacity > 0 ? Math.Clamp((double)Size / capacity * 100.0, 0.0, 100.0) : 0.0;
            }

            long baseTotal = RootTotalSize > 0 ? RootTotalSize : (Parent?.Size ?? Size);
            if (baseTotal <= 0) return 0.0;
            return Math.Clamp((double)Size / baseTotal * 100.0, 0.0, 100.0);
        }
    }

    public double PercentageOfParent
    {
        get
        {
            if (Parent == null)
            {
                if (IsRootDrive && RootDriveCapacity > 0)
                {
                    long used = RootDriveUsedBytes > 0 ? RootDriveUsedBytes : Size;
                    return Math.Clamp((double)used / RootDriveCapacity * 100.0, 0.0, 100.0);
                }
                return 100.0;
            }

            if (Kind == StorageItemKind.DriveFreeSpace)
            {
                long total = (IsRootDrive && RootDriveCapacity > 0)
                    ? RootDriveCapacity
                    : (Parent.Size + Size);
                return total > 0 ? Math.Clamp((double)Size / total * 100.0, 0.0, 100.0) : 0.0;
            }
            return Parent.Size > 0 ? Math.Clamp((double)Size / Parent.Size * 100.0, 0.0, 100.0) : 100.0;
        }
    }

    public double CurrentTreePercentage => TreePercentageRelativeToTotal ? PercentageOfTotal : PercentageOfParent;

    public string FormattedPercentage => $"{CurrentTreePercentage:F1}%";

    public string FormattedDisplaySize
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ActiveExtensionFilter) && Kind == StorageItemKind.Directory)
            {
                return $"{Formatting.SizeFormatter.Format(MatchingSize)} ({MatchingFileCount:N0} files matching {ActiveExtensionFilter})";
            }

            if (Parent == null && IsRootDrive && RootDriveCapacity > 0)
            {
                return $"{FormattedSize} used of {Formatting.SizeFormatter.Format(RootDriveCapacity)}";
            }
            return FormattedSize;
        }
    }

    public string TreePercentageTooltip
    {
        get
        {
            if (Parent == null && IsRootDrive && RootDriveCapacity > 0)
            {
                return $"{FormattedDisplaySize} | {PercentageOfTotal:F1}% drive utilization";
            }

            if (Kind == StorageItemKind.DriveFreeSpace)
            {
                return $"{FormattedSize} free | {PercentageOfTotal:F1}% of drive capacity";
            }

            var totalStr = $"{PercentageOfTotal:F1}% of total scanned";
            var parentStr = Parent != null ? $"{PercentageOfParent:F1}% of parent" : null;
            return parentStr != null ? $"{FormattedSize} | {totalStr} ({parentStr})" : $"{FormattedDisplaySize} | {totalStr}";
        }
    }

    public void NotifyPercentageChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTreePercentage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormattedPercentage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TreePercentageTooltip)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormattedDisplaySize)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SortedChildren)));
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.NotifyPercentageChanged();
            }
        }
    }

    public void NotifyFormattingChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormattedSize)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormattedDisplaySize)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TreePercentageTooltip)));
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.NotifyFormattingChanged();
            }
        }
    }
    public string FormattedLastModified => LastModified?.LocalDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
    public string ProgressColor => Kind == StorageItemKind.DriveFreeSpace ? "#CBD5E1" : "#93C5FD";
    public string IconText => Kind switch
    {
        StorageItemKind.Directory => "📁",
        StorageItemKind.DriveFreeSpace => "💾",
        StorageItemKind.Inaccessible => "🔒",
        _ => "📄"
    };

    public void AddChild(StorageNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public string GetFullPath()
    {
        if (Parent == null)
        {
            return Name;
        }

        var stack = new Stack<string>();
        var current = this;
        while (current != null)
        {
            stack.Push(current.Name);
            current = current.Parent;
        }

        var root = stack.Pop();
        if (root.EndsWith('\\') || root.EndsWith('/'))
        {
            return root + string.Join('\\', stack);
        }

        return root + "\\" + string.Join('\\', stack);
    }
}
