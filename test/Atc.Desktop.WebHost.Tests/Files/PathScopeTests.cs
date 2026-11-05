namespace Atc.Desktop.WebHost.Tests.Files;

public sealed class PathScopeTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atc-webhost-scope");

    [Fact]
    public void NothingGranted_DeniesEveryPath()
    {
        var scope = new PathScope(isRestricted: true);

        scope.IsAllowed(Path.Combine(root, "docs", "readme.md")).Should().BeFalse();
    }

    [Fact]
    public void GrantedFile_IsAllowed()
    {
        var scope = new PathScope(isRestricted: true);
        var file = Path.Combine(root, "docs", "readme.md");

        scope.Grant(file);

        scope.IsAllowed(file).Should().BeTrue();
    }

    [Fact]
    public void GrantedFolder_AllowsItsFiles_NotItsParentOrSiblings()
    {
        var scope = new PathScope(isRestricted: true);
        scope.GrantFolder(Path.Combine(root, "wiki"));

        scope.IsAllowed(Path.Combine(root, "wiki")).Should().BeTrue();
        scope.IsAllowed(Path.Combine(root, "wiki", "Home.md")).Should().BeTrue();
        scope.IsAllowed(Path.Combine(root, "wiki", "sub", "Page.md")).Should().BeTrue();
        scope.IsAllowed(Path.Combine(root, "other.md")).Should().BeFalse();
        scope.IsAllowed(Path.Combine(root, "wiki-old", "Home.md")).Should().BeFalse();
        scope.IsAllowed(Path.Combine(root, "wiki", "..", "other.md")).Should().BeFalse();
    }

    [Fact]
    public void FileNextToAGrantedFile_IsAllowed()
    {
        var scope = new PathScope(isRestricted: true);
        scope.Grant(Path.Combine(root, "docs", "readme.md"));

        scope.IsAllowed(Path.Combine(root, "docs", "other.md")).Should().BeTrue();
    }

    [Fact]
    public void FileBelowAGrantedFile_IsAllowed()
    {
        var scope = new PathScope(isRestricted: true);
        scope.Grant(Path.Combine(root, "docs", "readme.md"));

        scope.IsAllowed(Path.Combine(root, "docs", "images", "logo.png")).Should().BeTrue();
    }

    [Fact]
    public void FileAboveAGrantedFile_IsDenied()
    {
        var scope = new PathScope(isRestricted: true);
        scope.Grant(Path.Combine(root, "docs", "readme.md"));

        scope.IsAllowed(Path.Combine(root, "secret.txt")).Should().BeFalse();
    }

    [Fact]
    public void FolderWithTheSamePrefix_IsDenied()
    {
        var scope = new PathScope(isRestricted: true);
        scope.Grant(Path.Combine(root, "docs", "readme.md"));

        scope.IsAllowed(Path.Combine(root, "docs2", "readme.md")).Should().BeFalse();
    }

    [Fact]
    public void ParentSegments_AreResolvedBeforeTheCheck()
    {
        var scope = new PathScope(isRestricted: true);
        scope.Grant(Path.Combine(root, "docs", "readme.md"));

        scope.IsAllowed(Path.Combine(root, "docs", "..", "secret.txt")).Should().BeFalse();
    }

    [Fact]
    public void CaseDifferences_FollowTheFileSystem()
    {
        var scope = new PathScope(isRestricted: true);
        scope.Grant(Path.Combine(root, "docs", "readme.md"));

        scope.IsAllowed(Path.Combine(root, "DOCS", "other.md")).Should().Be(OperatingSystem.IsWindows());
    }

    [Fact]
    public void Unrestricted_AllowsEveryPath()
    {
        var scope = new PathScope(isRestricted: false);

        scope.IsAllowed(Path.Combine(root, "anything.txt")).Should().BeTrue();
    }

    [Fact]
    public void Require_ThrowsForbiddenForADeniedPath()
    {
        var scope = new PathScope(isRestricted: true);

        var act = () => scope.Require(Path.Combine(root, "secret.txt"));

        act.Should().Throw<BridgeException>().Which.Code.Should().Be("forbidden");
    }

    [Fact]
    public void Require_ReturnsTheFullPath()
    {
        var scope = new PathScope(isRestricted: true);
        scope.Grant(Path.Combine(root, "docs", "readme.md"));

        scope.Require(Path.Combine(root, "docs", ".", "other.md")).Should().Be(Path.Combine(root, "docs", "other.md"));
    }
}