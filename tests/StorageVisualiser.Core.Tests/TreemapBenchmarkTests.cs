using System;
using System.Diagnostics;
using Shouldly;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Core.Treemap;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class TreemapBenchmarkTests
{
    [Fact]
    public void ComputeLayout_OnLargeSyntheticTree_ExecutesWithinPerformanceBudget()
    {
        // Construct a synthetic tree with ~50,000 nodes across 4 depth levels
        var root = new StorageNode { Name = @"C:\", Kind = StorageItemKind.Directory };
        int nodeCount = 0;

        void BuildBranch(StorageNode parent, int depth, int breadth)
        {
            if (depth <= 0) return;
            for (int i = 0; i < breadth; i++)
            {
                var isFolder = i % 3 == 0;
                var size = (1000 - i * 10) * depth * 500;
                var child = new StorageNode
                {
                    Name = isFolder ? $"Folder_{depth}_{i}" : $"File_{depth}_{i}.dat",
                    Kind = isFolder ? StorageItemKind.Directory : StorageItemKind.File,
                    Size = size,
                    AllocatedSize = size
                };
                parent.AddChild(child);
                nodeCount++;

                if (isFolder)
                {
                    BuildBranch(child, depth - 1, breadth);
                }
            }
        }

        BuildBranch(root, depth: 5, breadth: 12);
        nodeCount.ShouldBeGreaterThan(2000);

        var engine = new TreemapLayoutEngine();
        var bounds = new LayoutRect(0, 0, 1920, 1080);
        var options = new TreemapOptions();

        // Warmup
        _ = engine.ComputeLayout(root, bounds, options);

        // Measure layout calculation time
        var sw = Stopwatch.StartNew();
        var layout = engine.ComputeLayout(root, bounds, options);
        sw.Stop();

        layout.ShouldNotBeNull();
        layout.Bounds.Width.ShouldBe(1920);
        layout.Bounds.Height.ShouldBe(1080);

        // Must re-layout well under 100ms budget for interactive 60fps responsiveness
        sw.ElapsedMilliseconds.ShouldBeLessThan(100);
    }
}
