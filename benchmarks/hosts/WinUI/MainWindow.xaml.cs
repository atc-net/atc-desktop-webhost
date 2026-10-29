using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;

namespace AtcBench.WinUI;

public sealed partial class MainWindow : Window
{
    private bool shown;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1200, 800));
        BenchRun.Mark("windowCreated");
        Activated += (_, _) =>
        {
            if (!shown)
            {
                shown = true;
                BenchRun.Mark("windowShown");
            }
        };
        _ = InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        BenchRun.Mark("webviewStarting");
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
            string.Empty,
            BenchRun.DataFolder("WinUI"),
            new CoreWebView2EnvironmentOptions());
        BenchRun.Mark("webviewEnvironmentCreated");
        await WebView.EnsureCoreWebView2Async(environment);
        BenchRun.Mark("webviewControllerCreated");
        await BenchRun.ShowPageAsync(WebView.CoreWebView2, Close);
    }
}
