using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Scanning;

public sealed class DirectoryWalkerScanner : IScanner
{
    private const int RecallOnDataAccess = 0x00400000;
    private const int RecallOnOpen = 0x00040000;

    public string ScannerName => "Directory Walker";

    public async Task<StorageNode> ScanAsync(
        ScanTarget target,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var rootDir = new DirectoryInfo(target.RootPath);
        if (!rootDir.Exists)
        {
            throw new DirectoryNotFoundException($"Target directory '{target.RootPath}' does not exist.");
        }

        var rootNode = new StorageNode
        {
            Name = string.IsNullOrEmpty(rootDir.Name) ? target.RootPath : rootDir.Name,
            Kind = StorageItemKind.Directory,
            LastModified = rootDir.LastWriteTimeUtc
        };

        var stopwatch = Stopwatch.StartNew();
        long totalFiles = 0;
        long totalDirs = 0;
        long totalBytes = 0;
        long lastReportTime = 0;

        void ReportProgress(string currentDir, bool force = false)
        {
            if (progress == null) return;
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            if (force || elapsedMs - lastReportTime >= 100)
            {
                lastReportTime = elapsedMs;
                progress.Report(new ScanProgress(
                    totalFiles,
                    totalDirs,
                    totalBytes,
                    currentDir,
                    stopwatch.Elapsed));
            }
        }

        await Task.Run(() =>
        {
            ScanDirectoryRecursive(rootDir, rootNode, ref totalFiles, ref totalDirs, ref totalBytes, ReportProgress, cancellationToken);

            if (target.IsDriveRoot && target.FreeSizeBytes > 0)
            {
                var freeSpaceNode = new StorageNode
                {
                    Name = "<Free Space>",
                    Kind = StorageItemKind.DriveFreeSpace,
                    Size = target.FreeSizeBytes,
                    AllocatedSize = target.FreeSizeBytes
                };
                rootNode.AddChild(freeSpaceNode);
            }
        }, cancellationToken).ConfigureAwait(false);

        ReportProgress(target.RootPath, force: true);
        return rootNode;
    }

    private static void ScanDirectoryRecursive(
        DirectoryInfo dirInfo,
        StorageNode parentNode,
        ref long totalFiles,
        ref long totalDirs,
        ref long totalBytes,
        Action<string, bool> reportProgress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        reportProgress(dirInfo.FullName, false);

        IEnumerable<FileSystemInfo> entries;
        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = 0,
                RecurseSubdirectories = false
            };
            entries = dirInfo.EnumerateFileSystemInfos("*", options);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            var inaccessibleNode = new StorageNode
            {
                Name = "<Inaccessible>",
                Kind = StorageItemKind.Inaccessible
            };
            parentNode.AddChild(inaccessibleNode);
            return;
        }

        long dirTotalSize = 0;
        long dirTotalAllocated = 0;
        int fileCount = 0;
        int dirCount = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawAttributes = (int)entry.Attributes;
            var isReparsePoint = (entry.Attributes & FileAttributes.ReparsePoint) != 0;
            var isCloudPlaceholder = (rawAttributes & RecallOnDataAccess) != 0
                || (rawAttributes & RecallOnOpen) != 0
                || (entry.Attributes & FileAttributes.Offline) != 0;

            var attributes = StorageItemAttributes.None;
            if (isReparsePoint) attributes |= StorageItemAttributes.ReparsePoint;
            if (isCloudPlaceholder) attributes |= StorageItemAttributes.CloudPlaceholder;
            if ((entry.Attributes & FileAttributes.Compressed) != 0) attributes |= StorageItemAttributes.Compressed;
            if ((entry.Attributes & FileAttributes.Encrypted) != 0) attributes |= StorageItemAttributes.Encrypted;
            if ((entry.Attributes & FileAttributes.Hidden) != 0) attributes |= StorageItemAttributes.Hidden;
            if ((entry.Attributes & FileAttributes.System) != 0) attributes |= StorageItemAttributes.System;
            if ((entry.Attributes & FileAttributes.ReadOnly) != 0) attributes |= StorageItemAttributes.ReadOnly;

            if (entry is FileInfo file)
            {
                long fileSize = 0;
                try
                {
                    fileSize = file.Length;
                }
                catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
                {
                    // If file was deleted or cannot be queried, keep 0
                }

                // For cloud placeholders, allocated size on disk is 0 or less than logical
                long allocatedSize = isCloudPlaceholder ? 0 : fileSize;

                var fileNode = new StorageNode
                {
                    Name = file.Name,
                    Kind = StorageItemKind.File,
                    Attributes = attributes,
                    Size = fileSize,
                    AllocatedSize = allocatedSize,
                    LastModified = file.LastWriteTimeUtc
                };

                parentNode.AddChild(fileNode);
                dirTotalSize += fileSize;
                dirTotalAllocated += allocatedSize;
                fileCount++;

                Interlocked.Increment(ref totalFiles);
                Interlocked.Add(ref totalBytes, fileSize);
            }
            else if (entry is DirectoryInfo subDir)
            {
                // CRITICAL RULE: Never follow directory reparse points (symlinks/junctions)
                if (isReparsePoint)
                {
                    var reparseDirNode = new StorageNode
                    {
                        Name = subDir.Name,
                        Kind = StorageItemKind.Directory,
                        Attributes = attributes,
                        LastModified = subDir.LastWriteTimeUtc
                    };
                    parentNode.AddChild(reparseDirNode);
                    continue;
                }

                var subDirNode = new StorageNode
                {
                    Name = subDir.Name,
                    Kind = StorageItemKind.Directory,
                    Attributes = attributes,
                    LastModified = subDir.LastWriteTimeUtc
                };
                parentNode.AddChild(subDirNode);

                ScanDirectoryRecursive(subDir, subDirNode, ref totalFiles, ref totalDirs, ref totalBytes, reportProgress, cancellationToken);

                dirTotalSize += subDirNode.Size;
                dirTotalAllocated += subDirNode.AllocatedSize;
                fileCount += subDirNode.FileCount;
                dirCount += 1 + subDirNode.DirectoryCount;

                Interlocked.Increment(ref totalDirs);
            }
        }

        parentNode.Size = dirTotalSize;
        parentNode.AllocatedSize = dirTotalAllocated;
        parentNode.FileCount = fileCount;
        parentNode.DirectoryCount = dirCount;
    }
}
