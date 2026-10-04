using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    private readonly Stack<StorageNode> _backStack = new();
    private readonly Stack<StorageNode> _forwardStack = new();

    public TreemapOptions Options { get; set; } = new()
    {
        MinItemFraction = 0.002, // 0.2%
        FolderHeaderHeight = 18.0,
        BorderPadding = 2.0
    };

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
        await StartScanPathAsync(SelectedDrive.Name, isDriveRoot: true);
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
            StatusText = $"Scanning network location '{path}' (this may take longer)...";
        }

        var progress = new Progress<ScanProgress>(p =>
        {
            ScanProgressText = $"{p.FilesScanned:N0} files, {p.DirectoriesScanned:N0} dirs ({SizeFormatter.Format(p.BytesScanned)}) - {p.ElapsedTime:mm\\:ss}";
        });

        try
        {
            var root = await _scanner.ScanAsync(target, progress, _scanCts.Token);
            RootNode = root;
            _backStack.Clear();
            _forwardStack.Clear();
            UpdateNavigationState();

            SetViewNode(root, saveHistory: false);
            StatusText = $"Scan complete. {root.FileCount:N0} files, {root.DirectoryCount:N0} directories ({SizeFormatter.Format(root.Size)} total).";
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
            WindowsShellHelper.OpenInExplorer(SelectedNode.GetFullPath());
        }
    }

    [RelayCommand]
    public void OpenFile()
    {
        if (SelectedNode != null)
        {
            WindowsShellHelper.OpenFileOrFolder(SelectedNode.GetFullPath());
        }
    }

    [RelayCommand]
    public void ShowProperties()
    {
        if (SelectedNode != null)
        {
            WindowsShellHelper.ShowFileProperties(SelectedNode.GetFullPath());
        }
    }

    public void Dispose()
    {
        _scanCts?.Dispose();
    }
}
