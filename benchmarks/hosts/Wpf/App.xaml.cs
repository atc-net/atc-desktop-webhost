using System.Windows;

namespace AtcBench.Wpf;

/// <summary>A standard WPF app: generated Main, App.xaml, MainWindow.xaml.</summary>
public partial class App : Application
{
    public App()
        => BenchRun.Start("WPF + WebView2");
}
