namespace Atc.Desktop.WebHost.Hosting;

/// <summary>
/// Per-app settings as one JSON object in <c>settings.json</c>. The page reads them at boot (no
/// round trip) and changes them with <c>settings.set</c>. Saves are atomic (write + rename).
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private readonly Lock gate = new();
    private JsonObject? current;

    /// <summary>Initializes a new instance of the <see cref="SettingsStore"/> class.</summary>
    /// <param name="folder">The folder holding settings.json.</param>
    public SettingsStore(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        FilePath = Path.Combine(folder, "settings.json");
    }

    /// <summary>Gets the settings file path.</summary>
    public string FilePath { get; }

    /// <summary>Gets a copy of the settings; empty when none are saved or the file is broken.</summary>
    /// <returns>The settings.</returns>
    public JsonObject Get()
    {
        lock (gate)
        {
            return (JsonObject)Load().DeepClone();
        }
    }

    /// <summary>Gets one top-level string setting.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <see langword="null"/> when missing or not a string.</returns>
    public string? GetString(string key)
    {
        lock (gate)
        {
            return Load()[key] is JsonValue value && value.TryGetValue<string>(out var text)
                ? text
                : null;
        }
    }

    /// <summary>Merges top-level keys into the settings and saves them. A <see langword="null"/> value removes the key.</summary>
    /// <param name="patch">The keys to set.</param>
    /// <returns>A copy of the merged settings.</returns>
    public JsonObject Merge(JsonObject patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        lock (gate)
        {
            var merged = (JsonObject)Load().DeepClone();
            foreach (var (key, value) in patch)
            {
                if (value is null)
                {
                    merged.Remove(key);
                }
                else
                {
                    merged[key] = value.DeepClone();
                }
            }

            Save(merged);
            current = merged;
            return (JsonObject)merged.DeepClone();
        }
    }

    private JsonObject Load()
    {
        if (current is not null)
        {
            return current;
        }

        try
        {
            current = File.Exists(FilePath)
                ? JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            current = null;
        }

        current ??= [];
        return current;
    }

    private void Save(JsonObject settings)
    {
        var folder = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(folder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, settings.ToJsonString(Indented));
        File.Move(temp, FilePath, overwrite: true);
    }
}