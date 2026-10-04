using System;
using System.Collections.Generic;
using System.Linq;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Treemap;

public sealed class TreemapLayoutEngine
{
    public TreemapItem ComputeLayout(StorageNode rootNode, LayoutRect bounds, TreemapOptions options)
    {
        var rootItem = new TreemapItem
        {
            Node = rootNode,
            Bounds = bounds,
            Depth = 0,
            EffectiveSize = GetNodeSize(rootNode, options.UseAllocatedSize)
        };

        LayoutFolderContent(rootItem, bounds, 0, options);
        return rootItem;
    }

    private static long GetNodeSize(StorageNode node, bool useAllocated) =>
        useAllocated ? node.AllocatedSize : node.Size;

    private void LayoutFolderContent(TreemapItem folderItem, LayoutRect availableBounds, int depth, TreemapOptions options)
    {
        if (depth >= options.MaxDepth || availableBounds.Width < options.MinPixelDimension || availableBounds.Height < options.MinPixelDimension)
        {
            return;
        }

        var node = folderItem.Node;
        if (!node.HasChildren)
        {
            return;
        }

        // SpaceMonger style: If not the root view or if folder has depth > 0, allocate header bar
        LayoutRect contentRect;
        if (depth > 0)
        {
            var headerHeight = Math.Min(options.FolderHeaderHeight, Math.Max(0, availableBounds.Height - 4));
            folderItem.HeaderBounds = new LayoutRect(availableBounds.X, availableBounds.Y, availableBounds.Width, headerHeight);

            var remainingHeight = availableBounds.Height - headerHeight - options.BorderPadding * 2;
            var remainingWidth = availableBounds.Width - options.BorderPadding * 2;

            if (remainingHeight < options.MinPixelDimension || remainingWidth < options.MinPixelDimension)
            {
                // Too small to show interior children
                folderItem.ContentBounds = LayoutRect.Empty;
                return;
            }

            contentRect = new LayoutRect(
                availableBounds.X + options.BorderPadding,
                availableBounds.Y + headerHeight + options.BorderPadding,
                remainingWidth,
                remainingHeight);
        }
        else
        {
            folderItem.HeaderBounds = LayoutRect.Empty;
            contentRect = availableBounds.Deflate(options.BorderPadding);
        }

        folderItem.ContentBounds = contentRect;

        if (contentRect.Area <= 0)
        {
            return;
        }

        // Collect and filter children
        var rawChildren = node.Children;
        var totalChildSize = rawChildren.Sum(c => Math.Max(0, GetNodeSize(c, options.UseAllocatedSize)));
        if (totalChildSize <= 0)
        {
            return;
        }

        var threshold = totalChildSize * options.MinItemFraction;
        var significantItems = new List<(StorageNode Node, long Size)>();
        long otherTotalSize = 0;
        int otherCount = 0;

        foreach (var child in rawChildren)
        {
            var size = Math.Max(0, GetNodeSize(child, options.UseAllocatedSize));
            // Always keep DriveFreeSpace visible
            if (child.Kind == StorageItemKind.DriveFreeSpace || size >= threshold)
            {
                significantItems.Add((child, size));
            }
            else
            {
                otherTotalSize += size;
                otherCount++;
            }
        }

        // Sort descending by size
        significantItems.Sort((a, b) => b.Size.CompareTo(a.Size));

        var layoutElements = new List<(StorageNode? Node, long Size, bool IsOther, int Count)>();
        foreach (var item in significantItems)
        {
            layoutElements.Add((item.Node, item.Size, false, 1));
        }

        if (otherTotalSize > 0)
        {
            layoutElements.Add((null, otherTotalSize, true, otherCount));
        }

        // Squarified layout algorithm
        var remainingRect = contentRect;
        var remainingElements = layoutElements.ToList();
        var currentWeightSum = remainingElements.Sum(e => e.Size);

        while (remainingElements.Count > 0 && remainingRect.Width >= 1 && remainingRect.Height >= 1)
        {
            var isHorizontal = options.Bias switch
            {
                TreemapBias.Horizontal => true,
                TreemapBias.Vertical => false,
                _ => remainingRect.Width >= remainingRect.Height
            };

            var shorterEdge = isHorizontal ? remainingRect.Height : remainingRect.Width;
            if (shorterEdge <= 0) break;

            var row = new List<(StorageNode? Node, long Size, bool IsOther, int Count)>();
            double rowTotalWeight = 0;
            double bestWorstRatio = double.MaxValue;

            while (remainingElements.Count > 0)
            {
                var candidate = remainingElements[0];
                var testRow = new List<(StorageNode? Node, long Size, bool IsOther, int Count)>(row) { candidate };
                var testWeight = rowTotalWeight + candidate.Size;

                var worstRatio = ComputeWorstAspectRatio(testRow, testWeight, currentWeightSum, shorterEdge, remainingRect.Area);

                if (row.Count > 0 && worstRatio > bestWorstRatio)
                {
                    // Adding this item made aspect ratios worse; lock the row
                    break;
                }

                row.Add(candidate);
                rowTotalWeight = testWeight;
                bestWorstRatio = worstRatio;
                remainingElements.RemoveAt(0);
            }

            if (row.Count == 0 && remainingElements.Count > 0)
            {
                // Force at least one item
                row.Add(remainingElements[0]);
                rowTotalWeight = remainingElements[0].Size;
                remainingElements.RemoveAt(0);
            }

            // Lay out the row inside remainingRect along shorterEdge
            var rowAreaFraction = currentWeightSum > 0 ? rowTotalWeight / currentWeightSum : 0;
            var rowArea = remainingRect.Area * rowAreaFraction;
            var rowThickness = shorterEdge > 0 ? rowArea / shorterEdge : 0;

            if (isHorizontal)
            {
                var rowRect = new LayoutRect(remainingRect.X, remainingRect.Y, rowThickness, remainingRect.Height);
                var curY = rowRect.Y;

                foreach (var el in row)
                {
                    var itemFraction = rowTotalWeight > 0 ? (double)el.Size / rowTotalWeight : 0;
                    var itemHeight = rowRect.Height * itemFraction;
                    var itemRect = new LayoutRect(rowRect.X, curY, rowRect.Width, itemHeight);
                    curY += itemHeight;

                    CreateTreemapItem(folderItem, el, itemRect, depth, options);
                }

                remainingRect = new LayoutRect(
                    remainingRect.X + rowThickness,
                    remainingRect.Y,
                    Math.Max(0, remainingRect.Width - rowThickness),
                    remainingRect.Height);
            }
            else
            {
                var rowRect = new LayoutRect(remainingRect.X, remainingRect.Y, remainingRect.Width, rowThickness);
                var curX = rowRect.X;

                foreach (var el in row)
                {
                    var itemFraction = rowTotalWeight > 0 ? (double)el.Size / rowTotalWeight : 0;
                    var itemWidth = rowRect.Width * itemFraction;
                    var itemRect = new LayoutRect(curX, rowRect.Y, itemWidth, rowRect.Height);
                    curX += itemWidth;

                    CreateTreemapItem(folderItem, el, itemRect, depth, options);
                }

                remainingRect = new LayoutRect(
                    remainingRect.X,
                    remainingRect.Y + rowThickness,
                    remainingRect.Width,
                    Math.Max(0, remainingRect.Height - rowThickness));
            }

            currentWeightSum -= (long)rowTotalWeight;
        }
    }

