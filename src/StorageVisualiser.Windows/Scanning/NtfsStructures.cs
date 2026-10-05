using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace StorageVisualiser.Windows.Scanning;

internal static class NtfsNative
{
    public const uint FsctlGetNtfsVolumeData = 0x00090064;
    public const uint GenericRead = 0x80000000;
    public const uint FileShareRead = 0x00000001;
    public const uint FileShareWrite = 0x00000002;
    public const uint FileShareDelete = 0x00000004;
    public const uint OpenExisting = 3;
    public const uint FileFlagBackupSemantics = 0x02000000;

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TOKEN_PRIVILEGES
    {
        public int PrivilegeCount;
        public LUID Luid;
        public int Attributes;
    }

    public const int SE_PRIVILEGE_ENABLED = 0x00000002;
    public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    public const uint TOKEN_QUERY = 0x0008;

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out SafeFileHandle TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        SafeFileHandle TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    public static bool TryEnablePrivilege(string privilegeName)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var hToken))
                return false;

            using (hToken)
            {
                if (!LookupPrivilegeValue(null, privilegeName, out var luid))
                    return false;

                var tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                };

                return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch
        {
            return false;
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct NtfsVolumeDataBuffer
{
    public long VolumeSerialNumber;
    public long NumberSectors;
    public long TotalClusters;
    public long FreeClusters;
    public long TotalReserved;
    public uint BytesPerSector;
    public uint BytesPerCluster;
    public uint BytesPerFileRecordSegment;
    public uint ClustersPerFileRecordSegment;
    public long MftValidDataLength;
    public long MftStartLcn;
    public long Mft2StartLcn;
    public long MftZoneStart;
    public long MftZoneEnd;
}

public readonly record struct DataRun(long StartLcn, long ClusterCount);

public static class DataRunDecoder
{
    public static List<DataRun> Decode(ReadOnlySpan<byte> runData)
    {
        var runs = new List<DataRun>();
        int offset = 0;
        long currentLcn = 0;

        while (offset < runData.Length)
        {
            byte header = runData[offset++];
            if (header == 0) break; // Terminating run

            int lengthBytes = header & 0x0F;
            int offsetBytes = (header >> 4) & 0x0F;

            if (offsetBytes == 0)
            {
                // Sparse run (clusters unallocated)
                if (offset + lengthBytes > runData.Length) break;
                long sparseLength = 0;
                for (int i = 0; i < lengthBytes; i++)
                {
                    sparseLength |= (long)runData[offset++] << (i * 8);
                }
                runs.Add(new DataRun(-1, sparseLength));
                continue;
            }

            if (offset + lengthBytes + offsetBytes > runData.Length)
            {
                break; // Corrupted run header
            }

            long clusterLength = 0;
            for (int i = 0; i < lengthBytes; i++)
            {
                clusterLength |= (long)runData[offset++] << (i * 8);
            }

            long lcnDelta = 0;
            for (int i = 0; i < offsetBytes; i++)
            {
                lcnDelta |= (long)runData[offset++] << (i * 8);
            }

            // Sign-extend lcnDelta if negative
            if (offsetBytes > 0 && (runData[offset - 1] & 0x80) != 0)
            {
                for (int i = offsetBytes; i < 8; i++)
                {
                    lcnDelta |= unchecked((long)(0xFFUL << (i * 8)));
                }
            }

            currentLcn += lcnDelta;
            runs.Add(new DataRun(currentLcn, clusterLength));
        }

        return runs;
    }
}
