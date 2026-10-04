using System;
using System.Collections.Generic;

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

public sealed class StorageNode
{
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

    public IEnumerable<StorageNode> SortedChildren
    {
        get
        {
            if (_children == null) return [];
            var items = ShowFreeSpaceInTree
                ? (IEnumerable<StorageNode>)_children
                : System.Linq.Enumerable.Where(_children, c => c.Kind != StorageItemKind.DriveFreeSpace);
            return System.Linq.Enumerable.OrderByDescending(items, c => c.Size);
        }
    }

    public string FormattedSize => Formatting.SizeFormatter.Format(Size);

    public double PercentageOfParent
    {
        get
        {
            if (Parent == null) return 100.0;
            if (Kind == StorageItemKind.DriveFreeSpace)
            {
                long total = Parent.Size + Size;
                return total > 0 ? Math.Clamp((double)Size / total * 100.0, 0.0, 100.0) : 0.0;
            }
            return Parent.Size > 0 ? Math.Clamp((double)Size / Parent.Size * 100.0, 0.0, 100.0) : 100.0;
        }
    }

    public string FormattedPercentage => $"{PercentageOfParent:F1}%";
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
