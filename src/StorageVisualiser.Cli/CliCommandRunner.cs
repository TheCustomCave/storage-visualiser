using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StorageVisualiser.Core.Analysis;
using StorageVisualiser.Core.Export;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Windows.Scanning;

namespace StorageVisualiser.Cli;

public static class CliCommandRunner
{
    public const int ExitSuccess = 0;
    public const int ExitThresholdAlert = 1;
    public const int ExitError = 2;

    public static async Task<int> RunAsync(string[] args)
    {
        var options = CliOptions.Parse(args);

        if (options.ErrorMessage != null)
        {
            Console.Error.WriteLine($"Error: {options.ErrorMessage}");
            Console.Error.WriteLine("Run 'StorageVisualiser.Cli --help' for usage information.");
            return ExitError;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return ExitSuccess;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine("Storage Visualiser CLI v0.1.1-alpha");
            return ExitSuccess;
        }

        if (string.IsNullOrWhiteSpace(options.TargetPath))
        {
            Console.Error.WriteLine("Error: No target path or drive specified.");
            Console.Error.WriteLine("Usage: StorageVisualiser.Cli scan <path> [options]");
            Console.Error.WriteLine("Run 'StorageVisualiser.Cli --help' for details.");
            return ExitError;
        }

        var fullPath = Path.GetFullPath(options.TargetPath);
        if (!Directory.Exists(fullPath))
        {
            Console.Error.WriteLine($"Error: Target path does not exist or is inaccessible: '{fullPath}'");
            return ExitError;
        }

        SizeFormatter.DefaultUnitSystem = options.UnitSystem;

        bool suppressConsoleOutput = options.Silent || options.OutputJsonToStdout || options.OutputCsvToStdout;

        if (!suppressConsoleOutput)
        {
            Console.WriteLine("======================================================================");
            Console.WriteLine(" Storage Visualiser CLI (Headless Engine)");
            Console.WriteLine($" Scanning: {fullPath}");
            Console.WriteLine("======================================================================");
        }

        DriveType driveType = DriveType.Unknown;
        long totalBytes = 0;
        long freeBytes = 0;

        try
        {
            var driveRoot = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(driveRoot))
            {
                var dInfo = new DriveInfo(driveRoot);
                if (dInfo.IsReady)
                {
                    driveType = dInfo.DriveType;
                    totalBytes = dInfo.TotalSize;
                    freeBytes = dInfo.AvailableFreeSpace;
                }
            }
        }
        catch
        {
            // Ignore drive queries on unmapped paths
        }

