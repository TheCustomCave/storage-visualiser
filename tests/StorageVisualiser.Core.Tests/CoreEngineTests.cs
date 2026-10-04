using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Core.Scanning;
using StorageVisualiser.Core.Treemap;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class StorageModelTests
{
    [Fact]
    public void StorageNode_GetFullPath_ResolvesProperly()
    {
        var root = new StorageNode { Name = @"C:\", Kind = StorageItemKind.Directory };
        var users = new StorageNode { Name = "Users", Kind = StorageItemKind.Directory };
        var alice = new StorageNode { Name = "Alice", Kind = StorageItemKind.Directory };
        var file = new StorageNode { Name = "document.pdf", Kind = StorageItemKind.File, Size = 1024 };

        root.AddChild(users);
        users.AddChild(alice);
        alice.AddChild(file);

        file.GetFullPath().ShouldBe(@"C:\Users\Alice\document.pdf");
        alice.GetFullPath().ShouldBe(@"C:\Users\Alice");
        root.GetFullPath().ShouldBe(@"C:\");
    }
}

public sealed class DirectoryWalkerTests : IDisposable
{
    private readonly string _testDir;

    public DirectoryWalkerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "StorageVisTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        Directory.CreateDirectory(Path.Combine(_testDir, "FolderA"));
        Directory.CreateDirectory(Path.Combine(_testDir, "FolderB"));
        File.WriteAllBytes(Path.Combine(_testDir, "rootfile.bin"), new byte[5000]);
        File.WriteAllBytes(Path.Combine(_testDir, "FolderA", "fileA1.bin"), new byte[3000]);
        File.WriteAllBytes(Path.Combine(_testDir, "FolderA", "fileA2.bin"), new byte[2000]);
        File.WriteAllBytes(Path.Combine(_testDir, "FolderB", "fileB1.bin"), new byte[1000]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DirectoryWalker_ScansHierarchyAndAggregatesSizes()
    {
        var scanner = new DirectoryWalkerScanner();
        var target = new ScanTarget
        {
            RootPath = _testDir,
            IsDriveRoot = false
        };

        var root = await scanner.ScanAsync(target, null, CancellationToken.None);

        root.ShouldNotBeNull();
        root.Size.ShouldBe(11000); // 5000 + 3000 + 2000 + 1000
        root.FileCount.ShouldBe(4);
        root.DirectoryCount.ShouldBe(2);

        root.Children.Count.ShouldBe(3); // FolderA, FolderB, rootfile.bin
    }

    [Fact]
    public async Task DirectoryWalker_DriveRoot_AppendsFreeSpaceBlock()
    {
        var scanner = new DirectoryWalkerScanner();
        var target = new ScanTarget
        {
            RootPath = _testDir,
            IsDriveRoot = true,
            FreeSizeBytes = 50_000_000
        };

        var root = await scanner.ScanAsync(target, null, CancellationToken.None);

        var freeSpace = root.Children.Find(c => c.Kind == StorageItemKind.DriveFreeSpace);
        freeSpace.ShouldNotBeNull();
        freeSpace.Size.ShouldBe(50_000_000);
    }
}

public class TreemapLayoutEngineTests
{
    [Fact]
    public void Treemap_Layout_ConstrainsChildrenWithinBounds()
    {
        var root = new StorageNode { Name = "Root", Kind = StorageItemKind.Directory, Size = 1000 };
        root.AddChild(new StorageNode { Name = "A.bin", Kind = StorageItemKind.File, Size = 600 });
        root.AddChild(new StorageNode { Name = "B.bin", Kind = StorageItemKind.File, Size = 300 });
        root.AddChild(new StorageNode { Name = "C.bin", Kind = StorageItemKind.File, Size = 100 });

        var engine = new TreemapLayoutEngine();
        var bounds = new LayoutRect(0, 0, 800, 600);
        var layout = engine.ComputeLayout(root, bounds, new TreemapOptions());

        layout.Bounds.ShouldBe(bounds);
        layout.Children.Count.ShouldBe(3);

        foreach (var child in layout.Children)
        {
            child.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
            child.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0);
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(800.01);
            child.Bounds.Bottom.ShouldBeLessThanOrEqualTo(600.01);
            child.Bounds.Area.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void Treemap_Layout_GroupsSmallItemsIntoOther()
    {
        var root = new StorageNode { Name = "Root", Kind = StorageItemKind.Directory, Size = 10000 };
        root.AddChild(new StorageNode { Name = "Big.bin", Kind = StorageItemKind.File, Size = 9800 });

        // Add 20 tiny files (each 10 bytes = 0.1% < 0.3% threshold)
        for (int i = 0; i < 20; i++)
        {
            root.AddChild(new StorageNode { Name = $"Tiny_{i}.bin", Kind = StorageItemKind.File, Size = 10 });
        }

        var engine = new TreemapLayoutEngine();
        var layout = engine.ComputeLayout(root, new LayoutRect(0, 0, 1000, 1000), new TreemapOptions
        {
            MinItemFraction = 0.01 // 1% threshold
        });

        // Should have "Big.bin" and one grouped "Other" block
        layout.Children.Count.ShouldBe(2);
        var otherItem = layout.Children.Find(c => c.IsGroupedOther);
        otherItem.ShouldNotBeNull();
        otherItem.GroupedItemCount.ShouldBe(20);
        otherItem.EffectiveSize.ShouldBe(200);
    }

    [Fact]
    public void Treemap_Layout_SpaceMongerNestedFolder_HasHeaderBar()
    {
        var root = new StorageNode { Name = "Drive", Kind = StorageItemKind.Directory, Size = 2000 };
        var subFolder = new StorageNode { Name = "SubFolder", Kind = StorageItemKind.Directory, Size = 1500 };
        subFolder.AddChild(new StorageNode { Name = "DeepFile.bin", Kind = StorageItemKind.File, Size = 1500 });
        root.AddChild(subFolder);

        var engine = new TreemapLayoutEngine();
        var layout = engine.ComputeLayout(root, new LayoutRect(0, 0, 800, 600), new TreemapOptions
        {
            FolderHeaderHeight = 20.0
        });

        var subFolderItem = layout.Children.Find(c => c.Node.Name == "SubFolder");
        subFolderItem.ShouldNotBeNull();
        subFolderItem.HeaderBounds.Height.ShouldBe(20.0);
        subFolderItem.ContentBounds.Height.ShouldBeLessThan(subFolderItem.Bounds.Height);
        subFolderItem.Children.Count.ShouldBe(1);
        subFolderItem.Children[0].Node.Name.ShouldBe("DeepFile.bin");
    }
}
