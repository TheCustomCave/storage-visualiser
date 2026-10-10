using System.Globalization;
using System.Text.Json;
using StorageVisualiser.Core.Analysis;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Export;

public static class JsonReportExporter
{
    public static string ExportToJson(
        StorageNode root,
        string targetPath,
        string scanDuration = "",
        bool redactPaths = false,
        int maxTreeDepth = 5,
        int topFilesLimit = 200)
    {
        ArgumentNullException.ThrowIfNull(root);

        var topFiles = StorageAnalysisEngine.GetTopFiles(root, topFilesLimit).Select(f => new ExportTopFileDto
        {
            Name = f.Name,
            FullPath = redactPaths ? ExportSecurityHelper.RedactPath(f.FullPath) : f.FullPath,
            Extension = f.Extension,
            Size = f.Size,
            FormattedSize = f.FormattedSize,
            Percentage = f.PercentageOfTotal,
            FormattedPercentage = f.FormattedPercentage,
            Modified = f.FormattedModified
        }).ToList();

        var fileTypes = StorageAnalysisEngine.GetFileTypeBreakdown(root).Select(t => new ExportFileTypeDto
        {
            Extension = t.Extension,
            Category = t.Category,
            TotalSize = t.TotalSize,
            FormattedTotalSize = t.FormattedTotalSize,
            Percentage = t.PercentageOfTotal,
            FormattedPercentage = t.FormattedPercentage,
            FileCount = t.FileCount,
            FormattedFileCount = t.FormattedFileCount
        }).ToList();

        var displayTarget = string.IsNullOrWhiteSpace(targetPath) ? root.GetFullPath() : targetPath;
        if (redactPaths)
        {
            displayTarget = ExportSecurityHelper.RedactPath(displayTarget);
        }

        var metadata = new ReportMetadata
        {
            HostName = Environment.MachineName,
            TargetPath = displayTarget,
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            TotalSizeBytes = root.Size,
            FormattedTotalSize = SizeFormatter.Format(root.Size),
            FileCount = root.FileCount,
            DirectoryCount = root.DirectoryCount,
            ScanDuration = scanDuration
        };

        var treeDto = BuildExportTree(root, maxTreeDepth, 0, redactPaths);

        var fullData = new FullReportData
        {
            Metadata = metadata,
            TreeRoot = treeDto,
            TopFiles = topFiles,
            FileTypes = fileTypes
        };

        return JsonSerializer.Serialize(fullData, ReportJsonContext.Default.FullReportData);
    }

    private static ExportNodeDto BuildExportTree(StorageNode node, int maxDepth, int currentDepth, bool redactPaths)
    {
        var nodeName = redactPaths && currentDepth == 1 && node.Parent != null &&
                       (string.Equals(node.Parent.Name, "Users", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(node.Parent.Name, "home", StringComparison.OrdinalIgnoreCase))
            ? "<user>"
            : node.Name;

        var dto = new ExportNodeDto
        {
            Name = nodeName,
            Size = node.Size,
            Kind = (byte)node.Kind
        };

        if (currentDepth < maxDepth && node.HasChildren)
        {
            dto.Children = [];
            var sorted = node.Children.OrderByDescending(c => c.Size);
            var minFraction = currentDepth == 0 ? 0.005 : Math.Max(0.005, 0.015);
            long minThreshold = (long)(node.Size * minFraction);
            long otherSize = 0;
            int otherCount = 0;

            foreach (var child in sorted)
            {
                if (child.Kind == StorageItemKind.DriveFreeSpace)
                {
                    dto.Children.Add(new ExportNodeDto
                    {
                        Name = child.Name,
                        Size = child.Size,
                        Kind = (byte)child.Kind
                    });
                    continue;
                }

                if (child.Size >= minThreshold)
                {
                    dto.Children.Add(BuildExportTree(child, maxDepth, currentDepth + 1, redactPaths));
                }
                else
                {
                    otherSize += child.Size;
                    otherCount++;
                }
            }

            if (otherCount > 0)
            {
                dto.Children.Add(new ExportNodeDto
                {
                    Name = $"<Other ({otherCount:N0} items)>",
                    Size = otherSize,
                    Kind = 4
                });
            }
        }

        return dto;
    }
}
