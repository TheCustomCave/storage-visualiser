using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Core.Scanning;

namespace StorageVisualiser.Windows.Scanning;

public sealed class WindowsNtfsMftScanner : IScanner
{
    private const int RecallOnDataAccess = 0x00400000;
    private const int RecallOnOpen = 0x00040000;
    private const int ReparsePoint = 0x00000400;

    public string ScannerName => "NTFS MFT Direct Scanner";

    public static Action<string>? LogAction { get; set; }

    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool CanScan(ScanTarget target)
    {
        if (!OperatingSystem.IsWindows() || !target.IsDriveRoot) return false;
        if (!IsAdministrator()) return false;

        try
        {
            var root = Path.GetPathRoot(target.RootPath) ?? target.RootPath;
            var d = new DriveInfo(root);
            return d.IsReady && d.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<StorageNode> ScanAsync(
        ScanTarget target,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() => PerformMftScan(target, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private static StorageNode PerformMftScan(
        ScanTarget target,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var rootPath = target.RootPath.TrimEnd('\\');
        var volumeDevicePath = @"\\.\" + rootPath;

        LogAction?.Invoke($"[MFT Scanner] Starting MFT scan for {volumeDevicePath}...");
        NtfsNative.TryEnablePrivilege("SeBackupPrivilege");

        using var hVolume = NtfsNative.CreateFile(
            volumeDevicePath,
            NtfsNative.GenericRead,
            NtfsNative.FileShareRead | NtfsNative.FileShareWrite | NtfsNative.FileShareDelete,
            IntPtr.Zero,
            NtfsNative.OpenExisting,
            NtfsNative.FileFlagBackupSemantics,
            IntPtr.Zero);

        if (hVolume.IsInvalid)
        {
            var err = Marshal.GetLastWin32Error();
            LogAction?.Invoke($"[MFT Scanner] CreateFile failed for '{volumeDevicePath}'. Win32 Error: {err}");
            throw new UnauthorizedAccessException($"Failed to open NTFS volume '{volumeDevicePath}'. Win32 Error: {err}");
        }

        var volumeData = GetNtfsVolumeData(hVolume);
        var bytesPerCluster = volumeData.BytesPerCluster;
        var bytesPerSector = volumeData.BytesPerSector > 0 ? volumeData.BytesPerSector : 4096;
        var recordSize = volumeData.BytesPerFileRecordSegment > 0 ? (int)volumeData.BytesPerFileRecordSegment : 1024;
        LogAction?.Invoke($"[MFT Scanner] Volume geometry: BytesPerCluster={bytesPerCluster}, BytesPerSector={bytesPerSector}, RecordSize={recordSize}, MftValidDataLength={volumeData.MftValidDataLength}, MftStartLcn={volumeData.MftStartLcn}");

        if (bytesPerCluster == 0 || volumeData.MftValidDataLength <= 0)
        {
            throw new InvalidDataException("Invalid NTFS volume geometry returned by device.");
        }

        // 1. Read Record 0 ($MFT) to discover all cluster runs of the MFT
        var record0 = ReadMftRecord0(hVolume, volumeData);
        if (!record0.HasDataRuns || record0.MftDataRuns == null)
        {
            LogAction?.Invoke("[MFT Scanner] Failed to decode $MFT data runs from Record 0.");
            throw new InvalidDataException("Failed to decode $MFT data runs from Record 0.");
        }

        var runs = DataRunDecoder.Decode(record0.MftDataRuns);
        LogAction?.Invoke($"[MFT Scanner] Decoded {runs.Count} MFT data runs from Record 0.");
        if (runs.Count == 0)
        {
            throw new InvalidDataException("No cluster runs found for $MFT.");
        }

        // 2. Scan and parse all MFT records
        int totalExpectedRecords = (int)Math.Min(int.MaxValue, volumeData.MftValidDataLength / recordSize);
        var records = new Dictionary<ulong, ParsedMftRecord>(Math.Min(totalExpectedRecords, 500_000));
        records[0] = record0;

        var stopwatch = Stopwatch.StartNew();
        long totalBytesScanned = 0;
        long lastReportMs = 0;
        ulong currentRecordIndex = 0;

        const int bufferSize = 2 * 1024 * 1024; // 2 MB sequential buffer
        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            foreach (var run in runs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (run.StartLcn < 0)
                {
                    // Sparse cluster run in MFT
                    long sparseBytes = run.ClusterCount * bytesPerCluster;
                    currentRecordIndex += (ulong)(sparseBytes / recordSize);
                    continue;
                }

                long runOffset = run.StartLcn * bytesPerCluster;
                long runBytes = run.ClusterCount * bytesPerCluster;
                long bytesRemaining = runBytes;
                long currentFilePos = runOffset;

                while (bytesRemaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int toRead = (int)Math.Min(bufferSize, bytesRemaining);

                    int bytesRead = RandomAccess.Read(hVolume, readBuffer.AsSpan(0, toRead), currentFilePos);
                    if (bytesRead <= 0) break;

                    int recordsInBuffer = bytesRead / recordSize;
                    for (int r = 0; r < recordsInBuffer; r++)
                    {
                        ulong rIndex = currentRecordIndex + (ulong)r;
                        var recordSlice = readBuffer.AsSpan(r * recordSize, recordSize);
                        if (NtfsMftRecordParser.TryParseRecord(recordSlice, rIndex, out var parsed))
                        {
                            records[rIndex] = parsed;
                        }
                    }

                    currentRecordIndex += (ulong)recordsInBuffer;
                    currentFilePos += bytesRead;
                    bytesRemaining -= bytesRead;
                    totalBytesScanned += bytesRead;

                    if (progress != null && stopwatch.ElapsedMilliseconds - lastReportMs >= 150)
                    {
                        lastReportMs = stopwatch.ElapsedMilliseconds;
                        progress.Report(new ScanProgress(
                            records.Count,
                            0,
                            totalBytesScanned,
                            $"{records.Count:N0} MFT records read",
                            stopwatch.Elapsed));
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }

        // 3. Assemble StorageNode directory tree
        var rootNode = new StorageNode
        {
            Name = target.RootPath,
            Kind = StorageItemKind.Directory,
            LastModified = DateTimeOffset.UtcNow
        };

        var nodeLookup = new Dictionary<ulong, StorageNode>(records.Count);
        nodeLookup[5] = rootNode; // Record 5 is root directory '\'

        // First pass: instantiate all directory nodes so hierarchy can be linked
        foreach (var (frn, rec) in records)
        {
            if (frn == 5) continue;
            if (rec.IsDirectory)
            {
                var attrs = StorageItemAttributes.None;
                if ((rec.FileAttributes & (RecallOnDataAccess | RecallOnOpen)) != 0) attrs |= StorageItemAttributes.CloudPlaceholder;
                if ((rec.FileAttributes & ReparsePoint) != 0) attrs |= StorageItemAttributes.ReparsePoint;

                var dirNode = new StorageNode
                {
                    Name = rec.Name,
                    Kind = StorageItemKind.Directory,
                    Attributes = attrs,
                    Size = 0,
                    AllocatedSize = 0,
                    LastModified = rec.LastWriteTimeUtc > 0 ? DateTimeOffset.FromFileTime(rec.LastWriteTimeUtc) : null
                };
                nodeLookup[frn] = dirNode;
            }
        }

        // Second pass: attach directories and files to parents
        long fileCount = 0;
        long dirCount = 0;

        foreach (var (frn, rec) in records)
        {
            if (frn == 5) continue;

            StorageNode? parentNode = null;
            if (rec.ParentRecordNumber != 0 && nodeLookup.TryGetValue(rec.ParentRecordNumber, out var p))
            {
                parentNode = p;
            }
            else if (frn > 15) // Attach orphaned non-system entries to root
            {
                parentNode = rootNode;
            }

            if (parentNode == null) continue;

            if (rec.IsDirectory)
            {
                if (nodeLookup.TryGetValue(frn, out var dNode))
                {
                    parentNode.AddChild(dNode);
                    dirCount++;
                }
            }
            else
            {
                var attrs = StorageItemAttributes.None;
                if ((rec.FileAttributes & (RecallOnDataAccess | RecallOnOpen)) != 0) attrs |= StorageItemAttributes.CloudPlaceholder;
                if ((rec.FileAttributes & ReparsePoint) != 0) attrs |= StorageItemAttributes.ReparsePoint;

                var fileNode = new StorageNode
                {
                    Name = rec.Name,
                    Kind = StorageItemKind.File,
                    Attributes = attrs,
                    Size = rec.Size,
                    AllocatedSize = rec.AllocatedSize,
                    LastModified = rec.LastWriteTimeUtc > 0 ? DateTimeOffset.FromFileTime(rec.LastWriteTimeUtc) : null
                };
                parentNode.AddChild(fileNode);
                fileCount++;
            }
        }

        // 4. Compute directory aggregate sizes and counts post-order
        CalculateDirectoryAggregates(rootNode);

        // 5. Add Free Space node if applicable
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

        progress?.Report(new ScanProgress(fileCount, dirCount, rootNode.Size, target.RootPath, stopwatch.Elapsed));
        return rootNode;
    }

    private static void CalculateDirectoryAggregates(StorageNode node)
    {
        long totalSize = 0;
        long totalAlloc = 0;
        int files = 0;
        int dirs = 0;

        foreach (var child in node.Children)
        {
            if (child.Kind == StorageItemKind.Directory)
            {
                CalculateDirectoryAggregates(child);
                totalSize += child.Size;
                totalAlloc += child.AllocatedSize;
                dirs += 1 + child.DirectoryCount;
                files += child.FileCount;
            }
            else if (child.Kind == StorageItemKind.File)
            {
                totalSize += child.Size;
                totalAlloc += child.AllocatedSize;
                files++;
            }
        }

        node.Size = totalSize;
        node.AllocatedSize = totalAlloc;
        node.FileCount = files;
        node.DirectoryCount = dirs;
    }

    private static NtfsVolumeDataBuffer GetNtfsVolumeData(SafeFileHandle hVolume)
    {
        int size = Marshal.SizeOf<NtfsVolumeDataBuffer>();
        IntPtr pBuffer = Marshal.AllocHGlobal(size);
        try
        {
            bool ok = NtfsNative.DeviceIoControl(
                hVolume,
                NtfsNative.FsctlGetNtfsVolumeData,
                IntPtr.Zero,
                0,
                pBuffer,
                (uint)size,
                out _,
                IntPtr.Zero);

            if (!ok)
            {
                var err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"FSCTL_GET_NTFS_VOLUME_DATA failed with Win32 Error {err}");
            }

            return Marshal.PtrToStructure<NtfsVolumeDataBuffer>(pBuffer);
        }
        finally
        {
            Marshal.FreeHGlobal(pBuffer);
        }
    }

    private static ParsedMftRecord ReadMftRecord0(SafeFileHandle hVolume, in NtfsVolumeDataBuffer volumeData)
    {
        long record0Offset = volumeData.MftStartLcn * volumeData.BytesPerCluster;
        int recordSize = volumeData.BytesPerFileRecordSegment > 0 ? (int)volumeData.BytesPerFileRecordSegment : 1024;
        int readLen = Math.Max(4096, Math.Max((int)volumeData.BytesPerCluster, recordSize));
        byte[] buffer = new byte[readLen];
        int bytesRead = RandomAccess.Read(hVolume, buffer, record0Offset);
        if (bytesRead < recordSize)
        {
            throw new InvalidDataException($"Failed to read MFT Record 0 from volume. Bytes read: {bytesRead}, expected at least {recordSize}.");
        }

        if (NtfsMftRecordParser.TryParseRecord(buffer.AsSpan(0, recordSize), 0, out var rec))
        {
            return rec;
        }

        throw new InvalidDataException("Failed to parse MFT Record 0.");
    }
}
