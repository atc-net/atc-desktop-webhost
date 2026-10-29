namespace Atc.Desktop.WebHost.Tests;

public sealed class WebHostCommandLineTests
{
    [Fact]
    public void Parse_SeparatesFilesAndSwitches()
    {
        var commandLine = WebHostCommandLine.Parse(["a.md", "--devtools", "--ui=embedded", "b.md"]);

        commandLine.Files.Should().Equal(Path.GetFullPath("a.md"), Path.GetFullPath("b.md"));
        commandLine.IsDevTools.Should().BeTrue();
        commandLine.GetSwitch("ui").Should().Be("embedded");
        commandLine.IsBench.Should().BeFalse();
    }

    [Fact]
    public void Parse_BenchOut_TakesTheNextArgument()
    {
        var commandLine = WebHostCommandLine.Parse(["doc.md", "--bench", "--bench-out", "out.txt"]);

        commandLine.IsBench.Should().BeTrue();
        commandLine.BenchOut.Should().Be(Path.GetFullPath("out.txt"));
        commandLine.Files.Should().ContainSingle();
    }

    [Fact]
    public void Parse_BenchOut_AcceptsEqualsForm()
    {
        var commandLine = WebHostCommandLine.Parse(["--bench-out=r.txt"]);

        commandLine.BenchOut.Should().Be(Path.GetFullPath("r.txt"));
        commandLine.Files.Should().BeEmpty();
    }

    [Fact]
    public void Switches_AreCaseInsensitive()
        => WebHostCommandLine.Parse(["--New-Instance"]).IsNewInstance.Should().BeTrue();
}