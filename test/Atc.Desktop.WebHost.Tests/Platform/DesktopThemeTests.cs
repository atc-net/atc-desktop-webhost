namespace Atc.Desktop.WebHost.Tests.Platform;

public sealed class DesktopThemeTests
{
    [Theory]
    [InlineData("Adwaita:dark", null, null, true)]
    [InlineData("Adwaita-dark", null, null, true)]
    [InlineData("Adwaita", "prefer-dark", "Yaru-dark", false)]
    [InlineData(null, "prefer-dark", null, true)]
    [InlineData(null, "prefer-light", "Yaru-dark", false)]
    [InlineData(null, "default", "Yaru-dark", true)]
    [InlineData(null, "default", "Breeze-Dark", true)]
    [InlineData(null, null, "Adwaita", false)]
    [InlineData(null, null, null, false)]
    public void IsDark_FollowsTheDesktopSettings(
        string? gtkThemeVariable,
        string? colorScheme,
        string? gtkThemeName,
        bool expected)
        => DesktopTheme.IsDark(gtkThemeVariable, colorScheme, gtkThemeName).Should().Be(expected);
}