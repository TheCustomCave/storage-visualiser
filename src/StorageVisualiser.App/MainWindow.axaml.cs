using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StorageVisualiser.App.ViewModels;
using StorageVisualiser.Core.Model;

namespace StorageVisualiser.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Window lifetime is managed by OnClosed")]
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        CanvasControl.NodeSelected += OnCanvasNodeSelected;
        CanvasControl.NodeDrillDown += OnCanvasNodeDrillDown;
        CanvasControl.SizeChangedAction += (w, h) => _vm.RecomputeLayout(w, h);
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
                await _vm.StartScanPathAsync(path, isDriveRoot: false);
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