using System.Collections.Generic;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Treemap;

public sealed class TreemapItem
{
    public required StorageNode Node { get; init; }
    public required LayoutRect Bounds { get; set; }
    public LayoutRect HeaderBounds { get; set; }
    public LayoutRect ContentBounds { get; set; }
    public int Depth { get; init; }
    public bool IsGroupedOther { get; init; }
    public int GroupedItemCount { get; init; }
    public long EffectiveSize { get; init; }

    private List<TreemapItem>? _children;
    public List<TreemapItem> Children => _children ??= [];
    public bool HasChildren => _children != null && _children.Count > 0;

    public TreemapItem? HitTest(double x, double y)
    {
        if (!Bounds.Contains(x, y)) return null;

        if (_children != null)
        {
            // Search top-most children first (reverse order)
            for (var i = _children.Count - 1; i >= 0; i--)
            {
                var hit = _children[i].HitTest(x, y);
                if (hit != null) return hit;
            }
        }

        return this;
    }
}
