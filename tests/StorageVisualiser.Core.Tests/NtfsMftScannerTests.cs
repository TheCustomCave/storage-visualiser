using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Windows.Scanning;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class NtfsMftScannerTests
{
    [Fact]
    public void DataRunDecoder_DecodesSingleRun()
    {
        // Header: length = 2 bytes (0x2), offset = 3 bytes (0x3) -> header byte = 0x32
        // Length = 0x0800 (2048 clusters)
        // Offset = 0x010000 (65536 LCN) -> [0x00, 0x00, 0x01]
        byte[] data = [0x32, 0x00, 0x08, 0x00, 0x00, 0x01, 0x00];

        var runs = DataRunDecoder.Decode(data);

        runs.Count.ShouldBe(1);
        runs[0].ClusterCount.ShouldBe(2048);
        runs[0].StartLcn.ShouldBe(65536);
    }

    [Fact]
    public void DataRunDecoder_DecodesMultipleConsecutiveRuns()
    {
        // Run 1: len=1 (0x1), off=2 (0x2) -> 0x21, len=100 (0x64), off=1000 (0x03E8)
        // Run 2: len=1 (0x1), off=2 (0x2) -> 0x21, len=50 (0x32), off=200 (0x00C8) -> LCN = 1000 + 200 = 1200
        // Run 3: len=1 (0x1), off=1 (0x1) -> 0x11, len=20 (0x14), off=-56 (0xC8 in signed byte) -> LCN = 1200 - 56 = 1144
        byte[] data = [
            0x21, 0x64, 0xE8, 0x03,
            0x21, 0x32, 0xC8, 0x00,
            0x11, 0x14, 0xC8,
            0x00
        ];

        var runs = DataRunDecoder.Decode(data);

        runs.Count.ShouldBe(3);
        runs[0].ClusterCount.ShouldBe(100);
        runs[0].StartLcn.ShouldBe(1000);

        runs[1].ClusterCount.ShouldBe(50);
        runs[1].StartLcn.ShouldBe(1200);

        runs[2].ClusterCount.ShouldBe(20);
        runs[2].StartLcn.ShouldBe(1144);
    }

    [Fact]
    public void DataRunDecoder_HandlesEmptyAndTruncatedData()
    {
        DataRunDecoder.Decode([]).Count.ShouldBe(0);
        DataRunDecoder.Decode([0x00]).Count.ShouldBe(0);
        DataRunDecoder.Decode([0x32, 0x01]).Count.ShouldBe(0); // Truncated header
    }

    [Fact]
    public void NtfsMftRecordParser_RejectsInvalidMagic()
    {
        byte[] buffer = new byte[1024];
        Encoding.ASCII.GetBytes("BAAD").CopyTo(buffer, 0);

        bool success = NtfsMftRecordParser.TryParseRecord(buffer, 10, out var record);

        success.ShouldBeFalse();
        record.IsInUse.ShouldBeFalse();
    }

    [Fact]
    public void NtfsMftRecordParser_ParsesMinimalValidRecordWithFixup()
    {
        byte[] buffer = new byte[1024];

        // 1. Magic "FILE"
        Encoding.ASCII.GetBytes("FILE").CopyTo(buffer, 0);

        // 2. Update sequence array: offset=48, size=3 (check word + 2 sector fixup words)
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4, 2), 48);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(6, 2), 3);

        // 3. Flags: in use (0x01)
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(22, 2), 0x0001);

        // 4. First attribute offset = 56
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(20, 2), 56);

        // 5. Update Sequence Array at offset 48:
        // check word = 0xAA55
        // sector 0 original word = 0x1122
        // sector 1 original word = 0x3344
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(48, 2), 0xAA55);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(50, 2), 0x1122);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(52, 2), 0x3344);

        // Put check word 0xAA55 at the end of sector 0 (510..511) and sector 1 (1022..1023)
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(510, 2), 0xAA55);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1022, 2), 0xAA55);

        // 6. Attribute $FILE_NAME (0x30) at offset 56:
        int attrPos = 56;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(attrPos, 4), 0x30); // type
        ushort attrLen = 120; // 56 + 120 = 176 (safely past string end at 162)
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(attrPos + 4, 4), attrLen); // length
        buffer[attrPos + 8] = 0; // resident
        buffer[attrPos + 9] = 0; // name length

        // Content offset: 24 bytes from attrPos
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(attrPos + 20, 2), 24);
        int contentPos = attrPos + 24; // 80

        // Parent FRN = 5 (root folder)
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(contentPos, 8), 5UL);

        // File name length = 8 chars, namespace = 1 (Win32)
        string testName = "test.txt";
        buffer[contentPos + 64] = (byte)testName.Length;
        buffer[contentPos + 65] = 1; // Win32 namespace
        Encoding.Unicode.GetBytes(testName).CopyTo(buffer, contentPos + 66); // 146..161

        // Terminating attribute 0xFFFFFFFF at offset 176
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(attrPos + attrLen, 4), 0xFFFFFFFF);

        bool parsed = NtfsMftRecordParser.TryParseRecord(buffer, 100, out var result);

        parsed.ShouldBeTrue();
        result.RecordNumber.ShouldBe(100UL);
        result.Name.ShouldBe("test.txt");
        result.ParentRecordNumber.ShouldBe(5UL);
        result.IsInUse.ShouldBeTrue();
        result.IsDirectory.ShouldBeFalse();

        // Verify fixup was applied: end of sectors restored from USA array
        BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(510, 2)).ShouldBe((ushort)0x1122);
        BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(1022, 2)).ShouldBe((ushort)0x3344);
    }

    [Fact]
    public async Task WindowsAutoScanner_FallsBackToWalkerGracefully()
    {
        var scanner = new WindowsAutoScanner();
        scanner.ScannerName.ShouldContain("Auto");

        // Scan current project directory (non-drive root, should use Directory Walker)
        var currentDir = AppContext.BaseDirectory;
        var target = new ScanTarget
        {
            RootPath = currentDir,
            DisplayName = currentDir,
            DriveType = DriveType.Fixed,
            IsDriveRoot = false
        };

        var rootNode = await scanner.ScanAsync(target, null, CancellationToken.None);

        rootNode.ShouldNotBeNull();
        rootNode.HasChildren.ShouldBeTrue();
        scanner.ActiveScannerName.ShouldBe("Directory Walker");
    }
}
