using Shouldly;
using StorageVisualiser.Cli;
using StorageVisualiser.Core.Formatting;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Parse_EmptyArgs_ShowsHelp()
    {
        var options = CliOptions.Parse([]);
        options.ShowHelp.ShouldBeTrue();
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    public void Parse_HelpFlags_ShowsHelp(string flag)
    {
        var options = CliOptions.Parse([flag]);
        options.ShowHelp.ShouldBeTrue();
    }

    [Theory]
    [InlineData("-v")]
    [InlineData("--version")]
    public void Parse_VersionFlags_ShowsVersion(string flag)
    {
        var options = CliOptions.Parse([flag]);
        options.ShowVersion.ShouldBeTrue();
    }

    [Fact]
    public void Parse_ScanCommandWithTargetPath_SetsTargetPath()
    {
        string[] args = ["scan", @"C:\TestFolder"];
        var options = CliOptions.Parse(args);
        options.TargetPath.ShouldBe(@"C:\TestFolder");
        options.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Parse_DirectPathWithoutScanVerb_SetsTargetPath()
    {
        string[] args = [@"D:\Data"];
        var options = CliOptions.Parse(args);
        options.TargetPath.ShouldBe(@"D:\Data");
        options.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Parse_OptionsAndFlags_ParsesCorrectly()
    {
        string[] args =
        [
            "scan", @"C:\Logs",
            "--top", "50",
            "--json", "output.json",
            "--csv", "report.csv",
            "--allocated",
            "--unit", "iec",
            "--threshold-gb", "5.5",
            "--threshold-free-percent", "10",
            "--silent"
        ];

        var options = CliOptions.Parse(args);

        options.TargetPath.ShouldBe(@"C:\Logs");
        options.TopFilesLimit.ShouldBe(50);
        options.JsonOutputFile.ShouldBe("output.json");
        options.OutputJsonToStdout.ShouldBeFalse();
        options.CsvOutputFile.ShouldBe("report.csv");
        options.OutputCsvToStdout.ShouldBeFalse();
        options.UseAllocatedSize.ShouldBeTrue();
        options.UnitSystem.ShouldBe(UnitSystem.Iec);
        options.ThresholdGb.ShouldBe(5.5);
        options.ThresholdFreePercent.ShouldBe(10.0);
        options.Silent.ShouldBeTrue();
        options.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Parse_JsonStdoutDash_SetsOutputJsonToStdout()
    {
        string[] args = ["scan", @"C:\", "--json", "-"];
        var options = CliOptions.Parse(args);
        options.OutputJsonToStdout.ShouldBeTrue();
        options.JsonOutputFile.ShouldBeNull();
    }

    [Fact]
    public void Parse_JsonStdoutWithoutParameter_SetsOutputJsonToStdout()
    {
        string[] args = ["scan", @"C:\", "--json", "--silent"];
        var options = CliOptions.Parse(args);
        options.OutputJsonToStdout.ShouldBeTrue();
        options.JsonOutputFile.ShouldBeNull();
        options.Silent.ShouldBeTrue();
    }

    [Fact]
    public void Parse_CsvStdoutDash_SetsOutputCsvToStdout()
    {
        string[] args = ["scan", @"C:\", "--csv", "-"];
        var options = CliOptions.Parse(args);
        options.OutputCsvToStdout.ShouldBeTrue();
        options.CsvOutputFile.ShouldBeNull();
    }

    [Fact]
    public void Parse_InvalidUnit_SetsErrorMessage()
    {
        string[] args = ["scan", @"C:\", "--unit", "invalid"];
        var options = CliOptions.Parse(args);
        options.ErrorMessage.ShouldNotBeNull();
        options.ErrorMessage.ShouldContain("Unknown unit system");
    }

    [Fact]
    public void Parse_MissingPath_SetsErrorMessage()
    {
        string[] args = ["scan"];
        var options = CliOptions.Parse(args);
        options.ErrorMessage.ShouldNotBeNull();
        options.ErrorMessage.ShouldContain("Missing path");
    }

    [Fact]
    public void Parse_UnknownOption_SetsErrorMessage()
    {
        string[] args = ["scan", @"C:\", "--unknown-flag"];
        var options = CliOptions.Parse(args);
        options.ErrorMessage.ShouldNotBeNull();
        options.ErrorMessage.ShouldContain("Unrecognised argument");
    }
}
