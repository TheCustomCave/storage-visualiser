using System.Globalization;
using System.Text;
using StorageVisualiser.Core.Analysis;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Export;

public static class CsvReportExporter
{
    public static string ExportTopFilesToCsv(StorageNode root, bool redactPaths = false, int limit = 1000)
    {
        ArgumentNullException.ThrowIfNull(root);

        var topFiles = StorageAnalysisEngine.GetTopFiles(root, limit);
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Name,FullPath,Extension,SizeBytes,FormattedSize,PercentageOfTotal,LastModified");

        foreach (var file in topFiles)
        {
            var path = redactPaths ? ExportSecurityHelper.RedactPath(file.FullPath) : file.FullPath;
            sb.Append(ExportSecurityHelper.EscapeCsvField(file.Name)).Append(',')
              .Append(ExportSecurityHelper.EscapeCsvField(path)).Append(',')
              .Append(ExportSecurityHelper.EscapeCsvField(file.Extension)).Append(',')
              .Append(file.Size.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(ExportSecurityHelper.EscapeCsvField(file.FormattedSize)).Append(',')
              .Append(file.PercentageOfTotal.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
              .Append(ExportSecurityHelper.EscapeCsvField(file.FormattedModified))
              .AppendLine();
        }

        return sb.ToString();
    }

    public static string ExportFileTypesToCsv(StorageNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var fileTypes = StorageAnalysisEngine.GetFileTypeBreakdown(root);
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Extension,Category,TotalSizeBytes,FormattedTotalSize,PercentageOfTotal,FileCount");

        foreach (var type in fileTypes)
        {
            sb.Append(ExportSecurityHelper.EscapeCsvField(type.Extension)).Append(',')
              .Append(ExportSecurityHelper.EscapeCsvField(type.Category)).Append(',')
              .Append(type.TotalSize.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(ExportSecurityHelper.EscapeCsvField(type.FormattedTotalSize)).Append(',')
              .Append(type.PercentageOfTotal.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
              .Append(type.FileCount.ToString(CultureInfo.InvariantCulture))
              .AppendLine();
        }

        return sb.ToString();
    }
}
