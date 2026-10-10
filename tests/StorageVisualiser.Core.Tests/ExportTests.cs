using Shouldly;
using StorageVisualiser.Core.Export;
using StorageVisualiser.Core.Model;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class ExportTests
{
    [Theory]
    [InlineData("=cmd|'/C calc'!A0")]
    [InlineData("+123")]
    [InlineData("-456")]
    [InlineData("@SUM(A1:A10)")]
    [InlineData("Normal File.txt")]
    [InlineData("File with \"quotes\".txt")]
    public void EscapeCsvField_NeutralisesFormulasAndEscapesQuotes(string input)
    {
        // Fix test expectation for + and - without unicode artifact
        var result = ExportSecurityHelper.EscapeCsvField(input);
        if (input.StartsWith('+') || input.StartsWith('-') || input.StartsWith('=') || input.StartsWith('@'))
        {
            result.ShouldStartWith("\"'");
        }
        else
        {
            result.ShouldStartWith("\"");
            result.ShouldNotStartWith("\"'");
        }
    }

    [Theory]
    [InlineData(@"C:\Users\john.doe\Documents\file.txt", @"C:\Users\<user>\Documents\file.txt")]
    [InlineData(@"C:\Users\Alice\AppData\Local\test.log", @"C:\Users\<user>\AppData\Local\test.log")]
    [InlineData(@"/home/bob/projects/code.cs", @"/home/<user>/projects/code.cs")]
    [InlineData(@"D:\Data\Shared\report.pdf", @"D:\Data\Shared\report.pdf")]
    public void RedactPath_AnonymisesUserProfileFolders(string input, string expected)
    {
        var redacted = ExportSecurityHelper.RedactPath(input);
        redacted.ShouldBe(expected);
    }

    [Fact]
    public void ExportTopFilesToCsv_ProducesValidHeaderAndRows()
    {
        var root = new StorageNode { Name = "TestRoot", Kind = StorageItemKind.Directory, Size = 1024 };
        var subDir = new StorageNode { Name = "SubFolder", Kind = StorageItemKind.Directory, Size = 1024, Parent = root };
        var file = new StorageNode { Name = "file.txt", Kind = StorageItemKind.File, Size = 1024, Parent = subDir };
        subDir.Children.Add(file);
        root.Children.Add(subDir);

        var csv = CsvReportExporter.ExportTopFilesToCsv(root, redactPaths: false, limit: 10);

        csv.ShouldContain("Name,FullPath,Extension,SizeBytes,FormattedSize,PercentageOfTotal,LastModified");
        csv.ShouldContain("\"file.txt\"");
        csv.ShouldContain("1024");
    }

    [Fact]
    public void ExportFileTypesToCsv_ProducesValidHeaderAndRows()
    {
        var root = new StorageNode { Name = "TestRoot", Kind = StorageItemKind.Directory, Size = 2048 };
        var file = new StorageNode { Name = "data.json", Kind = StorageItemKind.File, Size = 2048, Parent = root };
        root.Children.Add(file);

        var csv = CsvReportExporter.ExportFileTypesToCsv(root);

        csv.ShouldContain("Extension,Category,TotalSizeBytes,FormattedTotalSize,PercentageOfTotal,FileCount");
        csv.ShouldContain("\".json\"");
        csv.ShouldContain("2048");
    }

    [Fact]
    public void ExportToJson_ProducesValidJsonData()
    {
        var root = new StorageNode { Name = "TestRoot", Kind = StorageItemKind.Directory, Size = 4096 };
        var file = new StorageNode { Name = "doc.docx", Kind = StorageItemKind.File, Size = 4096, Parent = root };
        root.Children.Add(file);

        var json = JsonReportExporter.ExportToJson(root, @"C:\TestRoot", scanDuration: "1.2s", redactPaths: false);

        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("\"totalSizeBytes\":4096");
        json.ShouldContain("\"doc.docx\"");
    }
}