        var isDriveRoot = string.Equals(Path.GetPathRoot(fullPath)?.TrimEnd('\\'), fullPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        var target = new ScanTarget
        {
            RootPath = fullPath,
            DisplayName = fullPath,
            DriveType = driveType,
            TotalSizeBytes = totalBytes,
            FreeSizeBytes = freeBytes,
            IsDriveRoot = isDriveRoot
        };

        var scanner = new WindowsAutoScanner();
        var sw = Stopwatch.StartNew();

        var root = await scanner.ScanAsync(target, null, CancellationToken.None);
        sw.Stop();

        if (root == null)
        {
            Console.Error.WriteLine($"Error: Scan failed for path: '{fullPath}'");
            return ExitError;
        }

        var durationStr = $"{sw.Elapsed.TotalSeconds:F2}s";

        // Query drive capacity and free space if available
        long? driveCapacity = totalBytes > 0 ? totalBytes : null;
        long? driveFree = freeBytes > 0 ? freeBytes : null;
        double? driveFreePercent = (totalBytes > 0 && freeBytes >= 0)
            ? (double)freeBytes / totalBytes * 100.0
            : null;

        // Top files and File types analysis
        var topFiles = StorageAnalysisEngine.GetTopFiles(root, options.TopFilesLimit);
        var fileTypes = StorageAnalysisEngine.GetFileTypeBreakdown(root);

        // Evaluate thresholds
        var thresholdAlerts = new List<string>();
        bool thresholdExceeded = false;

        long effectiveSize = options.UseAllocatedSize ? root.AllocatedSize : root.Size;

        if (options.ThresholdGb.HasValue)
        {
            long limitBytes = (long)(options.ThresholdGb.Value * 1024.0 * 1024.0 * 1024.0);
            if (effectiveSize > limitBytes)
            {
                thresholdExceeded = true;
                thresholdAlerts.Add($"Scanned size ({SizeFormatter.Format(effectiveSize)}) exceeds threshold of {options.ThresholdGb.Value:F2} GB");
            }
        }

        if (options.ThresholdFreePercent.HasValue && driveFreePercent.HasValue)
        {
            if (driveFreePercent.Value < options.ThresholdFreePercent.Value)
            {
                thresholdExceeded = true;
                thresholdAlerts.Add($"Drive free space ({driveFreePercent.Value:F1}%) is below threshold of {options.ThresholdFreePercent.Value:F1}%");
            }
        }

        // DTO for JSON output
        var dto = new CliScanResultDto
        {
            TargetPath = fullPath,
            MachineName = Environment.MachineName,
            ScanTimeUtc = DateTimeOffset.UtcNow,
            ScanDurationMs = sw.ElapsedMilliseconds,
            ScannerUsed = scanner.ActiveScannerName,
            TotalFiles = root.FileCount,
            TotalDirectories = root.DirectoryCount,
            TotalSizeBytes = root.Size,
            FormattedTotalSize = SizeFormatter.Format(root.Size),
            AllocatedSizeBytes = root.AllocatedSize,
            FormattedAllocatedSize = SizeFormatter.Format(root.AllocatedSize),
            DriveCapacityBytes = driveCapacity,
            DriveFreeBytes = driveFree,
            DriveFreePercentage = driveFreePercent.HasValue ? Math.Round(driveFreePercent.Value, 1) : null,
            ThresholdExceeded = thresholdExceeded,
            ThresholdAlerts = thresholdAlerts,
            TopFiles = topFiles.Select(f => new CliTopFileDto
            {
                Name = f.Name,
                Path = f.FullPath,
                Extension = f.Extension,
                SizeBytes = f.Size,
                FormattedSize = f.FormattedSize,
                PercentageOfTotal = Math.Round(f.PercentageOfTotal, 2),
                LastModified = f.FormattedModified
            }).ToList(),
            FileTypeBreakdown = fileTypes.Select(t => new CliFileTypeDto
            {
                Extension = t.Extension,
                Category = t.Category,
                FileCount = t.FileCount,
                TotalSizeBytes = t.TotalSize,
                FormattedTotalSize = t.FormattedTotalSize,
                PercentageOfTotal = Math.Round(t.PercentageOfTotal, 2)
            }).ToList()
        };

        // 1. JSON output
        if (options.OutputJsonToStdout)
        {
            var json = JsonSerializer.Serialize(dto, CliJsonContext.Default.CliScanResultDto);
            Console.WriteLine(json);
        }
        else if (!string.IsNullOrEmpty(options.JsonOutputFile))
        {
            var json = JsonSerializer.Serialize(dto, CliJsonContext.Default.CliScanResultDto);
            var dir = Path.GetDirectoryName(options.JsonOutputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(options.JsonOutputFile, json, Encoding.UTF8);
            if (!suppressConsoleOutput)
            {
                Console.WriteLine($"[✓] JSON report saved: {options.JsonOutputFile}");
            }
        }

        // 2. CSV output
        if (options.OutputCsvToStdout)
        {
            var csv = GenerateCsv(dto);
            Console.WriteLine(csv);
        }
        else if (!string.IsNullOrEmpty(options.CsvOutputFile))
        {
            var csv = GenerateCsv(dto);
            var dir = Path.GetDirectoryName(options.CsvOutputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(options.CsvOutputFile, csv, Encoding.UTF8);
            if (!suppressConsoleOutput)
            {
                Console.WriteLine($"[✓] CSV report saved: {options.CsvOutputFile}");
            }
        }

        // 3. HTML Report export
        if (!string.IsNullOrEmpty(options.HtmlOutputFile))
        {
            var htmlPath = options.HtmlOutputFile == "auto"
                ? Path.Combine(Environment.CurrentDirectory, HtmlReportExporter.GenerateDefaultFileName(fullPath))
                : options.HtmlOutputFile;

            var dir = Path.GetDirectoryName(htmlPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var htmlContent = HtmlReportExporter.ExportToHtml(root, fullPath, durationStr);
            await File.WriteAllTextAsync(htmlPath, htmlContent, Encoding.UTF8);
            if (!suppressConsoleOutput)
            {
                Console.WriteLine($"[✓] HTML storage report saved: {htmlPath}");
            }
        }

        // 4. Human-readable console summary (when stdout is not used for data)
        if (!suppressConsoleOutput)
        {
            PrintSummary(dto, scanner.ActiveScannerName, durationStr, options.TopFilesLimit);
        }

        return thresholdExceeded ? ExitThresholdAlert : ExitSuccess;
    }

    private static void PrintSummary(CliScanResultDto dto, string scannerName, string duration, int topLimit)
    {
        Console.WriteLine();
        Console.WriteLine($"Scanner:          {scannerName} ({duration})");
        Console.WriteLine($"Total Scanned:    {dto.FormattedTotalSize} ({dto.TotalSizeBytes:N0} bytes)");
        Console.WriteLine($"Allocated Size:   {dto.FormattedAllocatedSize} (Size on disk)");
        Console.WriteLine($"Contents:         {dto.TotalFiles:N0} files across {dto.TotalDirectories:N0} directories");

        if (dto.DriveCapacityBytes.HasValue && dto.DriveFreeBytes.HasValue && dto.DriveFreePercentage.HasValue)
        {
            Console.WriteLine($"Drive Free Space: {SizeFormatter.Format(dto.DriveFreeBytes.Value)} free ({dto.DriveFreePercentage.Value:F1}% of {SizeFormatter.Format(dto.DriveCapacityBytes.Value)})");
        }

        Console.WriteLine();
        Console.WriteLine($"--- TOP {Math.Min(topLimit, dto.TopFiles.Count)} LARGEST FILES ---");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,4}  {1,12}  {2,8}  {3,-16}  {4}", "#", "Size", "% Total", "Modified", "Path"));
        Console.WriteLine(new string('-', 78));

        int rank = 1;
        foreach (var file in dto.TopFiles.Take(topLimit))
        {
            var modStr = file.LastModified ?? "-";
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,4}  {1,12}  {2,7:F1}%  {3,-16}  {4}", rank++, file.FormattedSize, file.PercentageOfTotal, modStr, file.Path));
        }

        Console.WriteLine();
        Console.WriteLine("--- FILE TYPE BREAKDOWN (TOP 5) ---");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-8}  {1,-18}  {2,8}  {3,12}  {4,8}", "Ext", "Category", "Files", "Total Size", "% Total"));
        Console.WriteLine(new string('-', 60));

        foreach (var type in dto.FileTypeBreakdown.Take(5))
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-8}  {1,-18}  {2,8:N0}  {3,12}  {4,7:F1}%", type.Extension, type.Category, type.FileCount, type.FormattedTotalSize, type.PercentageOfTotal));
        }

        Console.WriteLine("======================================================================");

        if (dto.ThresholdExceeded)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("ALERT: THRESHOLD EXCEEDED");
            foreach (var alert in dto.ThresholdAlerts)
            {
                Console.WriteLine($" - {alert}");
            }
            Console.ResetColor();
            Console.WriteLine("Status: Threshold Alert (Exit Code 1)");
        }
        else
        {
            Console.WriteLine("Status: OK (Exit Code 0)");
        }
    }

    private static string GenerateCsv(CliScanResultDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rank,Type,Name,Path,Extension,SizeBytes,FormattedSize,PercentageOfTotal,Modified");

        int rank = 1;
        foreach (var f in dto.TopFiles)
        {
            var escapedPath = $"\"{f.Path.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            var escapedName = $"\"{f.Name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},File,{1},{2},{3},{4},{5},{6:F2},{7}", rank++, escapedName, escapedPath, f.Extension, f.SizeBytes, f.FormattedSize, f.PercentageOfTotal, f.LastModified ?? ""));
        }

        return sb.ToString();
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"Storage Visualiser CLI v0.1.1-alpha
High-speed, headless disk space analysis for Windows and RMM automations.

