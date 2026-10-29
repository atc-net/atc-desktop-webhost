namespace Atc.Desktop.WebHost.Tests.Hosting;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atc-webhost-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Get_WithoutFile_IsEmpty()
        => new SettingsStore(folder).Get().Count.Should().Be(0);

    [Fact]
    public void Merge_SavesAndIsReadBackByANewStore()
    {
        new SettingsStore(folder).Merge(new JsonObject { ["theme"] = "dark", ["zoom"] = 1.25 });

        var reloaded = new SettingsStore(folder);

        reloaded.GetString("theme").Should().Be("dark");
        reloaded.Get()["zoom"]!.GetValue<double>().Should().Be(1.25);
    }

    [Fact]
    public void Merge_WithNull_RemovesTheKey()
    {
        var store = new SettingsStore(folder);
        store.Merge(new JsonObject { ["theme"] = "dark", ["keep"] = true });

        var merged = store.Merge(new JsonObject { ["theme"] = null });

        merged.ContainsKey("theme").Should().BeFalse();
        merged["keep"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Get_ReturnsACopy()
    {
        var store = new SettingsStore(folder);
        store.Get()["x"] = 1;

        store.Get().ContainsKey("x").Should().BeFalse();
    }

    [Fact]
    public void BrokenFile_IsTreatedAsEmpty()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "settings.json"), "{ not json");

        new SettingsStore(folder).Get().Count.Should().Be(0);
    }

    [Fact]
    public void GetString_IgnoresNonStrings()
    {
        var store = new SettingsStore(folder);
        store.Merge(new JsonObject { ["theme"] = 3 });

        store.GetString("theme").Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}