using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StorageVisualiser.App.ViewModels;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Windows.Shell;

namespace StorageVisualiser.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Window lifetime is managed by OnClosed")]
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        Program.Log("MainWindow constructor started");
        InitializeComponent();
        Program.Log("MainWindow InitializeComponent finished");
        _vm = new MainViewModel();
        Program.Log("MainWindow MainViewModel created");
        DataContext = _vm;

        CanvasControl.NodeSelected += OnCanvasNodeSelected;
        CanvasControl.NodeDrillDown += OnCanvasNodeDrillDown;
        CanvasControl.SizeChangedAction += (w, h) => _vm.RecomputeLayout(w, h);

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Program.Log("MainWindow constructor finished");
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File)) return;

        try
        {
            var item = e.DataTransfer.TryGetValue(DataFormat.File);
            if (item == null) return;

            var path = item.Path.LocalPath;
            if (Directory.Exists(path) || File.Exists(path))
            {
                var targetPath = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? path);
                var isDrive = Path.GetPathRoot(targetPath)?.Equals(targetPath, StringComparison.OrdinalIgnoreCase) == true;
                var isNetwork = targetPath.StartsWith(@"\\", StringComparison.Ordinal);
                if (!isNetwork)
                {
                    try
                    {
                        var root = Path.GetPathRoot(targetPath);
                        if (!string.IsNullOrEmpty(root))
                        {
                            var d = new DriveInfo(root);
                            if (d.IsReady && d.DriveType == DriveType.Network) isNetwork = true;
                        }
                    }
                    catch { }
                }
                await _vm.RequestScanPathAsync(targetPath, isDriveRoot: isDrive, isNetwork: isNetwork);
            }
        }
        catch (Exception ex)
        {
            Program.Log($"OnDrop error: {ex.Message}");
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        Program.Log("MainWindow.OnOpened entered");
        base.OnOpened(e);
        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        Program.Log($"MainWindow.OnOpened handle={handle}");
        if (handle != 0)
        {
            WindowsShellHelper.EnsureWindowVisible(handle, 1260, 800);
            Program.Log("MainWindow.OnOpened EnsureWindowVisible finished");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _vm.Dispose();
    }

    private void OnCanvasNodeSelected(StorageNode? node)
    {
        _vm.SelectNode(node);
    }

    private void OnCanvasNodeDrillDown(StorageNode node)
    {
        _vm.SetViewNode(node);
    }

    private async void OnBrowseFolderClicked(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder to Scan",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            if (Directory.Exists(path))
            {
                var isNetwork = path.StartsWith(@"\\", StringComparison.Ordinal);
                if (!isNetwork)
                {
                    try
                    {
                        var root = Path.GetPathRoot(path);
                        if (!string.IsNullOrEmpty(root))
                        {
                            var d = new DriveInfo(root);
                            if (d.IsReady && d.DriveType == DriveType.Network) isNetwork = true;
                        }
                    }
                    catch { }
                }
                await _vm.RequestScanPathAsync(path, isDriveRoot: false, isNetwork: isNetwork);
            }
        }
    }

    private async void OnRescanFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm.CurrentViewNode != null)
        {
            var path = _vm.CurrentViewNode.GetFullPath();
            if (Directory.Exists(path))
            {
                await _vm.StartScanPathAsync(path, isDriveRoot: false);
            }
        }
    }

    private async void OnCopyPathClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm.SelectedNode != null && Clipboard != null)
        {
            var path = _vm.SelectedNode.Kind == StorageItemKind.OtherGroup
                ? (_vm.SelectedNode.Parent?.GetFullPath() ?? _vm.CurrentPath)
                : _vm.SelectedNode.GetFullPath();
            await Clipboard.SetTextAsync(path);
        }
    }

    private void OnTopFilesDoubleTapped(object? sender, RoutedEventArgs e)
    {
        _vm.OpenInExplorer();
    }

    private static readonly string[] HtmlPatterns = ["*.html"];
    private static readonly string[] HtmlMimeTypes = ["text/html"];

    private async void OnExportReportClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm.RootNode == null) return;

        var defaultFileName = StorageVisualiser.Core.Export.HtmlReportExporter.GenerateDefaultFileName(_vm.CurrentPath, "html");
        var docsFolder = await StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Storage Report (HTML)",
            SuggestedFileName = defaultFileName,
            SuggestedStartLocation = docsFolder,
            DefaultExtension = "html",
            FileTypeChoices =
            [
                new FilePickerFileType("HTML Report (*.html)")
                {
                    Patterns = HtmlPatterns,
                    MimeTypes = HtmlMimeTypes
                }
            ]
        });

        if (file != null)
        {
            var localPath = file.Path.LocalPath;
            await _vm.ExportHtmlReportAsync(localPath);
        }
    }
}