USAGE:
  StorageVisualiser.Cli [scan] <path> [options]

COMMANDS:
  scan <path>                  Scans the specified folder or drive (default action).

OPTIONS:
  -t, --top <n>                Number of largest files to display (default: 20).
  --json [file]                Output results as JSON. Pass '-' or omit file for stdout.
  --csv [file]                 Output top files as CSV. Pass '-' or omit file for stdout.
  --html [file]                Export self-contained interactive HTML storage report.
  -a, --allocated              Calculate allocated size on disk instead of logical file size.
  -u, --unit <windows|iec|si>  Size unit system: Windows (KB/MB), IEC (KiB/MiB), SI (kB/MB).
  --threshold-gb <n>           Return exit code 1 if scanned size exceeds N gigabytes.
  --threshold-free-percent <n> Return exit code 1 if drive free space is below N percent.
  -s, --silent                 Suppress console banners and progress text.
  -v, --version                Display application version.
  -h, --help                   Display this help screen.

EXIT CODES:
  0                            Scan completed successfully, all thresholds met.
  1                            Scan completed successfully, but threshold was exceeded.
  2                            Error occurred (invalid argument, path not found, or access error).

EXAMPLES:
  # Scan C:\ drive and display top 20 files
  StorageVisualiser.Cli C:\

  # Scan and generate an interactive HTML report
  StorageVisualiser.Cli scan D:\Data --html D:\Reports\data.html

  # Pipe structured JSON to stdout for RMM / PowerShell scripts
  StorageVisualiser.Cli C:\Logs --json - --silent

  # Alert if logs folder exceeds 10 GB (returns exit code 1 for NinjaOne / Action1)
  StorageVisualiser.Cli C:\Logs --threshold-gb 10.0");
    }
}
