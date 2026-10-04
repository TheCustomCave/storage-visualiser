using System;
using System.IO;
using Shouldly;
using StorageVisualiser.Core.Actions;
using StorageVisualiser.Core.Model;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class FileActionServiceTests : IDisposable
{
    private readonly string _tempLogFile;
    private readonly FakeRecycleBinProvider _fakeProvider;

    public FileActionServiceTests()
    {
        _tempLogFile = Path.Combine(Path.GetTempPath(), "storage_vis_test_action_" + Guid.NewGuid().ToString("N") + ".log");
        _fakeProvider = new FakeRecycleBinProvider();
    }

    public void Dispose()
    {
        if (File.Exists(_tempLogFile))
        {
            try { File.Delete(_tempLogFile); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CanDelete_RejectsNullAndDriveRoots()
    {
        var service = new FileActionService(_fakeProvider, logFilePath: _tempLogFile);

        service.CanDelete(null, out var reason1).ShouldBeFalse();
        reason1.ShouldContain("No item selected");

        var driveRoot = new StorageNode { Name = @"C:\", Kind = StorageItemKind.Directory };
        service.CanDelete(driveRoot, out var reason2).ShouldBeFalse();
        reason2.ShouldContain("root of a drive");
    }

    [Fact]
    public void CanDelete_RejectsSyntheticNodes()
    {
        var service = new FileActionService(_fakeProvider, logFilePath: _tempLogFile);
        var root = new StorageNode { Name = @"C:\", Kind = StorageItemKind.Directory };

        var freeSpace = new StorageNode { Name = "<Free Space>", Kind = StorageItemKind.DriveFreeSpace };
        root.AddChild(freeSpace);
        service.CanDelete(freeSpace, out var reason1).ShouldBeFalse();
        reason1.ShouldContain("Free space cannot be deleted");

        var other = new StorageNode { Name = "<Other (100 items)>", Kind = StorageItemKind.OtherGroup };
        root.AddChild(other);
        service.CanDelete(other, out var reason2).ShouldBeFalse();
        reason2.ShouldContain("virtual group");

        var inaccessible = new StorageNode { Name = "System Volume Information", Kind = StorageItemKind.Inaccessible };
        root.AddChild(inaccessible);
        service.CanDelete(inaccessible, out var reason3).ShouldBeFalse();
        reason3.ShouldContain("Inaccessible");
    }

    [Fact]
    public void CanDelete_RejectsProtectedSystemFiles()
    {
        var service = new FileActionService(_fakeProvider, logFilePath: _tempLogFile);
        var root = new StorageNode { Name = @"C:\", Kind = StorageItemKind.Directory };

        var pagefile = new StorageNode { Name = "pagefile.sys", Kind = StorageItemKind.File, Size = 4_000_000_000 };
        root.AddChild(pagefile);
        service.CanDelete(pagefile, out var reason).ShouldBeFalse();
        reason.ShouldContain("protected Windows system file");

        var hiberfil = new StorageNode { Name = "hiberfil.sys", Kind = StorageItemKind.File, Size = 16_000_000_000 };
        root.AddChild(hiberfil);
        service.CanDelete(hiberfil, out _).ShouldBeFalse();
    }

    [Fact]
    public void CanDelete_RejectsWindowsAndProgramFilesDirectories()
    {
        var service = new FileActionService(_fakeProvider, logFilePath: _tempLogFile);
        var root = new StorageNode { Name = @"C:\", Kind = StorageItemKind.Directory };

        var winDir = new StorageNode { Name = "Windows", Kind = StorageItemKind.Directory };
        root.AddChild(winDir);
        service.CanDelete(winDir, out var reason1).ShouldBeFalse();
        reason1.ShouldContain("protected system directory");

        var system32 = new StorageNode { Name = "System32", Kind = StorageItemKind.Directory };
        winDir.AddChild(system32);
        service.CanDelete(system32, out var reason2).ShouldBeFalse();
        reason2.ShouldContain("Windows folder are protected");
    }

    [Fact]
    public void CanDelete_AllowsNormalUserFiles()
    {
        var service = new FileActionService(_fakeProvider, logFilePath: _tempLogFile);
        var root = new StorageNode { Name = @"D:\", Kind = StorageItemKind.Directory };
        var games = new StorageNode { Name = "Games", Kind = StorageItemKind.Directory };
        root.AddChild(games);

        var file = new StorageNode { Name = "old_game.iso", Kind = StorageItemKind.File, Size = 5_000_000_000 };
        games.AddChild(file);

        service.CanDelete(file, out var reason).ShouldBeTrue();
        reason.ShouldBeEmpty();
    }

    [Fact]
    public void Policy_EnforcesDeleteDisabled()
    {
        var policy = new FileActionPolicy { Mode = DeleteMode.Disabled };
        var service = new FileActionService(_fakeProvider, policy, logFilePath: _tempLogFile);

        var root = new StorageNode { Name = @"D:\", Kind = StorageItemKind.Directory };
        var file = new StorageNode { Name = "test.txt", Kind = StorageItemKind.File, Size = 100 };
        root.AddChild(file);

        service.CanDelete(file, out var reason).ShouldBeFalse();
        reason.ShouldContain("disabled by policy");
    }

    [Fact]
    public void DeleteToRecycleBin_ExecutesAndLogsAction()
    {
        var service = new FileActionService(_fakeProvider, logFilePath: _tempLogFile);
        var root = new StorageNode { Name = @"D:\", Kind = StorageItemKind.Directory };
        var file = new StorageNode { Name = "junk.zip", Kind = StorageItemKind.File, Size = 2048 };
        root.AddChild(file);

        var result = service.DeleteToRecycleBin(file);

        result.Success.ShouldBeTrue();
        result.Path.ShouldBe(@"D:\junk.zip");
        result.Size.ShouldBe(2048);

        _fakeProvider.LastDeletedPath.ShouldBe(@"D:\junk.zip");

        File.Exists(_tempLogFile).ShouldBeTrue();
        var logContent = File.ReadAllText(_tempLogFile);
        logContent.ShouldContain(@"D:\junk.zip");
        logContent.ShouldContain("DELETE_RECYCLE_BIN_SUCCESS");
    }

    private sealed class FakeRecycleBinProvider : IRecycleBinProvider
    {
        public string? LastDeletedPath { get; private set; }

        public bool SendToRecycleBin(string path, out string? errorMessage)
        {
            errorMessage = null;
            LastDeletedPath = path;
            return true;
        }
    }
}
