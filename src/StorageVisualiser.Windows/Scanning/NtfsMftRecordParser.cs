using System;
using System.Buffers.Binary;
using System.Text;

namespace StorageVisualiser.Windows.Scanning;

public struct ParsedMftRecord
{
    public ulong RecordNumber { get; set; }
    public ulong ParentRecordNumber { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
    public long AllocatedSize { get; set; }
    public long LastWriteTimeUtc { get; set; }
    public uint FileAttributes { get; set; }
    public bool IsDirectory { get; set; }
    public bool IsInUse { get; set; }
    public bool HasDataRuns { get; set; }
    public byte[]? MftDataRuns { get; set; }
}

public static class NtfsMftRecordParser
{
    private const uint MagicFile = 0x454C4946; // "FILE" in little-endian ASCII
    private const uint AttrStandardInfo = 0x10;
    private const uint AttrFileName = 0x30;
    private const uint AttrData = 0x80;
    private const uint AttrEnd = 0xFFFFFFFF;

    public static bool TryParseRecord(
        Span<byte> recordBytes,
        ulong recordIndex,
        out ParsedMftRecord result)
    {
        result = default;
        if (recordBytes.Length < 1024) return false;

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(recordBytes[..4]);
        if (magic != MagicFile) return false;

        ushort updateSeqOffset = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes[4..6]);
        ushort updateSeqCount = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes[6..8]);
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes[22..24]);

        bool inUse = (flags & 0x0001) != 0;
        bool isDir = (flags & 0x0002) != 0;
        if (!inUse) return false;

        // Apply fixup (Update Sequence Array) to restore last 2 bytes of each 512-byte sector
        if (updateSeqOffset + updateSeqCount * 2 <= recordBytes.Length && updateSeqCount >= 2)
        {
            ushort checkWord = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes.Slice(updateSeqOffset, 2));
            for (int i = 0; i < updateSeqCount - 1; i++)
            {
                int sectorEndOffset = (i + 1) * 512 - 2;
                if (sectorEndOffset + 2 <= recordBytes.Length)
                {
                    ushort sectorWord = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes.Slice(sectorEndOffset, 2));
                    if (sectorWord == checkWord)
                    {
                        ushort replacement = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes.Slice(updateSeqOffset + 2 + i * 2, 2));
                        BinaryPrimitives.WriteUInt16LittleEndian(recordBytes.Slice(sectorEndOffset, 2), replacement);
                    }
                }
            }
        }

        ushort firstAttrOffset = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes[20..22]);
        if (firstAttrOffset >= recordBytes.Length) return false;

        result.RecordNumber = recordIndex;
        result.IsInUse = true;
        result.IsDirectory = isDir;

        int currentOffset = firstAttrOffset;
        byte bestNamespace = 255; // Lower is better (1=Win32, 3=Win32&DOS, 0=POSIX, 2=DOS)

        while (currentOffset + 8 <= recordBytes.Length)
        {
            uint attrType = BinaryPrimitives.ReadUInt32LittleEndian(recordBytes.Slice(currentOffset, 4));
            if (attrType == AttrEnd || attrType == 0) break;

            uint attrLen = BinaryPrimitives.ReadUInt32LittleEndian(recordBytes.Slice(currentOffset + 4, 4));
            if (attrLen < 16 || currentOffset + (int)attrLen > recordBytes.Length) break;

            byte nonResident = recordBytes[currentOffset + 8];
            byte nameLength = recordBytes[currentOffset + 9];

            if (attrType == AttrStandardInfo && nonResident == 0)
            {
                ushort valOffset = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes.Slice(currentOffset + 20, 2));
                int contentPos = currentOffset + valOffset;
                if (contentPos + 48 <= recordBytes.Length)
                {
                    long writeTime = BinaryPrimitives.ReadInt64LittleEndian(recordBytes.Slice(contentPos + 24, 8));
                    uint dosFlags = BinaryPrimitives.ReadUInt32LittleEndian(recordBytes.Slice(contentPos + 32, 4));
                    result.LastWriteTimeUtc = writeTime;
                    result.FileAttributes = dosFlags;
                }
            }
            else if (attrType == AttrFileName && nonResident == 0)
            {
                ushort valOffset = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes.Slice(currentOffset + 20, 2));
                int contentPos = currentOffset + valOffset;
                if (contentPos + 66 <= recordBytes.Length)
                {
                    ulong parentFrnRaw = BinaryPrimitives.ReadUInt64LittleEndian(recordBytes.Slice(contentPos, 8));
                    ulong parentFrn = parentFrnRaw & 0x0000FFFFFFFFFFFFUL;

                    byte nameCharCount = recordBytes[contentPos + 64];
                    byte ns = recordBytes[contentPos + 65];

                    if (contentPos + 66 + nameCharCount * 2 <= recordBytes.Length)
                    {
                        // Preference: 1 (Win32), 3 (Win32&DOS), 0 (POSIX), then 2 (DOS)
                        bool prefer = result.Name == null ||
                                      (ns == 1 || ns == 3) ||
                                      (bestNamespace == 2 && ns != 2);

                        if (prefer)
                        {
                            result.Name = Encoding.Unicode.GetString(recordBytes.Slice(contentPos + 66, nameCharCount * 2));
                            result.ParentRecordNumber = parentFrn;
                            bestNamespace = ns;
                        }
                    }
                }
            }
            else if (attrType == AttrData)
            {
                // Record 0 is $MFT itself! Read its data runs to map the rest of the MFT
                if (recordIndex == 0 && nonResident != 0 && currentOffset + 34 <= recordBytes.Length)
                {
                    ushort dataRunOffset = BinaryPrimitives.ReadUInt16LittleEndian(recordBytes.Slice(currentOffset + 32, 2));
                    int runStart = currentOffset + dataRunOffset;
                    int runLen = (int)attrLen - dataRunOffset;
                    if (runStart + runLen <= recordBytes.Length && runLen > 0)
                    {
                        result.HasDataRuns = true;
                        result.MftDataRuns = recordBytes.Slice(runStart, runLen).ToArray();
                    }
                }

                // Unnamed $DATA stream is the default file content
                if (!isDir && nameLength == 0)
                {
                    if (nonResident == 0)
                    {
                        uint valLen = BinaryPrimitives.ReadUInt32LittleEndian(recordBytes.Slice(currentOffset + 16, 4));
                        result.Size = valLen;
                        result.AllocatedSize = (valLen + 511) & ~511L;
                    }
                    else if (currentOffset + 56 <= recordBytes.Length)
                    {
                        long realSize = BinaryPrimitives.ReadInt64LittleEndian(recordBytes.Slice(currentOffset + 48, 8));
                        long allocSize = BinaryPrimitives.ReadInt64LittleEndian(recordBytes.Slice(currentOffset + 40, 8));
                        result.Size = Math.Max(0, realSize);
                        result.AllocatedSize = Math.Max(0, allocSize);
                    }
                }
            }

            currentOffset += (int)attrLen;
        }

        if (recordIndex == 0 && string.IsNullOrEmpty(result.Name))
        {
            result.Name = "$MFT";
        }

        return !string.IsNullOrEmpty(result.Name);
    }
}
