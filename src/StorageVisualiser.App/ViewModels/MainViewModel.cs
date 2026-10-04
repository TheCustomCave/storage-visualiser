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
        Options = Options with { ShowFreeSpace = value };
        RecomputeLayout(lastWidth, lastHeight);
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

            SetViewNode(root, saveHistory: false);
            var durationStr = stopwatch.Elapsed.TotalMinutes >= 1
                ? $"{stopwatch.Elapsed:mm\\:ss}"
                : $"{stopwatch.Elapsed.TotalSeconds:F1}s";
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
        if (width <= 0 || height <= 0 || CurrentViewNode == null) return;
        lastWidth = width;
        lastHeight = height;

        var bounds = new LayoutRect(0, 0, width, height);
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
            if (SelectedNode.Kind == StorageItemKind.DriveFreeSpace)
            {
                // Free space is not a file/folder on disk; open the drive root instead!
                var driveRoot = SelectedNode.Parent?.GetFullPath() ?? CurrentPath;
                WindowsShellHelper.OpenInExplorer(driveRoot);
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
            if (SelectedNode.Kind == StorageItemKind.DriveFreeSpace)
            {
                var driveRoot = SelectedNode.Parent?.GetFullPath() ?? CurrentPath;
                WindowsShellHelper.ShowFileProperties(driveRoot);
                return;
            }
            WindowsShellHelper.ShowFileProperties(SelectedNode.GetFullPath());
        }
    }

    public void Dispose()
    {
        _scanCts?.Dispose();
    }
}
