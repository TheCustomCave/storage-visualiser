using System;
using System.IO;
using Avalonia.Controls;
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
        Program.Log("MainWindow constructor finished");
    }

    protected override void OnOpened(EventArgs e)
    {
        Program.Log("MainWindow.OnOpened entered");
        base.OnOpened(e);
        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        Program.Log($"MainWindow.OnOpened handle={handle}");
        if (handle != 0)
        {
            WindowsShellHelper.EnsureWindowVisible(handle, 1100, 700);
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
            await Clipboard.SetTextAsync(_vm.SelectedNode.GetFullPath());
        }
    }
}