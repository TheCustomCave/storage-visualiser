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
using StorageVisualiser.Core.Policy;
using StorageVisualiser.Core.Scanning;
using StorageVisualiser.Core.Settings;
using StorageVisualiser.Core.Treemap;
using StorageVisualiser.Windows.Scanning;
using StorageVisualiser.Windows.Shell;

namespace StorageVisualiser.App.ViewModels;

public sealed record ColorModeOption(TreemapColorMode Value, string DisplayName);
public sealed record UnitSystemOption(UnitSystem Value, string DisplayName);
public sealed record LayoutBiasOption(TreemapBias Value, string DisplayName);
public sealed record LegendItem(string Name, string ColorHex);

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly PolicyService _policyService = new();
    private readonly WindowsAutoScanner _scanner = new();
    private readonly TreemapLayoutEngine _layoutEngine = new();
    private readonly FileActionService _fileActionService;
    private string _lastScanDuration = string.Empty;

    public bool IsPolicyActive => _policyService.IsPolicyLoaded && _policyService.ActivePolicy.IsEnforced;
    public string PolicyStatusMessage => _policyService.PolicyError != null
        ? $"Organisation Policy (Fail-Safe Enforced): {_policyService.PolicyError}"
        : $"Managed by your organisation ({Path.GetFileName(_policyService.LoadedPolicySource ?? "policy.json")})";

    public bool IsDeleteDisabledByPolicy => _policyService.ActivePolicy.DeleteMode == StorageVisualiser.Core.Policy.PolicyDeleteMode.Disabled;
    public bool IsExportRedactionLockedByPolicy => _policyService.ActivePolicy.RedactPathsInExports.HasValue;

    public IReadOnlyList<ColorModeOption> AvailableColorModes { get; } =
    [
        new(TreemapColorMode.DepthRainbow, "Folder Depth (Rainbow)"),
        new(TreemapColorMode.FileTypeCategory, "File Type Category (Media, Code, Docs...)"),
        new(TreemapColorMode.FileAge, "File Age (Recency Tiers)")
    ];

    public IReadOnlyList<UnitSystemOption> AvailableUnitSystems { get; } =
    [
        new(UnitSystem.Windows, "Windows Binary (1024 base: KB, MB, GB)"),
        new(UnitSystem.Iec, "IEC Standard (1024 base: KiB, MiB, GiB)"),
        new(UnitSystem.Si, "Metric / SI (1000 base: kB, MB, GB)")
    ];

    public IReadOnlyList<LayoutBiasOption> AvailableLayoutBiases { get; } =
    [
        new(TreemapBias.Equal, "Balanced (Squarified)"),
        new(TreemapBias.Horizontal, "Favor Horizontal (Wider Boxes)"),
        new(TreemapBias.Vertical, "Favor Vertical (Taller Boxes)")
    ];

    [ObservableProperty]
    private TreemapColorMode _colorMode = TreemapColorMode.DepthRainbow;

    partial void OnColorModeChanged(TreemapColorMode value)
    {
        OnPropertyChanged(nameof(IsLegendVisible));
        OnPropertyChanged(nameof(CurrentLegendItems));
    }

    [ObservableProperty]
    private bool _colorBlindSafe;

    partial void OnColorBlindSafeChanged(bool value)
    {
        OnPropertyChanged(nameof(CurrentLegendItems));
    }

    public bool IsLegendVisible => ColorMode != TreemapColorMode.DepthRainbow;

    public IReadOnlyList<LegendItem> CurrentLegendItems
    {
        get
        {
            if (ColorMode == TreemapColorMode.FileTypeCategory)
            {
                return ColorBlindSafe
                    ? [
                        new("Video", "#F3E8FF"),
                        new("Images", "#CCFBF1"),
                        new("Audio", "#BAE6FD"),
                        new("Documents", "#DBEAFE"),
                        new("Archives", "#FEF3C7"),
                        new("Binaries", "#FFE4E6"),
                        new("Disk Images", "#E0E7FF"),
                        new("Code/Data", "#FEF9C3"),
                        new("Other", "#E2E8F0")
                      ]
                    : [
                        new("Video", "#DDD6FE"),
                        new("Images", "#A7F3D0"),
                        new("Audio", "#A5F3FC"),
                        new("Documents", "#BFDBFE"),
                        new("Archives", "#FDE68A"),
                        new("Binaries", "#FECACA"),
                        new("Disk Images", "#C7D2FE"),
                        new("Code/Data", "#99F6E4"),
                        new("Other", "#CBD5E1")
                      ];
            }

            if (ColorMode == TreemapColorMode.FileAge)
            {
                return ColorBlindSafe
                    ? [
                        new("< 1 mo", "#E0F2FE"),
                        new("1–6 mo", "#BAE6FD"),
                        new("6–12 mo", "#FEF3C7"),
                        new("1–2 yr", "#FDE68A"),
                        new("> 2 yr", "#CBD5E1")
                      ]
                    : [
                        new("< 1 mo", "#BAE6FD"),
                        new("1–6 mo", "#BBF7D0"),
                        new("6–12 mo", "#FEF08A"),
                        new("1–2 yr", "#FED7AA"),
                        new("> 2 yr", "#CBD5E1")
                      ];
            }

            return [];
        }
    }

    [ObservableProperty]
    private UnitSystem _unitSystem = UnitSystem.Windows;

    [ObservableProperty]
    private bool _useAllocatedSize;

    [ObservableProperty]
    private TreemapBias _layoutBias = TreemapBias.Equal;

    [ObservableProperty]
    private bool _confirmBeforeDelete = true;

    [ObservableProperty]
    private bool _autoRescanAfterDelete = true;

    [ObservableProperty]
    private bool _redactPathsInExports;

    // Settings Dialog Working State
    [ObservableProperty]
    private bool _isSettingsVisible;

    [ObservableProperty]
    private ColorModeOption? _selectedColorModeOption;

    [ObservableProperty]
    private bool _settingsColorBlindSafe;

    [ObservableProperty]
    private UnitSystemOption? _selectedUnitSystemOption;

    [ObservableProperty]
    private bool _settingsUseAllocatedSize;

    [ObservableProperty]
    private LayoutBiasOption? _selectedLayoutBiasOption;

    [ObservableProperty]
    private int _settingsDetailLevel = 3;

    [ObservableProperty]
    private bool _settingsShowFreeSpace = true;

    [ObservableProperty]
    private bool _settingsTreePercentageRelativeToTotal = true;

    [ObservableProperty]
    private bool _settingsConfirmBeforeDelete = true;

    [ObservableProperty]
    private bool _settingsAutoRescanAfterDelete = true;

    [ObservableProperty]
    private bool _settingsRedactPathsInExports;

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
    private bool _canOpenInExplorer;

    [ObservableProperty]
    private bool _canCopyPath;

    [ObservableProperty]
    private bool _canDrillDownSelected;

    [ObservableProperty]
    private bool _canShowProperties;

    [ObservableProperty]
    private bool _canOpenFile;

    [ObservableProperty]
    private bool _canDelete;

    [ObservableProperty]
    private bool _treePercentageRelativeToTotal = true;

    [ObservableProperty]
    private bool _treePercentageRelativeToParent;

    partial void OnTreePercentageRelativeToTotalChanged(bool value)
    {
        if (value)
        {
            _treePercentageRelativeToParent = false;
            OnPropertyChanged(nameof(TreePercentageRelativeToParent));
            StorageNode.TreePercentageRelativeToTotal = true;
            RootNode?.NotifyPercentageChanged();
            _settingsService.Save(_settingsService.Current with { TreePercentageRelativeToTotal = true });
        }
    }

    partial void OnTreePercentageRelativeToParentChanged(bool value)
    {
        if (value)
        {
            _treePercentageRelativeToTotal = false;
            OnPropertyChanged(nameof(TreePercentageRelativeToTotal));
            StorageNode.TreePercentageRelativeToTotal = false;
            RootNode?.NotifyPercentageChanged();
            _settingsService.Save(_settingsService.Current with { TreePercentageRelativeToTotal = false });
        }
    }

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
    private string? _activeExtensionFilter;

    [ObservableProperty]
    private bool _hasActiveFilter;

    [ObservableProperty]
    private string _activeFilterDescription = string.Empty;

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
        _settingsService.Save(_settingsService.Current with { ShowFreeSpace = value });
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
        _settingsService.Save(_settingsService.Current with { DefaultDetailLevel = value });
    }

    public MainViewModel()
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file_actions.log");
        var fileActionPolicy = new FileActionPolicy();

        // Apply Policy to FileActionService
        if (_policyService.ActivePolicy.DeleteMode.HasValue)
        {
            fileActionPolicy.Mode = _policyService.ActivePolicy.DeleteMode.Value switch
            {
                PolicyDeleteMode.Disabled => DeleteMode.Disabled,
                PolicyDeleteMode.AllowPermanent => DeleteMode.AllowPermanent,
                _ => DeleteMode.RecycleBinOnly
            };
        }

        if (_policyService.ActivePolicy.ProtectedPaths != null)
        {
            fileActionPolicy.AdditionalProtectedPaths = _policyService.ActivePolicy.ProtectedPaths;
            fileActionPolicy.ReplaceBuiltInProtectedPaths = _policyService.ActivePolicy.ProtectedPathsMode == ProtectedPathsMode.Replace;
        }

        _fileActionService = new FileActionService(new WindowsRecycleBinProvider(), fileActionPolicy, logFilePath: logPath);

        var s = _settingsService.Current;
        _colorMode = s.ColorMode;
        _colorBlindSafe = s.ColorBlindSafe;
        _unitSystem = s.UnitSystem;
        SizeFormatter.DefaultUnitSystem = s.UnitSystem;
        _useAllocatedSize = s.UseAllocatedSize;
        _layoutBias = s.LayoutBias;
        _showFreeSpace = s.ShowFreeSpace;
        _detailLevel = Math.Clamp(s.DefaultDetailLevel, 1, 5);
        _treePercentageRelativeToTotal = s.TreePercentageRelativeToTotal;
        _treePercentageRelativeToParent = !s.TreePercentageRelativeToTotal;
        StorageNode.TreePercentageRelativeToTotal = s.TreePercentageRelativeToTotal;
        StorageNode.ShowFreeSpaceInTree = s.ShowFreeSpace;
        _confirmBeforeDelete = s.ConfirmBeforeDelete;
        _autoRescanAfterDelete = s.AutoRescanAfterDelete;
        _redactPathsInExports = _policyService.ActivePolicy.RedactPathsInExports ?? s.RedactPathsInExports;

        Options = (_detailLevel switch
        {
            1 => Options with { MinItemFraction = 0.015, MinPixelDimension = 22.0, MinFolderContentDimension = 48.0 },
            2 => Options with { MinItemFraction = 0.008, MinPixelDimension = 18.0, MinFolderContentDimension = 40.0 },
            3 => Options with { MinItemFraction = 0.005, MinPixelDimension = 14.0, MinFolderContentDimension = 34.0 },
            4 => Options with { MinItemFraction = 0.003, MinPixelDimension = 10.0, MinFolderContentDimension = 26.0 },
            5 => Options with { MinItemFraction = 0.0015, MinPixelDimension = 6.0, MinFolderContentDimension = 18.0 },
            _ => Options
        }) with
        {
            UseAllocatedSize = s.UseAllocatedSize,
            Bias = s.LayoutBias,
            ShowFreeSpace = s.ShowFreeSpace
        };

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

    [RelayCommand]
    public async Task Rescan()
    {
        if (IsScanning) return;

        if (RootNode != null)
        {
            var rootPath = RootNode.GetFullPath();
            var isDrive = SelectedDrive != null && rootPath.Equals(SelectedDrive.Name, StringComparison.OrdinalIgnoreCase);
            await StartScanPathAsync(rootPath, isDriveRoot: isDrive);
        }
        else if (SelectedDrive != null)
        {
            await StartDriveScanAsync();
        }
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
        ActiveExtensionFilter = null;
        HasActiveFilter = false;
        ActiveFilterDescription = string.Empty;
        StorageNode.ActiveExtensionFilter = null;
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

            StorageNode.RootTotalSize = root.Size;
            StorageNode.RootDriveCapacity = target.TotalSizeBytes;
            StorageNode.RootDriveUsedBytes = (target.TotalSizeBytes > 0 && target.FreeSizeBytes > 0)
                ? target.TotalSizeBytes - target.FreeSizeBytes
                : root.Size;
            StorageNode.IsRootDrive = target.IsDriveRoot;

            TreeRoots = [root];
            TopFiles = new ObservableCollection<TopFileItem>(StorageAnalysisEngine.GetTopFiles(root, 200));
            FileTypes = new ObservableCollection<FileTypeSummary>(StorageAnalysisEngine.GetFileTypeBreakdown(root));

            SetViewNode(root, saveHistory: false);
            var durationStr = stopwatch.Elapsed.TotalMinutes >= 1
                ? $"{stopwatch.Elapsed:mm\\:ss}"
                : $"{stopwatch.Elapsed.TotalSeconds:F1}s";
            _lastScanDuration = durationStr;
            StatusText = $"Scan complete in {durationStr} via {_scanner.ActiveScannerName}. {root.FileCount:N0} files, {root.DirectoryCount:N0} directories ({SizeFormatter.Format(root.Size)} total).";
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
        SelectNode(node);

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
        CanOpenInExplorer = node != null && node.Kind != StorageItemKind.OtherGroup && node.Kind != StorageItemKind.DriveFreeSpace;
        CanCopyPath = node != null && node.Kind != StorageItemKind.OtherGroup;
        CanShowProperties = node != null && node.Kind != StorageItemKind.OtherGroup;
        CanOpenFile = node != null && node.Kind != StorageItemKind.OtherGroup && node.Kind != StorageItemKind.DriveFreeSpace;
        CanDelete = node != null && node.Parent != null && node.Kind != StorageItemKind.DriveFreeSpace && node.Kind != StorageItemKind.OtherGroup && node.Kind != StorageItemKind.Inaccessible;
        CanDrillDownSelected = node != null && (node.Kind == StorageItemKind.Directory || node.Kind == StorageItemKind.OtherGroup) && node.HasChildren && node != CurrentViewNode;
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
    public void DrillDownSelected()
    {
        if (SelectedNode != null && CanDrillDownSelected)
        {
            SetViewNode(SelectedNode);
        }
    }

    [RelayCommand]
    public void OpenInExplorer()
    {
        if (SelectedNode != null && CanOpenInExplorer)
        {
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
    public async Task RequestDelete()
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
            IsDeleteConfirmationVisible = true;
            return;
        }

        IsDeleteBlocked = false;
        DeleteBlockedReason = string.Empty;
        PendingDeleteTitle = SelectedNode.Kind == StorageItemKind.Directory
            ? "Move Directory to Recycle Bin?"
            : "Move File to Recycle Bin?";

        if (!ConfirmBeforeDelete)
        {
            await ConfirmDelete();
            return;
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
    public async Task ConfirmDelete()
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

            if (AutoRescanAfterDelete && !IsScanning && !string.IsNullOrEmpty(CurrentPath))
            {
                await Rescan();
            }
        }
        else
        {
            StatusText = $"Failed to move '{nodeToDelete.Name}' to Recycle Bin: {result.ErrorMessage}";
        }

        PendingDeleteNode = null;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        SelectedColorModeOption = System.Linq.Enumerable.FirstOrDefault(AvailableColorModes, o => o.Value == ColorMode) ?? AvailableColorModes[0];
        SettingsColorBlindSafe = ColorBlindSafe;
        SelectedUnitSystemOption = System.Linq.Enumerable.FirstOrDefault(AvailableUnitSystems, o => o.Value == UnitSystem) ?? AvailableUnitSystems[0];
        SettingsUseAllocatedSize = UseAllocatedSize;
        SelectedLayoutBiasOption = System.Linq.Enumerable.FirstOrDefault(AvailableLayoutBiases, o => o.Value == LayoutBias) ?? AvailableLayoutBiases[0];
        SettingsDetailLevel = DetailLevel;
        SettingsShowFreeSpace = ShowFreeSpace;
        SettingsTreePercentageRelativeToTotal = TreePercentageRelativeToTotal;
        SettingsConfirmBeforeDelete = ConfirmBeforeDelete;
        SettingsAutoRescanAfterDelete = AutoRescanAfterDelete;
        SettingsRedactPathsInExports = RedactPathsInExports;

        IsSettingsVisible = true;
    }

    [RelayCommand]
    public void CloseSettings()
    {
        IsSettingsVisible = false;
    }

    [RelayCommand]
    public void ResetSettingsToDefaults()
    {
        var def = new AppSettings();
        SelectedColorModeOption = System.Linq.Enumerable.FirstOrDefault(AvailableColorModes, o => o.Value == def.ColorMode) ?? AvailableColorModes[0];
        SettingsColorBlindSafe = def.ColorBlindSafe;
        SelectedUnitSystemOption = System.Linq.Enumerable.FirstOrDefault(AvailableUnitSystems, o => o.Value == def.UnitSystem) ?? AvailableUnitSystems[0];
        SettingsUseAllocatedSize = def.UseAllocatedSize;
        SelectedLayoutBiasOption = System.Linq.Enumerable.FirstOrDefault(AvailableLayoutBiases, o => o.Value == def.LayoutBias) ?? AvailableLayoutBiases[0];
        SettingsDetailLevel = def.DefaultDetailLevel;
        SettingsShowFreeSpace = def.ShowFreeSpace;
        SettingsTreePercentageRelativeToTotal = def.TreePercentageRelativeToTotal;
        SettingsConfirmBeforeDelete = def.ConfirmBeforeDelete;
        SettingsAutoRescanAfterDelete = def.AutoRescanAfterDelete;
        SettingsRedactPathsInExports = _policyService.ActivePolicy.RedactPathsInExports ?? def.RedactPathsInExports;
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var effectiveRedaction = _policyService.ActivePolicy.RedactPathsInExports ?? SettingsRedactPathsInExports;

        var updated = new AppSettings
        {
            ColorMode = SelectedColorModeOption?.Value ?? TreemapColorMode.DepthRainbow,
            ColorBlindSafe = SettingsColorBlindSafe,
            UnitSystem = SelectedUnitSystemOption?.Value ?? UnitSystem.Windows,
            UseAllocatedSize = SettingsUseAllocatedSize,
            DefaultDetailLevel = SettingsDetailLevel,
            LayoutBias = SelectedLayoutBiasOption?.Value ?? TreemapBias.Equal,
            ShowFreeSpace = SettingsShowFreeSpace,
            TreePercentageRelativeToTotal = SettingsTreePercentageRelativeToTotal,
            ConfirmBeforeDelete = SettingsConfirmBeforeDelete,
            AutoRescanAfterDelete = SettingsAutoRescanAfterDelete,
            RedactPathsInExports = effectiveRedaction
        };

        _settingsService.Save(updated);

        // Apply active properties
        ColorMode = updated.ColorMode;
        ColorBlindSafe = updated.ColorBlindSafe;
        UnitSystem = updated.UnitSystem;
        SizeFormatter.DefaultUnitSystem = updated.UnitSystem;
        UseAllocatedSize = updated.UseAllocatedSize;
        LayoutBias = updated.LayoutBias;
        ShowFreeSpace = updated.ShowFreeSpace;
        DetailLevel = updated.DefaultDetailLevel;
        ConfirmBeforeDelete = updated.ConfirmBeforeDelete;
        AutoRescanAfterDelete = updated.AutoRescanAfterDelete;
        RedactPathsInExports = updated.RedactPathsInExports;

        TreePercentageRelativeToTotal = updated.TreePercentageRelativeToTotal;
        TreePercentageRelativeToParent = !updated.TreePercentageRelativeToTotal;
        StorageNode.TreePercentageRelativeToTotal = updated.TreePercentageRelativeToTotal;
        StorageNode.ShowFreeSpaceInTree = updated.ShowFreeSpace;

        Options = Options with
        {
            UseAllocatedSize = updated.UseAllocatedSize,
            Bias = updated.LayoutBias,
            ShowFreeSpace = updated.ShowFreeSpace
        };

        // Recompute treemap layout and refresh node formatting
        RecomputeLayout(lastWidth, lastHeight);
        RootNode?.NotifyFormattingChanged();
        RootNode?.NotifyPercentageChanged();

        if (SelectedNode != null)
        {
            SelectNode(SelectedNode);
        }

        RefreshDrives();
        IsSettingsVisible = false;
    }

    [RelayCommand]
    public void FilterByExtension(string? extension)
    {
        if (RootNode == null) return;

        if (string.IsNullOrWhiteSpace(extension))
        {
            ActiveExtensionFilter = null;
            HasActiveFilter = false;
            ActiveFilterDescription = string.Empty;
            StorageNode.ActiveExtensionFilter = null;
            StorageNode.UpdateFilterMatching(RootNode, null);
            TopFiles = new ObservableCollection<TopFileItem>(StorageAnalysisEngine.GetTopFiles(RootNode, 200));
            StatusText = "Cleared file type filter.";
        }
        else
        {
            var filterNorm = extension.StartsWith('.') ? extension.ToLowerInvariant() : (extension == "(none)" ? "(none)" : "." + extension.ToLowerInvariant());
            ActiveExtensionFilter = filterNorm;
            HasActiveFilter = true;
            StorageNode.ActiveExtensionFilter = filterNorm;
            var res = StorageNode.UpdateFilterMatching(RootNode, filterNorm);
            ActiveFilterDescription = $"{filterNorm} ({res.MatchingCount:N0} files, {SizeFormatter.Format(res.MatchingSize)})";
            TopFiles = new ObservableCollection<TopFileItem>(StorageAnalysisEngine.GetTopFiles(RootNode, 200, filterNorm));
            StatusText = $"Filtered by {ActiveFilterDescription}.";
        }

        RootNode.NotifyPercentageChanged();
        if (TreeRoots.Count > 0)
        {
            var r = TreeRoots[0];
            TreeRoots = [r];
        }
        RecomputeLayout(lastWidth, lastHeight);
    }

    [RelayCommand]
    public void ClearFilter()
    {
        FilterByExtension(null);
    }

    [RelayCommand]
    public void FilterBySelectedFileType()
    {
        if (SelectedFileType != null)
        {
            FilterByExtension(SelectedFileType.Extension);
            SelectedTabIndex = 0; // Switch to Map
        }
    }

    [RelayCommand]
    public void FilterTreeBySelectedFileType()
    {
        if (SelectedFileType != null)
        {
            FilterByExtension(SelectedFileType.Extension);
            SelectedTabIndex = 1; // Switch to Tree tab
        }
    }

    [RelayCommand]
    public void ShowTopFilesForSelectedFileType()
    {
        if (SelectedFileType != null)
        {
            FilterByExtension(SelectedFileType.Extension);
            SelectedTabIndex = 2; // Switch to Top Files tab
        }
    }

    public async Task ExportHtmlReportAsync(string targetFilePath)
    {
        if (RootNode == null) return;

        if (_policyService.ActivePolicy.AllowExports == false)
        {
            StatusText = "Exports are disabled by organisation policy.";
            return;
        }

        try
        {
            StatusText = "Exporting HTML report...";
            var root = RootNode;
            var path = CurrentPath;
            var duration = _lastScanDuration;
            var redact = RedactPathsInExports;

            var html = await Task.Run(() => HtmlReportExporter.ExportToHtml(
                root,
                path,
                scanDuration: duration,
                maxTreeDepth: 6,
                redactPaths: redact));

            await File.WriteAllTextAsync(targetFilePath, html, System.Text.Encoding.UTF8);
            var fileName = Path.GetFileName(targetFilePath);
            StatusText = $"HTML report exported successfully: {fileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to export HTML report: {ex.Message}";
        }
    }

    public async Task ExportCsvTopFilesAsync(string targetFilePath)
    {
        if (RootNode == null) return;

        if (_policyService.ActivePolicy.AllowExports == false)
        {
            StatusText = "Exports are disabled by organisation policy.";
            return;
        }

        try
        {
            StatusText = "Exporting top files to CSV...";
            var root = RootNode;
            var redact = RedactPathsInExports;

            var csv = await Task.Run(() => CsvReportExporter.ExportTopFilesToCsv(root, redactPaths: redact, limit: 1000));
            await File.WriteAllTextAsync(targetFilePath, csv, System.Text.Encoding.UTF8);
            var fileName = Path.GetFileName(targetFilePath);
            StatusText = $"Top files CSV exported successfully: {fileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to export CSV: {ex.Message}";
        }
    }

    public async Task ExportCsvFileTypesAsync(string targetFilePath)
    {
        if (RootNode == null) return;

        if (_policyService.ActivePolicy.AllowExports == false)
        {
            StatusText = "Exports are disabled by organisation policy.";
            return;
        }

        try
        {
            StatusText = "Exporting file types to CSV...";
            var root = RootNode;

            var csv = await Task.Run(() => CsvReportExporter.ExportFileTypesToCsv(root));
            await File.WriteAllTextAsync(targetFilePath, csv, System.Text.Encoding.UTF8);
            var fileName = Path.GetFileName(targetFilePath);
            StatusText = $"File types CSV exported successfully: {fileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to export CSV: {ex.Message}";
        }
    }

    public async Task ExportJsonReportAsync(string targetFilePath)
    {
        if (RootNode == null) return;

        if (_policyService.ActivePolicy.AllowExports == false)
        {
            StatusText = "Exports are disabled by organisation policy.";
            return;
        }

        try
        {
            StatusText = "Exporting scan data to JSON...";
            var root = RootNode;
            var path = CurrentPath;
            var duration = _lastScanDuration;
            var redact = RedactPathsInExports;

            var json = await Task.Run(() => JsonReportExporter.ExportToJson(
                root,
                path,
                scanDuration: duration,
                redactPaths: redact,
                maxTreeDepth: 6,
                topFilesLimit: 500));

            await File.WriteAllTextAsync(targetFilePath, json, System.Text.Encoding.UTF8);
            var fileName = Path.GetFileName(targetFilePath);
            StatusText = $"JSON report exported successfully: {fileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to export JSON report: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _scanCts?.Dispose();
    }
}
