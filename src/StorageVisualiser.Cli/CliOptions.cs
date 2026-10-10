using System;
using System.IO;
using StorageVisualiser.Core.Formatting;

namespace StorageVisualiser.Cli;

public sealed record CliOptions
{
    public string? TargetPath { get; init; }
    public int TopFilesLimit { get; init; } = 20;
    public string? JsonOutputFile { get; init; }
    public bool OutputJsonToStdout { get; init; }
    public string? CsvOutputFile { get; init; }
    public bool OutputCsvToStdout { get; init; }
    public string? HtmlOutputFile { get; init; }
    public bool UseAllocatedSize { get; init; }
    public UnitSystem UnitSystem { get; init; } = UnitSystem.Windows;
    public double? ThresholdGb { get; init; }
    public double? ThresholdFreePercent { get; init; }
    public bool Silent { get; init; }
    public bool ShowHelp { get; init; }
    public bool ShowVersion { get; init; }
    public string? ErrorMessage { get; init; }

    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliOptions { ShowHelp = true };
        }

        string? targetPath = null;
        int top = 20;
        string? jsonFile = null;
        bool jsonStdout = false;
        string? csvFile = null;
        bool csvStdout = false;
        string? htmlFile = null;
        bool allocated = false;
        var unitSystem = UnitSystem.Windows;
        double? thresholdGb = null;
        double? thresholdFreePercent = null;
        bool silent = false;
        bool help = false;
        bool version = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "-h" or "--help" or "/?" or "-?")
            {
                help = true;
            }
            else if (arg is "-v" or "--version")
            {
                version = true;
            }
            else if (arg is "-s" or "--silent" or "--quiet")
            {
                silent = true;
            }
            else if (arg is "--allocated" or "-a")
            {
                allocated = true;
            }
            else if (arg is "scan")
            {
                // Optional sub-command keyword: 'scan C:\'
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    targetPath = args[++i];
                }
            }
            else if (arg is "--top" or "-t")
            {
                if (i + 1 < args.Length && int.TryParse(args[++i], out var parsedTop) && parsedTop > 0)
                {
                    top = parsedTop;
                }
                else
                {
                    return new CliOptions { ErrorMessage = "Invalid value for --top. Expected a positive integer." };
                }
            }
            else if (arg == "--json")
            {
                if (i + 1 < args.Length && args[i + 1] == "-")
                {
                    i++;
                    jsonStdout = true;
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    jsonFile = args[++i];
                }
                else
                {
                    jsonStdout = true;
                }
            }
            else if (arg == "--csv")
            {
                if (i + 1 < args.Length && args[i + 1] == "-")
                {
                    i++;
                    csvStdout = true;
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    csvFile = args[++i];
                }
                else
                {
                    csvStdout = true;
                }
            }
            else if (arg == "--html")
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    htmlFile = args[++i];
                }
                else
                {
                    htmlFile = "auto";
                }
            }
            else if (arg is "--unit" or "-u")
            {
                if (i + 1 < args.Length)
                {
                    var val = args[++i].ToLowerInvariant();
                    UnitSystem? parsedSystem = val switch
                    {
                        "iec" or "binary" => UnitSystem.Iec,
                        "si" or "metric" => UnitSystem.Si,
                        "windows" or "win" => UnitSystem.Windows,
                        _ => null
                    };

                    if (parsedSystem == null)
                    {
                        return new CliOptions { ErrorMessage = $"Unknown unit system: '{val}'. Expected: windows, iec, or si." };
                    }

                    unitSystem = parsedSystem.Value;
                }
                else
                {
                    return new CliOptions { ErrorMessage = "Missing value for --unit. Expected: windows, iec, or si." };
                }
            }
            else if (arg is "--threshold-gb" or "--threshold-size")
            {
                if (i + 1 < args.Length && double.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out var tGb) && tGb > 0)
                {
                    thresholdGb = tGb;
                }
                else
                {
                    return new CliOptions { ErrorMessage = "Invalid value for --threshold-gb. Expected a positive number." };
                }
            }
            else if (arg is "--threshold-free-percent" or "--threshold-free")
            {
                if (i + 1 < args.Length && double.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out var tFree) && tFree >= 0 && tFree <= 100)
                {
                    thresholdFreePercent = tFree;
                }
                else
                {
                    return new CliOptions { ErrorMessage = "Invalid value for --threshold-free-percent. Expected percentage between 0 and 100." };
                }
            }
            else if (!arg.StartsWith('-') && targetPath == null)
            {
                targetPath = arg;
            }
            else
            {
                return new CliOptions { ErrorMessage = $"Unrecognised argument: '{arg}'" };
            }
        }

        if (!help && !version && string.IsNullOrWhiteSpace(targetPath))
        {
            return new CliOptions { ErrorMessage = "Missing path to scan. Usage: StorageVisualiser.Cli scan <path>" };
        }

        return new CliOptions
        {
            TargetPath = targetPath,
            TopFilesLimit = top,
            JsonOutputFile = jsonFile,
            OutputJsonToStdout = jsonStdout,
            CsvOutputFile = csvFile,
            OutputCsvToStdout = csvStdout,
            HtmlOutputFile = htmlFile,
            UseAllocatedSize = allocated,
            UnitSystem = unitSystem,
            ThresholdGb = thresholdGb,
            ThresholdFreePercent = thresholdFreePercent,
            Silent = silent,
            ShowHelp = help,
            ShowVersion = version
        };
    }
}
