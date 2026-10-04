using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Analysis;

public static class StorageAnalysisEngine
{
    public static List<TopFileItem> GetTopFiles(StorageNode root, int limit = 200)
    {
        if (root == null || limit <= 0) return [];

        var minHeap = new PriorityQueue<StorageNode, long>();
        var totalRootSize = Math.Max(1, root.Size);

        void CollectFiles(StorageNode node)
        {
            if (node.Kind == StorageItemKind.File)
            {
                if (minHeap.Count < limit)
                {
                    minHeap.Enqueue(node, node.Size);
                }
                else if (minHeap.TryPeek(out _, out var smallestInHeap) && node.Size > smallestInHeap)
                {
                    minHeap.Dequeue();
                    minHeap.Enqueue(node, node.Size);
                }
                return;
            }

            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    CollectFiles(child);
                }
            }
        }

        CollectFiles(root);

        var list = new List<StorageNode>(minHeap.Count);
        while (minHeap.Count > 0)
        {
            list.Add(minHeap.Dequeue());
        }

        // Sort descending
        list.Reverse();

        return list.Select(node =>
        {
            var ext = Path.GetExtension(node.Name);
            return new TopFileItem
            {
                Name = node.Name,
                FullPath = node.GetFullPath(),
                Extension = string.IsNullOrEmpty(ext) ? "(none)" : ext.ToLowerInvariant(),
                Size = node.Size,
                LastModified = node.LastModified,
                PercentageOfTotal = (double)node.Size / totalRootSize * 100.0,
                Node = node
            };
        }).ToList();
    }

    public static List<FileTypeSummary> GetFileTypeBreakdown(StorageNode root)
    {
        if (root == null) return [];

        var totalRootSize = Math.Max(1, root.Size);
        var map = new Dictionary<string, (long Size, int Count)>(StringComparer.OrdinalIgnoreCase);

        void CollectExtensions(StorageNode node)
        {
            if (node.Kind == StorageItemKind.File)
            {
                var rawExt = Path.GetExtension(node.Name);
                var ext = string.IsNullOrEmpty(rawExt) ? "(none)" : rawExt.ToLowerInvariant();

                if (map.TryGetValue(ext, out var val))
                {
                    map[ext] = (val.Size + node.Size, val.Count + 1);
                }
                else
                {
                    map[ext] = (node.Size, 1);
                }
                return;
            }

            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    CollectExtensions(child);
                }
            }
        }

        CollectExtensions(root);

        return map
            .Select(kv => new FileTypeSummary
            {
                Extension = kv.Key,
                Category = CategorizeExtension(kv.Key),
                TotalSize = kv.Value.Size,
                FileCount = kv.Value.Count,
                PercentageOfTotal = (double)kv.Value.Size / totalRootSize * 100.0
            })
            .OrderByDescending(f => f.TotalSize)
            .ToList();
    }

    public static string CategorizeExtension(string ext) => ext.TrimStart('.').ToLowerInvariant() switch
    {
        "vhd" or "vhdx" or "vmdk" or "iso" or "img" or "bin" or "cue" or "qcow2" => "Disk Images / VMs",
        "mp4" or "mkv" or "avi" or "mov" or "wmv" or "flv" or "webm" or "m4v" or "mpeg" or "mpg" => "Video",
        "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" or "xz" or "cab" or "wim" => "Archives",
        "mp3" or "flac" or "wav" or "aac" or "ogg" or "m4a" or "wma" => "Audio",
        "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" or "svg" or "psd" or "ai" or "tiff" or "ico" => "Images",
        "pdf" or "docx" or "doc" or "xlsx" or "xls" or "pptx" or "ppt" or "txt" or "rtf" or "csv" or "md" => "Documents",
        "exe" or "dll" or "sys" or "msi" or "drv" or "ocx" or "so" => "System / Binaries",
        "cs" or "js" or "ts" or "json" or "xml" or "html" or "css" or "py" or "cpp" or "h" or "sql" or "db" => "Code / Data",
        "(none)" => "No Extension",
        _ => "Other"
    };
}
