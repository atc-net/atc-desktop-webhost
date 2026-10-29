namespace Atc.Desktop.WebHost.Tests.Diagnostics;

public sealed class DiagnosticsTests
{
    [Fact]
    public void StartupClock_ReportsMarksInOrder_FirstMarkWins()
    {
        var clock = new StartupClock();
        clock.Mark("a");
        clock.Mark("b");
        clock.Mark("a");

        var marks = clock.SinceProcessStart();

        marks.Keys.Should().Equal("a", "b");
        marks["a"].Should().BeLessThanOrEqualTo(marks["b"]);
        marks["a"].Should().BeGreaterThan(0);
    }

    [Fact]
    public void StartupClock_ConvertsPageTimestamps()
    {
        var clock = new StartupClock();
        var startEpochMs = (clock.ProcessStartUtc - DateTime.UnixEpoch).TotalMilliseconds;

        clock.FromEpochMs(startEpochMs + 250).Should().BeApproximately(250, 0.1);
    }

    [Fact]
    public void BenchReport_UsesPersKeyValueFormat()
    {
        var report = BenchReport.Format(
            new Dictionary<string, double>(StringComparer.Ordinal) { ["main"] = 40, ["firstPaint"] = 512.345 },
            new Dictionary<string, double>(StringComparer.Ordinal) { ["firstPaint"] = 510 },
            new Dictionary<string, double>(StringComparer.Ordinal) { ["Host"] = 10, ["WebView"] = 20 },
            "webview2");

        var lines = report.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().StartWith("STARTUP_MS=512.3");
        lines.Should().Contain(["PRIVATE_MB=30.0", "MARK_main=40.0", "PAGE_firstPaint=510.0", "PLATFORM=webview2"]);
    }
}