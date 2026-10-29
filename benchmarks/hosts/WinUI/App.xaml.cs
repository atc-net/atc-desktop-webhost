using Microsoft.UI.Xaml;

namespace AtcBench.WinUI;

/// <summary>A standard WinUI 3 app: generated Main, App.xaml, MainWindow.xaml.</summary>
public partial class App : Application
{
    private Window? window;

    public App()
    {
        BenchRun.Start("WinUI 3 + WebView2");
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.Activate();
    }
}
