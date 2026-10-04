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
}
