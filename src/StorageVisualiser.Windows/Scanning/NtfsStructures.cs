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
    public const uint OpenExisting = 3;

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
