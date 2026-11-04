namespace Atc.Desktop.WebHost.Tests.Platform;

public sealed class MonitorLayoutTests
{
    private static readonly HostMonitor Laptop = Monitor(x: 0, y: 0, width: 1920, height: 1080, isPrimary: true);
    private static readonly HostMonitor LedStrip = Monitor(x: 1920, y: 0, width: 2560, height: 720, isPrimary: false);
    private static readonly HostMonitor LeftScreen = Monitor(x: -2560, y: 0, width: 2560, height: 1440, isPrimary: false);

    [Fact]
    public void Number_GoesLeftToRight()
    {
        var numbered = MonitorLayout.Number([LedStrip, Laptop, LeftScreen]);

        numbered.Select(m => (m.Number, m.Bounds.X)).Should().Equal((1, -2560), (2, 0), (3, 1920));
    }

    [Fact]
    public void Find_ByNumber()
    {
        var numbered = MonitorLayout.Number([Laptop, LedStrip]);

        MonitorLayout.Find(numbered, 2)!.Bounds.Width.Should().Be(2560);
    }

    [Fact]
    public void Find_UnknownNumber_FallsBackToThePrimary()
    {
        var numbered = MonitorLayout.Number([LeftScreen, Laptop, LedStrip]);

        MonitorLayout.Find(numbered, 9)!.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Find_NoMonitors_IsNull()
        => MonitorLayout.Find([], 1).Should().BeNull();

    private static HostMonitor Monitor(
        int x,
        int y,
        int width,
        int height,
        bool isPrimary)
        => new(Number: 0, new HostRect(x, y, width, height), new HostRect(x, y, width, height), isPrimary, Scale: 1);
}