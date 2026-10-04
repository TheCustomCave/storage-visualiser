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
