namespace Atc.Desktop.WebHost.Tests.Diagnostics;

public sealed class ProcessTreeTests
{
    [Fact]
    public void ParseParentId_ReadsTheFourthField()
        => ProcessTree.ParseParentId("4242 (WebKitWebProcess) S 4200 4200 4100 0 -1").Should().Be(4200);

    [Fact]
    public void ParseParentId_HandlesSpacesAndParenthesesInTheName()
        => ProcessTree.ParseParentId("17 (a b) (c)) R 3 17 17").Should().Be(3);

    [Fact]
    public void ParseParentId_ReturnsNullForGarbage()
        => ProcessTree.ParseParentId("not a stat line").Should().BeNull();

    [Fact]
    public void FindDescendants_IncludesChildrenAndGrandchildren()
    {
        var parents = new Dictionary<int, int>
        {
            [1] = 0,
            [100] = 1,
            [200] = 100,
            [201] = 100,
            [300] = 200,
            [400] = 1,
        };

        ProcessTree.FindDescendants(100, parents).Should().BeEquivalentTo([200, 201, 300]);
    }

    [Fact]
    public void FindDescendants_IsEmptyWithoutChildren()
        => ProcessTree.FindDescendants(5, new Dictionary<int, int> { [5] = 1 }).Should().BeEmpty();

    [Fact]
    public void FindDescendants_StopsOnACycle()
    {
        var parents = new Dictionary<int, int> { [2] = 1, [1] = 2 };

        ProcessTree.FindDescendants(1, parents).Should().BeEquivalentTo([2]);
    }

    [Fact]
    public void GetDescendants_FindsAStartedChildOnLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            ProcessTree.GetDescendants(Environment.ProcessId).Should().BeEmpty();
            return;
        }

        using var child = Process.Start(new ProcessStartInfo("sleep", "5"))!;
        try
        {
            ProcessTree.GetDescendants(Environment.ProcessId).Should().Contain(child.Id);
        }
        finally
        {
            child.Kill();
        }
    }
}