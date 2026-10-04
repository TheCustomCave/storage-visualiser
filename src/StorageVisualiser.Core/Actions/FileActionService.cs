using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.Core.Actions;

public sealed class FileActionService
{
    private readonly IRecycleBinProvider _recycleBinProvider;
    private readonly string? _logFilePath;
    private readonly HashSet<string> _builtInProtectedPaths = new(StringComparer.OrdinalIgnoreCase);

    public FileActionPolicy Policy { get; set; }

    public FileActionService(
        IRecycleBinProvider recycleBinProvider,
        FileActionPolicy? policy = null,
        string? logFilePath = null)
    {
        _recycleBinProvider = recycleBinProvider ?? throw new ArgumentNullException(nameof(recycleBinProvider));
        Policy = policy ?? new FileActionPolicy();
        _logFilePath = logFilePath;

        InitializeDefaultProtectedPaths();
    }

    private void InitializeDefaultProtectedPaths()
    {
        // System paths
        AddPathIfExists(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        AddPathIfExists(Environment.GetFolderPath(Environment.SpecialFolder.System));
        AddPathIfExists(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
        AddPathIfExists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddPathIfExists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddPathIfExists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)); // C:\ProgramData
        AddPathIfExists(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); // C:\Users\<Username> root itself
    }

    private void AddPathIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            _builtInProtectedPaths.Add(NormalizePath(path));
        }
    }

    public static string NormalizePath(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        return trimmed;
    }

    public bool CanDelete(StorageNode? node, out string reason)
    {
        if (node == null)
        {
            reason = "No item selected.";
            return false;
        }

        if (!Policy.IsDeleteAllowed)
        {
            reason = "File deletion is disabled by policy.";
            return false;
        }

        if (node.Kind == StorageItemKind.DriveFreeSpace)
        {
            reason = "Free space cannot be deleted.";
            return false;
        }

        if (node.Kind == StorageItemKind.OtherGroup)
        {
            reason = "'Other' is a virtual group of items, not an individual file or folder.";
            return false;
        }

        if (node.Kind == StorageItemKind.Inaccessible)
        {
            reason = "Inaccessible item cannot be deleted.";
            return false;
        }

        if (node.Parent == null)
        {
            reason = "The root of a drive cannot be deleted.";
            return false;
        }

        var fullPath = node.GetFullPath();
        var normalized = NormalizePath(fullPath);

        // Check if root drive
        if (string.IsNullOrEmpty(normalized) || normalized.Length <= 3 && normalized.EndsWith(':'))
        {
            reason = "Drive root cannot be deleted.";
            return false;
        }

        // Check critical system files by name
        var fileName = node.Name.ToLowerInvariant();
        if (fileName is "pagefile.sys" or "hiberfil.sys" or "swapfile.sys" or "dumpstack.log"
            or "$recycle.bin" or "system volume information" or "bootmgr" or "boot" or "efi")
        {
            reason = $"'{node.Name}' is a protected Windows system file.";
            return false;
        }

        // Check built-in protected paths
        foreach (var protectedPath in _builtInProtectedPaths)
        {
            if (string.Equals(normalized, protectedPath, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"'{node.Name}' is a protected system directory.";
                return false;
            }

            // Also protect direct Windows directory contents from accidental deletion
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(winDir) && normalized.StartsWith(NormalizePath(winDir) + "\\", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Items inside the Windows folder are protected.";
                return false;
            }
        }

        // Check additional policy protected paths
        foreach (var additional in Policy.AdditionalProtectedPaths)
        {
            var normAdditional = NormalizePath(additional);
            if (string.Equals(normalized, normAdditional, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(normAdditional + "\\", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Path is protected by organisation policy.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    public FileActionResult DeleteToRecycleBin(StorageNode node)
    {
        if (!CanDelete(node, out var reason))
        {
            var blockedResult = new FileActionResult
            {
                Success = false,
                ErrorMessage = reason,
                Path = node?.GetFullPath() ?? string.Empty,
                Size = node?.Size ?? 0
            };
            LogAction("DELETE_RECYCLE_BIN_BLOCKED", blockedResult);
            return blockedResult;
        }

        var fullPath = node.GetFullPath();
        var success = _recycleBinProvider.SendToRecycleBin(fullPath, out var error);

        var result = new FileActionResult
        {
            Success = success,
            ErrorMessage = error,
            Path = fullPath,
            Size = node.Size
        };

        LogAction(success ? "DELETE_RECYCLE_BIN_SUCCESS" : "DELETE_RECYCLE_BIN_FAILED", result);
        return result;
    }

    private void LogAction(string actionType, FileActionResult result)
    {
        if (string.IsNullOrEmpty(_logFilePath)) return;

        try
        {
            var logDir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var line = string.Format(
                CultureInfo.InvariantCulture,
                "[{0:yyyy-MM-dd HH:mm:ss 'UTC'}] [{1}] Path=\"{2}\" Size={3} ({4}) Result={5} Error=\"{6}\"",
                result.Timestamp,
                actionType,
                result.Path,
                result.Size,
                SizeFormatter.Format(result.Size),
                result.Success ? "Success" : "Failed",
                result.ErrorMessage ?? string.Empty);

            File.AppendAllLines(_logFilePath, [line]);
        }
        catch
        {
            // Logging failure should never crash the host application
        }
    }
}
