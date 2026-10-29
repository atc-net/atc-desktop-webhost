using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace AtcBench.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        BenchRun.Mark("windowCreated");
        ContentRendered += (_, _) => BenchRun.Mark("windowShown");
        _ = InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        BenchRun.Mark("webviewStarting");
        var environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, BenchRun.DataFolder("Wpf"));
        BenchRun.Mark("webviewEnvironmentCreated");
        await WebView.EnsureCoreWebView2Async(environment);
        BenchRun.Mark("webviewControllerCreated");
        await BenchRun.ShowPageAsync(WebView.CoreWebView2, Close);
    }
}
