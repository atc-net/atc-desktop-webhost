namespace Atc.Desktop.WebHost.Tests.Platform;

public sealed class FullscreenStateTests
{
    private readonly FullscreenState state = new();

    [Fact]
    public void Toggle_EntersThenLeaves()
    {
        state.Toggle().Should().Be(FullscreenChange.Enter);
        state.IsFullscreen.Should().BeTrue();

        state.Toggle().Should().Be(FullscreenChange.Leave);
        state.IsFullscreen.Should().BeFalse();
    }

    [Fact]
    public void Element_StartsAndEndsFullscreen()
    {
        state.OnElement(containsFullscreenElement: true).Should().Be(FullscreenChange.Enter);

        state.OnElement(containsFullscreenElement: false).Should().Be(FullscreenChange.Leave);
        state.IsFullscreen.Should().BeFalse();
    }

    [Fact]
    public void ElementEnding_KeepsFullscreenStartedWithF11()
    {
        state.Toggle();

        state.OnElement(containsFullscreenElement: true).Should().Be(FullscreenChange.None);
        state.OnElement(containsFullscreenElement: false).Should().Be(FullscreenChange.None);
        state.IsFullscreen.Should().BeTrue();
    }

    [Fact]
    public void F11_LeavesFullscreenStartedByAnElement()
    {
        state.OnElement(containsFullscreenElement: true);

        state.Toggle().Should().Be(FullscreenChange.Leave);
        state.OnElement(containsFullscreenElement: false).Should().Be(FullscreenChange.None);
    }

    [Fact]
    public void SetByWindow_Fullscreen_IsNotEndedByAnElement()
    {
        state.SetByWindow(fullscreen: true);

        state.OnElement(containsFullscreenElement: false).Should().Be(FullscreenChange.None);
        state.IsFullscreen.Should().BeTrue();
    }
}