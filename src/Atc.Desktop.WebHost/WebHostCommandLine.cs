namespace Atc.Desktop.WebHost;

/// <summary>
/// The host's command line: file paths plus a few switches. Unknown <c>--name</c> or <c>--name=value</c>
/// switches are kept in <see cref="Switches"/> for the app.
/// <list type="bullet">
/// <item><c>--bench</c>: measure startup, then exit; skips single instance.</item>
/// <item><c>--bench-out &lt;file&gt;</c>: where to write the <c>KEY=VALUE</c> results (default: stdout).</item>
/// <item><c>--new-instance</c>: don't hand off to a running instance.</item>
/// <item><c>--devtools</c>: enable dev tools.</item>
/// </list>
/// </summary>
public sealed class WebHostCommandLine
{
    private WebHostCommandLine(
        IReadOnlyList<string> files,
        IReadOnlyDictionary<string, string> switches,
        string? benchOut)
    {
        Files = files;
        Switches = switches;
        BenchOut = benchOut;
    }

    /// <summary>Gets the file paths, made absolute.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>Gets all switches without the leading "--"; a switch without "=value" has the value "true".</summary>
    public IReadOnlyDictionary<string, string> Switches { get; }

    /// <summary>Gets a value indicating whether startup is measured (<c>--bench</c>).</summary>
    public bool IsBench
        => HasSwitch("bench");

    /// <summary>Gets the bench output file (<c>--bench-out</c>), or <see langword="null"/> for stdout.</summary>
    public string? BenchOut { get; }

    /// <summary>Gets a value indicating whether single instance is skipped (<c>--new-instance</c>).</summary>
    public bool IsNewInstance
        => HasSwitch("new-instance");

    /// <summary>Gets a value indicating whether dev tools are enabled (<c>--devtools</c>).</summary>
    public bool IsDevTools
        => HasSwitch("devtools");

    /// <summary>Parses the arguments.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The command line.</returns>
    public static WebHostCommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var files = new List<string>();
        var switches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? benchOut = null;
        var i = 0;
        while (i < args.Count)
        {
            var arg = args[i++];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                files.Add(Path.GetFullPath(arg));
                continue;
            }

            var body = arg[2..];
            var equals = body.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? body : body[..equals];
            var value = equals < 0 ? "true" : body[(equals + 1)..];
            if (name.Equals("bench-out", StringComparison.OrdinalIgnoreCase) && equals < 0 && i < args.Count)
            {
                value = args[i++];
            }

            if (name.Equals("bench-out", StringComparison.OrdinalIgnoreCase))
            {
                benchOut = Path.GetFullPath(value);
            }

            switches[name] = value;
        }

        return new WebHostCommandLine(files, switches, benchOut);
    }

    /// <summary>Checks for a switch.</summary>
    /// <param name="name">The switch name without "--".</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool HasSwitch(string name)
        => Switches.ContainsKey(name);

    /// <summary>Gets a switch value.</summary>
    /// <param name="name">The switch name without "--".</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? GetSwitch(string name)
        => Switches.GetValueOrDefault(name);
}