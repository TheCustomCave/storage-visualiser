using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StorageVisualiser.App.Models;
using StorageVisualiser.Core.Actions;
using StorageVisualiser.Core.Analysis;
using StorageVisualiser.Core.Export;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Core.Scanning;
using StorageVisualiser.Core.Treemap;
using StorageVisualiser.Windows.Shell;

namespace StorageVisualiser.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DirectoryWalkerScanner _scanner = new();
    private readonly TreemapLayoutEngine _layoutEngine = new();
    private readonly FileActionService _fileActionService;
    private string _lastScanDuration = string.Empty;

    [ObservableProperty]
    private bool _canExport;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private ObservableCollection<DriveItem> _drives = [];

    [ObservableProperty]
    private DriveItem? _selectedDrive;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Ready. Select a drive or folder to scan.";

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private string _scanProgressText = string.Empty;

    [ObservableProperty]
    private string _currentScanningDirectory = string.Empty;

    [ObservableProperty]
    private bool _isNetworkWarningVisible;

    [ObservableProperty]
    private string _networkPathToScan = string.Empty;

    private bool _isNetworkConfirmed;

    [ObservableProperty]
    private StorageNode? _rootNode;

    [ObservableProperty]
    private StorageNode? _currentViewNode;

    [ObservableProperty]
    private TreemapItem? _layoutRoot;

    [ObservableProperty]
    private StorageNode? _selectedNode;

    [ObservableProperty]
    private string _selectionDetailText = string.Empty;

    [ObservableProperty]
    private bool _canShowProperties;

    [ObservableProperty]
    private bool _canOpenFile;

    [ObservableProperty]
    private bool _canDelete;

    [ObservableProperty]
    private bool _isDeleteConfirmationVisible;

    [ObservableProperty]
    private StorageNode? _pendingDeleteNode;

    [ObservableProperty]
    private string _pendingDeleteTitle = string.Empty;

    [ObservableProperty]
    private string _pendingDeleteItemName = string.Empty;

    [ObservableProperty]
    private string _pendingDeletePath = string.Empty;

    [ObservableProperty]
    private string _pendingDeleteSize = string.Empty;

    [ObservableProperty]
    private bool _isDeleteBlocked;

    [ObservableProperty]
    private string _deleteBlockedReason = string.Empty;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private ObservableCollection<StorageNode> _treeRoots = [];

    [ObservableProperty]
    private StorageNode? _selectedTreeNode;

    partial void OnSelectedTreeNodeChanged(StorageNode? value)
    {
        if (value != null)
        {
            SelectNode(value);
        }
    }

    [ObservableProperty]
    private ObservableCollection<TopFileItem> _topFiles = [];

    [ObservableProperty]
    private TopFileItem? _selectedTopFile;

    partial void OnSelectedTopFileChanged(TopFileItem? value)
    {
        if (value?.Node != null)
        {
            SelectNode(value.Node);
        }
    }

    [ObservableProperty]
    private ObservableCollection<FileTypeSummary> _fileTypes = [];

    [ObservableProperty]
    private FileTypeSummary? _selectedFileType;

    partial void OnSelectedFileTypeChanged(FileTypeSummary? value)
    {
        if (value != null)
        {
            SelectionDetailText = $"{value.Extension} ({value.Category}): {value.FormattedTotalSize} ({value.TotalSize:N0} bytes) across {value.FormattedFileCount} files ({value.FormattedPercentage} of total)";
        }
    }

    [ObservableProperty]
    private bool _canNavigateBack;

    [ObservableProperty]
    private bool _canNavigateForward;

    [ObservableProperty]
    private bool _canNavigateUp;

    [ObservableProperty]
    private bool _showFreeSpace = true;

    [ObservableProperty]
    private int _detailLevel = 3; // 1 (Less) to 5 (More), default 3 matches SpaceMonger

    private readonly Stack<StorageNode> _backStack = new();
    private readonly Stack<StorageNode> _forwardStack = new();

    public TreemapOptions Options { get; set; } = new()
    {
        MinItemFraction = 0.005,
        MinPixelDimension = 14.0,
        MinFolderContentDimension = 34.0,
        FolderHeaderHeight = 16.0,
        BorderPadding = 1.5,
        ShowFreeSpace = true
    };

    partial void OnShowFreeSpaceChanged(bool value)
    {
        StorageNode.ShowFreeSpaceInTree = value;
        Options = Options with { ShowFreeSpace = value };
        RecomputeLayout(lastWidth, lastHeight);
        if (TreeRoots.Count > 0)
        {
            var root = TreeRoots[0];
            TreeRoots = [root];
        }
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 0)
        {
            RecomputeLayout(lastWidth, lastHeight);
        }
    }

    partial void OnDetailLevelChanged(int value)
    {
        Options = value switch
        {
            1 => Options with { MinItemFraction = 0.015, MinPixelDimension = 22.0, MinFolderContentDimension = 48.0 },
            2 => Options with { MinItemFraction = 0.008, MinPixelDimension = 18.0, MinFolderContentDimension = 40.0 },
            3 => Options with { MinItemFraction = 0.005, MinPixelDimension = 14.0, MinFolderContentDimension = 34.0 },
            4 => Options with { MinItemFraction = 0.003, MinPixelDimension = 10.0, MinFolderContentDimension = 26.0 },
            5 => Options with { MinItemFraction = 0.0015, MinPixelDimension = 6.0, MinFolderContentDimension = 18.0 },
            _ => Options
        };
        RecomputeLayout(lastWidth, lastHeight);
    }

    public MainViewModel()
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file_actions.log");
        _fileActionService = new FileActionService(new WindowsRecycleBinProvider(), logFilePath: logPath);
        RefreshDrives();
    }

    [RelayCommand]
    public void RefreshDrives()
    {
        Drives.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;

            try
            {
                Drives.Add(new DriveItem(
                    drive.Name,
                    drive.VolumeLabel,
                    drive.DriveType,
                    drive.TotalSize,
                    drive.AvailableFreeSpace));
            }
            catch
            {
                // Ignore drives that throw during query
            }
        }

        if (Drives.Count > 0 && SelectedDrive == null)
        {
            SelectedDrive = Drives[0];
        }
    }

    [RelayCommand]
    public async Task StartDriveScanAsync()
    {
        if (SelectedDrive == null) return;
        var isNetwork = SelectedDrive.DriveType == DriveType.Network;
        await RequestScanPathAsync(SelectedDrive.Name, isDriveRoot: true, isNetwork: isNetwork);
    }

    public async Task RequestScanPathAsync(string path, bool isDriveRoot, bool isNetwork)
    {
        if (isNetwork && !_isNetworkConfirmed)
        {
            NetworkPathToScan = path;
            IsNetworkWarningVisible = true;
            return;
        }

        _isNetworkConfirmed = false;
        IsNetworkWarningVisible = false;
        await StartScanPathAsync(path, isDriveRoot);
    }

    [RelayCommand]
    public async Task ConfirmNetworkScanAsync()
    {
        _isNetworkConfirmed = true;
        IsNetworkWarningVisible = false;
        if (!string.IsNullOrEmpty(NetworkPathToScan))
        {
            await StartScanPathAsync(NetworkPathToScan, isDriveRoot: true);
        }
    }

    [RelayCommand]
    public void DismissNetworkWarning()
    {
        IsNetworkWarningVisible = false;
        NetworkPathToScan = string.Empty;
    }

    public async Task StartScanPathAsync(string path, bool isDriveRoot)
    {
        if (IsScanning)
        {
            CancelScan();
        }

        _scanCts = new CancellationTokenSource();
        IsScanning = true;
        CanExport = false;
        StatusText = $"Scanning '{path}'...";
        ScanProgressText = "Starting scan...";
        CurrentScanningDirectory = path;

        DriveType driveType = DriveType.Unknown;
        long totalBytes = 0;
        long freeBytes = 0;

        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? path);
            if (driveInfo.IsReady)
            {
                driveType = driveInfo.DriveType;
                totalBytes = driveInfo.TotalSize;
                freeBytes = driveInfo.AvailableFreeSpace;
            }
        }
        catch
        {
            // Ignore drive queries on custom paths
        }

        var target = new ScanTarget
        {
            RootPath = path,
            DisplayName = path,
            DriveType = driveType,
            TotalSizeBytes = totalBytes,
            FreeSizeBytes = freeBytes,
            IsDriveRoot = isDriveRoot
        };

        if (target.IsNetwork)
        {
            StatusText = $"Scanning network location '{path}'...";
        }

        var progress = new Progress<ScanProgress>(p =>
        {
            ScanProgressText = $"{p.FilesScanned:N0} files, {p.DirectoriesScanned:N0} dirs ({SizeFormatter.Format(p.BytesScanned)}) - {p.ElapsedTime:mm\\:ss}";
            CurrentScanningDirectory = p.CurrentDirectory;
        });

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var root = await _scanner.ScanAsync(target, progress, _scanCts.Token);
            stopwatch.Stop();
            RootNode = root;
            _backStack.Clear();
            _forwardStack.Clear();
            UpdateNavigationState();

            TreeRoots = [root];
            TopFiles = new ObservableCollection<TopFileItem>(StorageAnalysisEngine.GetTopFiles(root, 200));
            FileTypes = new ObservableCollection<FileTypeSummary>(StorageAnalysisEngine.GetFileTypeBreakdown(root));

            SetViewNode(root, saveHistory: false);
            var durationStr = stopwatch.Elapsed.TotalMinutes >= 1
                ? $"{stopwatch.Elapsed:mm\\:ss}"
                : $"{stopwatch.Elapsed.TotalSeconds:F1}s";
            _lastScanDuration = durationStr;
            StatusText = $"Scan complete in {durationStr}. {root.FileCount:N0} files, {root.DirectoryCount:N0} directories ({SizeFormatter.Format(root.Size)} total).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            CanExport = RootNode != null;
            ScanProgressText = string.Empty;
            CurrentScanningDirectory = string.Empty;
        }
    }

    [RelayCommand]
    public void CancelScan()
    {
        _scanCts?.Cancel();
    }

    public void SetViewNode(StorageNode node, bool saveHistory = true)
    {
        if (CurrentViewNode != null && saveHistory)
        {
            _backStack.Push(CurrentViewNode);
            _forwardStack.Clear();
        }

        CurrentViewNode = node;
        CurrentPath = node.GetFullPath();
        UpdateNavigationState();
        SelectedNode = node;
        UpdateSelectionDetail(node);

        RecomputeLayout(lastWidth, lastHeight);
    }

    private double lastWidth = 800;
    private double lastHeight = 600;

    public void RecomputeLayout(double width, double height)
    {
        if (width > 0 && height > 0)
        {
            lastWidth = width;
            lastHeight = height;
        }

        if (CurrentViewNode == null || lastWidth <= 0 || lastHeight <= 0) return;

        var bounds = new LayoutRect(0, 0, lastWidth, lastHeight);
        LayoutRoot = _layoutEngine.ComputeLayout(CurrentViewNode, bounds, Options);
    }

    [RelayCommand]
    public void NavigateBack()
    {
        if (_backStack.Count == 0 || CurrentViewNode == null) return;
        _forwardStack.Push(CurrentViewNode);
        var target = _backStack.Pop();
        SetViewNode(target, saveHistory: false);
    }

    [RelayCommand]
    public void NavigateForward()
    {
        if (_forwardStack.Count == 0 || CurrentViewNode == null) return;
        _backStack.Push(CurrentViewNode);
        var target = _forwardStack.Pop();
        SetViewNode(target, saveHistory: false);
    }

    [RelayCommand]
    public void NavigateUp()
    {
        if (CurrentViewNode?.Parent != null)
        {
            SetViewNode(CurrentViewNode.Parent, saveHistory: true);
        }
    }

    [RelayCommand]
    public void NavigateHome()
    {
        if (RootNode != null && CurrentViewNode != RootNode)
        {
            SetViewNode(RootNode, saveHistory: true);
        }
    }

    private void UpdateNavigationState()
    {
        CanNavigateBack = _backStack.Count > 0;
        CanNavigateForward = _forwardStack.Count > 0;
        CanNavigateUp = CurrentViewNode?.Parent != null;
    }

    public void SelectNode(StorageNode? node)
    {
        SelectedNode = node;
        CanShowProperties = node != null && node.Kind != StorageItemKind.OtherGroup;
        CanOpenFile = node != null && node.Kind != StorageItemKind.OtherGroup && node.Kind != StorageItemKind.DriveFreeSpace;
        CanDelete = node != null && node.Parent != null && node.Kind != StorageItemKind.DriveFreeSpace && node.Kind != StorageItemKind.OtherGroup && node.Kind != StorageItemKind.Inaccessible;
        UpdateSelectionDetail(node);
    }

    private void UpdateSelectionDetail(StorageNode? node)
    {
        if (node == null)
        {
            SelectionDetailText = string.Empty;
            return;
        }

        var fullPath = node.GetFullPath();
        var sizeStr = SizeFormatter.Format(node.Size);
        var exactStr = SizeFormatter.Format(node.Size, exact: true);
        var modified = node.LastModified?.LocalDateTime.ToString("g", System.Globalization.CultureInfo.CurrentCulture) ?? "Unknown";

        if (node.Kind == StorageItemKind.Directory)
        {
            SelectionDetailText = $"{fullPath} | {sizeStr} ({exactStr}) | {node.FileCount:N0} files, {node.DirectoryCount:N0} subdirs | Modified: {modified}";
        }
        else if (node.Kind == StorageItemKind.DriveFreeSpace)
        {
            SelectionDetailText = $"Free Space: {sizeStr} ({exactStr})";
        }
        else if (node.Kind == StorageItemKind.OtherGroup)
        {
            SelectionDetailText = $"{node.Name}: {sizeStr} ({exactStr}) | {node.FileCount:N0} small files grouped";
        }
        else
        {
            SelectionDetailText = $"{fullPath} | {sizeStr} ({exactStr}) | Modified: {modified}";
        }
    }

    [RelayCommand]
    public void OpenInExplorer()
    {
        if (SelectedNode != null)
        {
            if (SelectedNode.Kind == StorageItemKind.DriveFreeSpace || SelectedNode.Kind == StorageItemKind.OtherGroup)
            {
                // Open the containing folder in Explorer rather than the virtual '<Other>' string
                var containingFolder = SelectedNode.Parent?.GetFullPath() ?? CurrentPath;
                WindowsShellHelper.OpenInExplorer(containingFolder);
                return;
            }
            WindowsShellHelper.OpenInExplorer(SelectedNode.GetFullPath());
        }
    }

    [RelayCommand]
    public void OpenFile()
    {
        if (SelectedNode != null)
        {
            if (SelectedNode.Kind == StorageItemKind.DriveFreeSpace || SelectedNode.Kind == StorageItemKind.OtherGroup)
            {
                return;
            }
            WindowsShellHelper.OpenFileOrFolder(SelectedNode.GetFullPath());
        }
    }

    [RelayCommand]
    public void ShowProperties()
    {
        if (SelectedNode != null)
        {
            if (SelectedNode.Kind == StorageItemKind.OtherGroup)
            {
                // OtherGroup is a virtual aggregator, not an on-disk entity
                return;
            }
            if (SelectedNode.Kind == StorageItemKind.DriveFreeSpace)
            {
                var driveRoot = SelectedNode.Parent?.GetFullPath() ?? CurrentPath;
                WindowsShellHelper.ShowFileProperties(driveRoot);
                return;
            }
            WindowsShellHelper.ShowFileProperties(SelectedNode.GetFullPath());
        }
    }

    [RelayCommand]
    public void ShowSelectedTreeNodeOnMap()
    {
        if (SelectedTreeNode != null)
        {
            if (SelectedTreeNode.Kind == StorageItemKind.File && SelectedTreeNode.Parent != null)
            {
                SetViewNode(SelectedTreeNode.Parent);
                SelectNode(SelectedTreeNode);
            }
            else
            {
                SetViewNode(SelectedTreeNode);
            }
            SelectedTabIndex = 0;
        }
    }

    [RelayCommand]
    public void ShowSelectedTopFileOnMap()
    {
        if (SelectedTopFile?.Node != null)
        {
            var node = SelectedTopFile.Node;
            if (node.Parent != null)
            {
                SetViewNode(node.Parent);
                SelectNode(node);
            }
            else
            {
                SetViewNode(node);
            }
            SelectedTabIndex = 0;
        }
    }

    [RelayCommand]
    public void RequestDelete()
    {
        if (SelectedNode == null) return;

        PendingDeleteNode = SelectedNode;
        PendingDeleteItemName = SelectedNode.Name;
        PendingDeletePath = SelectedNode.GetFullPath();
        PendingDeleteSize = $"{SizeFormatter.Format(SelectedNode.Size)} ({SizeFormatter.Format(SelectedNode.Size, exact: true)})";

        if (!_fileActionService.CanDelete(SelectedNode, out var reason))
        {
            IsDeleteBlocked = true;
            DeleteBlockedReason = reason;
            PendingDeleteTitle = "Protected Item - Deletion Blocked";
        }
        else
        {
            IsDeleteBlocked = false;
            DeleteBlockedReason = string.Empty;
            PendingDeleteTitle = SelectedNode.Kind == StorageItemKind.Directory
                ? "Move Directory to Recycle Bin?"
                : "Move File to Recycle Bin?";
        }

        IsDeleteConfirmationVisible = true;
    }

    [RelayCommand]
    public void CancelDelete()
    {
        IsDeleteConfirmationVisible = false;
        PendingDeleteNode = null;
    }

    [RelayCommand]
    public void ConfirmDelete()
    {
        if (PendingDeleteNode == null || IsDeleteBlocked)
        {
            IsDeleteConfirmationVisible = false;
            return;
        }

        var nodeToDelete = PendingDeleteNode;
        IsDeleteConfirmationVisible = false;

        var result = _fileActionService.DeleteToRecycleBin(nodeToDelete);
        if (result.Success)
        {
            StatusText = $"Moved '{nodeToDelete.Name}' ({SizeFormatter.Format(nodeToDelete.Size)}) to Recycle Bin.";

            // Remove from parent in memory
            var parent = nodeToDelete.Parent;
            if (parent != null)
            {
                parent.Children.Remove(nodeToDelete);

                // Deduct size up the ancestor tree
                var current = parent;
                while (current != null)
                {
                    current.Size = Math.Max(0, current.Size - nodeToDelete.Size);
                    if (nodeToDelete.Kind == StorageItemKind.Directory)
                    {
                        current.DirectoryCount = Math.Max(0, current.DirectoryCount - 1 - nodeToDelete.DirectoryCount);
                        current.FileCount = Math.Max(0, current.FileCount - nodeToDelete.FileCount);
                    }
                    else
                    {
                        current.FileCount = Math.Max(0, current.FileCount - 1);
                    }
                    current = current.Parent;
                }

                // Refresh views
                if (CurrentViewNode == nodeToDelete)
                {
                    SetViewNode(parent);
                }
                else
                {
                    RecomputeLayout(lastWidth, lastHeight);
                }

                // Refresh tree
                if (TreeRoots.Count > 0)
                {
                    var r = TreeRoots[0];
                    TreeRoots = [r];
                }

                // Remove from TopFiles if present
                var topMatch = System.Linq.Enumerable.FirstOrDefault(TopFiles, t => t.Node == nodeToDelete);
                if (topMatch != null)
                {
                    TopFiles.Remove(topMatch);
                }
            }
        }
        else
        {
            StatusText = $"Failed to move '{nodeToDelete.Name}' to Recycle Bin: {result.ErrorMessage}";
        }

        PendingDeleteNode = null;
    }

    public async Task ExportHtmlReportAsync(string targetFilePath)
    {
        if (RootNode == null) return;

        try
        {
            StatusText = "Exporting HTML report...";
            var root = RootNode;
            var path = CurrentPath;
            var duration = _lastScanDuration;

            var html = await Task.Run(() => HtmlReportExporter.ExportToHtml(
                root,
                path,
                scanDuration: duration,
                maxTreeDepth: 6));

            await File.WriteAllTextAsync(targetFilePath, html, System.Text.Encoding.UTF8);
            var fileName = Path.GetFileName(targetFilePath);
            StatusText = $"HTML report exported successfully: {fileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to export HTML report: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _scanCts?.Dispose();
    }
}
