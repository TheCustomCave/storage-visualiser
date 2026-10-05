using System;
using System.IO;
using System.Linq;
using Shouldly;
using StorageVisualiser.Core.Analysis;
using StorageVisualiser.Core.Model;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class StorageAnalysisEngineTests
{
    [Fact]
    public void GetTopFiles_ReturnsSortedLargestFilesWithinLimit()
    {
        var root = new StorageNode { Name = @"C:\Data", Kind = StorageItemKind.Directory, Size = 100_000 };
        var sub = new StorageNode { Name = "Sub", Kind = StorageItemKind.Directory, Size = 50_000 };
        root.AddChild(sub);

        var file1 = new StorageNode { Name = "small.txt", Kind = StorageItemKind.File, Size = 1_000 };
        var file2 = new StorageNode { Name = "huge.iso", Kind = StorageItemKind.File, Size = 50_000 };
        var file3 = new StorageNode { Name = "medium.zip", Kind = StorageItemKind.File, Size = 20_000 };
        var file4 = new StorageNode { Name = "tiny.log", Kind = StorageItemKind.File, Size = 500 };

        root.AddChild(file1);
        root.AddChild(file2);
        sub.AddChild(file3);
        sub.AddChild(file4);

        var top3 = StorageAnalysisEngine.GetTopFiles(root, limit: 3);

        top3.Count.ShouldBe(3);
        top3[0].Name.ShouldBe("huge.iso");
        top3[0].Size.ShouldBe(50_000);
        top3[0].PercentageOfTotal.ShouldBe(50.0);
        top3[0].Extension.ShouldBe(".iso");

        top3[1].Name.ShouldBe("medium.zip");
        top3[1].Size.ShouldBe(20_000);

        top3[2].Name.ShouldBe("small.txt");
        top3[2].Size.ShouldBe(1_000);
    }

    [Fact]
    public void GetFileTypeBreakdown_AggregatesAndCategorizesProperly()
    {
        var root = new StorageNode { Name = @"D:\Media", Kind = StorageItemKind.Directory, Size = 100_000 };
        var vid1 = new StorageNode { Name = "movie1.mp4", Kind = StorageItemKind.File, Size = 40_000 };
        var vid2 = new StorageNode { Name = "movie2.MKV", Kind = StorageItemKind.File, Size = 30_000 };
        var zip = new StorageNode { Name = "archive.zip", Kind = StorageItemKind.File, Size = 20_000 };
        var noExt = new StorageNode { Name = "Dockerfile", Kind = StorageItemKind.File, Size = 10_000 };

        root.AddChild(vid1);
        root.AddChild(vid2);
        root.AddChild(zip);
        root.AddChild(noExt);

        var breakdown = StorageAnalysisEngine.GetFileTypeBreakdown(root);

        breakdown.Count.ShouldBe(4);
        var mp4 = breakdown.First(b => b.Extension == ".mp4");
        mp4.Category.ShouldBe("Video");
        mp4.TotalSize.ShouldBe(40_000);
        mp4.FileCount.ShouldBe(1);
        mp4.PercentageOfTotal.ShouldBe(40.0);

        var mkv = breakdown.First(b => b.Extension == ".mkv");
        mkv.Category.ShouldBe("Video");
        mkv.TotalSize.ShouldBe(30_000);

        var none = breakdown.First(b => b.Extension == "(none)");
        none.Category.ShouldBe("No Extension");
        none.TotalSize.ShouldBe(10_000);
    }

    [Fact]
    public void StorageNode_DisplayProperties_FormatCorrectly()
    {
        var parent = new StorageNode { Name = "Parent", Kind = StorageItemKind.Directory, Size = 10_000 };
        var child = new StorageNode
        {
            Name = "Child.txt",
            Kind = StorageItemKind.File,
            Size = 2_500,
            LastModified = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)
        };
        parent.AddChild(child);

        child.PercentageOfParent.ShouldBe(25.0);
        child.FormattedPercentage.ShouldBe("25.0%");
        child.IconText.ShouldBe("📄");
        parent.IconText.ShouldBe("📁");
        child.FormattedSize.ShouldBe("2.44 KB");
        child.FormattedLastModified.ShouldStartWith("2026-10-04");
    }

    [Fact]
    public void StorageNode_DriveFreeSpace_PercentageCalculatesAgainstTotalCapacity()
    {
        var root = new StorageNode { Name = @"C:\", Kind = StorageItemKind.Directory, Size = 500_000 }; // 500 KB used
        var freeSpace = new StorageNode
        {
            Name = "<Free Space>",
            Kind = StorageItemKind.DriveFreeSpace,
            Size = 1_500_000 // 1.5 MB free -> Total capacity = 2.0 MB -> 75%
        };
        root.AddChild(freeSpace);

        freeSpace.PercentageOfParent.ShouldBe(75.0);
        freeSpace.FormattedPercentage.ShouldBe("75.0%");
        freeSpace.ProgressColor.ShouldBe("#CBD5E1");

        // When ShowFreeSpaceInTree is true, SortedChildren includes it
        StorageNode.ShowFreeSpaceInTree = true;
        root.SortedChildren.Count().ShouldBe(1);

        // When ShowFreeSpaceInTree is false, SortedChildren filters it out
        StorageNode.ShowFreeSpaceInTree = false;
        root.SortedChildren.Count().ShouldBe(0);

        // Restore default
        StorageNode.ShowFreeSpaceInTree = true;
    }

    [Fact]
    public void StorageNode_TreePercentage_Modes_CalculateCorrectly()
    {
        // 600 GB used on a 1000 GB drive
        var root = new StorageNode { Name = @"C:\", Kind = StorageItemKind.Directory, Size = 600_000_000_000L };
        var users = new StorageNode { Name = "Users", Kind = StorageItemKind.Directory, Size = 200_000_000_000L };
        var thecu = new StorageNode { Name = "thecu", Kind = StorageItemKind.Directory, Size = 100_000_000_000L };
        users.AddChild(thecu);
        root.AddChild(users);

        StorageNode.IsRootDrive = true;
        StorageNode.RootDriveCapacity = 1_000_000_000_000L;
        StorageNode.RootTotalSize = 600_000_000_000L;

        try
        {
            // Mode 1: % of Total (WinDirStat monotonic scaling)
            StorageNode.TreePercentageRelativeToTotal = true;
            root.CurrentTreePercentage.ShouldBe(60.0);
            users.CurrentTreePercentage.ShouldBe(20.0);
            thecu.CurrentTreePercentage.ShouldBe(10.0);
            (thecu.CurrentTreePercentage < users.CurrentTreePercentage).ShouldBeTrue();
            (users.CurrentTreePercentage < root.CurrentTreePercentage).ShouldBeTrue();

            root.FormattedDisplaySize.ShouldContain("used of");

            // Mode 2: % of Parent (RidNacs local relative)
            StorageNode.TreePercentageRelativeToTotal = false;
            root.CurrentTreePercentage.ShouldBe(60.0); // Drive root still reflects drive utilization
            users.CurrentTreePercentage.ShouldBe(33.3, 0.1); // 200 / 600
            thecu.CurrentTreePercentage.ShouldBe(50.0); // 100 / 200
        }
        finally
        {
            // Restore defaults
            StorageNode.IsRootDrive = false;
            StorageNode.RootDriveCapacity = 0;
            StorageNode.RootTotalSize = 0;
            StorageNode.TreePercentageRelativeToTotal = true;
        }
    }

    [Fact]
    public void TreemapLayoutEngine_OtherGroup_PopulatesChildren_AndAllowsDrillDown()
    {
        var root = new StorageNode { Name = "TestFolder", Kind = StorageItemKind.Directory, Size = 120_000_000 };
        var bigFile = new StorageNode { Name = "big.bin", Kind = StorageItemKind.File, Size = 100_000_000 };
        root.AddChild(bigFile);

        for (int i = 0; i < 20; i++)
        {
            var smallFile = new StorageNode { Name = $"small_{i}.txt", Kind = StorageItemKind.File, Size = 1_000_000 };
            root.AddChild(smallFile);
        }

        var engine = new StorageVisualiser.Core.Treemap.TreemapLayoutEngine();
        var bounds = new StorageVisualiser.Core.Treemap.LayoutRect(0, 0, 800, 600);
        var options = new StorageVisualiser.Core.Treemap.TreemapOptions
        {
            MinItemFraction = 0.05,
            MinPixelDimension = 2.0,
            MinFolderContentDimension = 10.0
        };

        var layout = engine.ComputeLayout(root, bounds, options);
        layout.ShouldNotBeNull();

        // Should find OtherGroup item in layout
        var otherItem = layout.Children.FirstOrDefault(c => c.Node.Kind == StorageItemKind.OtherGroup);
        otherItem.ShouldNotBeNull();
        otherItem.Node.Children.Count.ShouldBe(20);
        otherItem.Node.HasChildren.ShouldBeTrue();
        otherItem.Node.Parent.ShouldBe(root);

        // Ensure original smallFile parents were NOT mutated
        otherItem.Node.Children[0].Parent.ShouldBe(root);

        // Drilling down into otherItem.Node produces a valid child layout
        var drillDownLayout = engine.ComputeLayout(otherItem.Node, bounds, options);
        drillDownLayout.ShouldNotBeNull();
        drillDownLayout.Node.ShouldBe(otherItem.Node);
        drillDownLayout.Children.Count.ShouldBeGreaterThan(0);
    }
}
