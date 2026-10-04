using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace StorageVisualiser.App;

public partial class App : Application
{
    public override void Initialize()
    {
        Program.Log("App.Initialize started");
        AvaloniaXamlLoader.Load(this);
        Program.Log("App.Initialize finished");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Program.Log("App.OnFrameworkInitializationCompleted started");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Program.Log("Creating MainWindow instance...");
            desktop.MainWindow = new MainWindow();
            Program.Log("MainWindow instance assigned to desktop.MainWindow");
        }

        base.OnFrameworkInitializationCompleted();
        Program.Log("App.OnFrameworkInitializationCompleted finished");
    }
}