namespace Atc.Desktop.WebHost.Linux;

/// <summary>
/// The displays as GTK sees them. GTK reports logical pixels; they are scaled to physical pixels here.
/// GTK 4 has no primary display or work area, so the first display counts as primary and the work area is the whole display.
/// </summary>
[SupportedOSPlatform("linux")]
internal static class GtkMonitors
{
    /// <summary>Lists the displays, numbered left to right. Initializes GTK when called before the window exists.</summary>
    /// <returns>The displays; empty without a display connection.</returns>
    public static IReadOnlyList<HostMonitor> GetAll()
        => MonitorLayout.Number(GetGdkMonitors().Select((m, i) => Describe(m.Monitor, isFirst: i == 0)));

    /// <summary>Finds the GDK display for a placement.</summary>
    /// <param name="number">The display number, or <see langword="null"/>.</param>
    /// <returns>The display, or <see langword="null"/>.</returns>
    public static Gdk.Monitor? Find(int? number)
    {
        var all = GetGdkMonitors();
        var numbered = GetAll();
        var wanted = MonitorLayout.Find(numbered, number);
        return wanted is null
            ? null
            : all.FirstOrDefault(m => Describe(m.Monitor, isFirst: false).Bounds == wanted.Bounds).Monitor;
    }

    private static List<(Gdk.Monitor Monitor, int Index)> GetGdkMonitors()
    {
        Gtk.Module.Initialize();
        if (!Gtk.Functions.InitCheck())
        {
            return [];
        }

        var list = Gdk.Display.GetDefault()?.GetMonitors();
        var result = new List<(Gdk.Monitor, int)>();
        if (list is null)
        {
            return result;
        }

        for (uint i = 0; i < list.GetNItems(); i++)
        {
            if (list.GetObject(i) is Gdk.Monitor monitor)
            {
                result.Add((monitor, (int)i));
            }
        }

        return result;
    }

    private static HostMonitor Describe(
        Gdk.Monitor monitor,
        bool isFirst)
    {
        var scale = monitor.GetScale();
        monitor.GetGeometry(out var geometry);
        using var owned = geometry;
        var bounds = new HostRect(
            (int)(geometry.X * scale),
            (int)(geometry.Y * scale),
            (int)(geometry.Width * scale),
            (int)(geometry.Height * scale));
        return new HostMonitor(Number: 0, bounds, bounds, isFirst, scale);
    }
}