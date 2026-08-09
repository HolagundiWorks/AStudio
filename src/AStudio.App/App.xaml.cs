using Microsoft.UI.Xaml;

namespace AStudio.App;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
        catch (Exception ex)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AStudio");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "startup-error.log"), $"{DateTime.Now:O}\n{ex}");
            }
            catch { /* best-effort */ }
            throw;
        }
    }
}
