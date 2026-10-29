namespace Atc.Desktop.WebHost.Platform;

/// <summary>An opaque RGB colour.</summary>
/// <param name="R">Red.</param>
/// <param name="G">Green.</param>
/// <param name="B">Blue.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct HostColor(
    byte R,
    byte G,
    byte B)
{
    /// <summary>Gets white.</summary>
    public static HostColor White { get; } = new(255, 255, 255);

    /// <summary>Gets GitHub's dark page background (#0d1117).</summary>
    public static HostColor GitHubDark { get; } = new(0x0D, 0x11, 0x17);

    /// <summary>Parses "#rrggbb" or "rrggbb".</summary>
    /// <param name="hex">The colour text.</param>
    /// <returns>The colour.</returns>
    public static HostColor Parse(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        var text = hex.AsSpan().TrimStart('#');
        if (text.Length != 6 ||
            !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"'{hex}' is not a #rrggbb colour.");
        }

        return new HostColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>Formats the colour as CSS "#rrggbb".</summary>
    /// <returns>The CSS colour.</returns>
    public string ToCss()
        => string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}");

    /// <inheritdoc />
    public override string ToString()
        => ToCss();
}