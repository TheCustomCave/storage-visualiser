using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace StorageVisualiser.App;

public partial class App : Application
{
    public override void Initialize()
    {
        Console.WriteLine("[StorageVisualiser] App.Initialize() calling AvaloniaXamlLoader.Load...");
        AvaloniaXamlLoader.Load(this);
        Console.WriteLine("[StorageVisualiser] App.Initialize() done.");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("[StorageVisualiser] OnFrameworkInitializationCompleted()...");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                Console.WriteLine("[StorageVisualiser] Creating MainWindow...");
                desktop.MainWindow = new MainWindow();
                desktop.MainWindow.Show();
                desktop.MainWindow.Activate();
                Console.WriteLine("[StorageVisualiser] MainWindow created and shown successfully.");
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("crash.log", ex.ToString());
                Console.Error.WriteLine("[StorageVisualiser] Exception in MainWindow creation: " + ex);
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("[StorageVisualiser] OnFrameworkInitializationCompleted() complete.");
    }
}