    private void CreateTreemapItem(
        TreemapItem parentFolder,
        (StorageNode? Node, long Size, bool IsOther, int Count) element,
        LayoutRect itemRect,
        int depth,
        TreemapOptions options)
    {
        if (element.IsOther)
        {
            var otherNode = new StorageNode
            {
                Name = $"<Other ({element.Count:N0} items)>",
                Kind = StorageItemKind.OtherGroup,
                Size = element.Size,
                AllocatedSize = element.Size
            };

            var otherItem = new TreemapItem
            {
                Node = otherNode,
                Bounds = itemRect,
                Depth = depth + 1,
                IsGroupedOther = true,
                GroupedItemCount = element.Count,
                EffectiveSize = element.Size
            };
            parentFolder.Children.Add(otherItem);
        }
        else if (element.Node != null)
        {
            var childItem = new TreemapItem
            {
                Node = element.Node,
                Bounds = itemRect,
                Depth = depth + 1,
                EffectiveSize = element.Size
            };
            parentFolder.Children.Add(childItem);

            if (element.Node.Kind == StorageItemKind.Directory && element.Node.HasChildren)
            {
                LayoutFolderContent(childItem, itemRect, depth + 1, options);
            }
        }
    }

    private static double ComputeWorstAspectRatio(
        List<(StorageNode? Node, long Size, bool IsOther, int Count)> row,
        double rowTotalWeight,
        double totalWeight,
        double shorterEdge,
        double totalArea)
    {
        if (rowTotalWeight <= 0 || totalWeight <= 0 || shorterEdge <= 0) return double.MaxValue;

        var rowArea = totalArea * (rowTotalWeight / totalWeight);
        var rowThickness = rowArea / shorterEdge;
        if (rowThickness <= 0) return double.MaxValue;

        double maxRatio = 0;
        foreach (var item in row)
        {
            var itemArea = rowArea * ((double)item.Size / rowTotalWeight);
            var itemLength = itemArea / rowThickness;
            if (itemLength <= 0) return double.MaxValue;

            var ratio = Math.Max(rowThickness / itemLength, itemLength / rowThickness);
            if (ratio > maxRatio)
            {
                maxRatio = ratio;
            }
        }

        return maxRatio;
    }
}
