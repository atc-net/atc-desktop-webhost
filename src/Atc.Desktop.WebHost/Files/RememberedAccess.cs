namespace Atc.Desktop.WebHost.Files;

/// <summary>
/// The files and folders the user chose (command line, a second launch, the Open, Save and Open Folder dialogs, drops),
/// kept in <c>file-access.json</c> next to the settings, so the page may use them again after a restart: an app
/// reopens its last tabs without asking. Only the host adds to the list; the page can read it and remove entries,
/// so a page can't get access to a path the user never chose. Most recent first, at most
/// <see cref="MaxEntries"/>. Saved in the background. Thread-safe.
/// </summary>
internal sealed class RememberedAccess
{
    /// <summary>The most paths kept; older ones are dropped.</summary>
    public const int MaxEntries = 100;

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly Lock gate = new();
    private readonly Lock fileGate = new();
    private readonly List<BuiltInMessages.RememberedPath> entries;
    private Task saving = Task.CompletedTask;

    /// <summary>Initializes a new instance of the <see cref="RememberedAccess"/> class and reads the saved list.</summary>
    /// <param name="folder">The folder holding file-access.json.</param>
    public RememberedAccess(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        FilePath = Path.Combine(folder, "file-access.json");
        entries = Read(FilePath);
    }

    /// <summary>Gets the file path.</summary>
    public string FilePath { get; }

    /// <summary>Gets a copy of the list, most recent first.</summary>
    /// <returns>The remembered paths.</returns>
    public IReadOnlyList<BuiltInMessages.RememberedPath> GetAll()
    {
        lock (gate)
        {
            return [.. entries];
        }
    }

    /// <summary>Puts a path the user chose first in the list.</summary>
    /// <param name="path">The file or folder.</param>
    /// <param name="isFolder">True for a folder (the folder itself is allowed), false for a file (its folder is).</param>
    public void Record(
        string path,
        bool isFolder)
    {
        var fullPath = Path.GetFullPath(path);
        lock (gate)
        {
            entries.RemoveAll(e => PathComparer.Equals(e.Path, fullPath));
            entries.Insert(0, new BuiltInMessages.RememberedPath(fullPath, isFolder, DateTime.UtcNow));
            if (entries.Count > MaxEntries)
            {
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            }

            SaveInBackground();
        }
    }

    /// <summary>Removes a path from the list. Access already given in this run stays until the app closes.</summary>
    /// <param name="path">The file or folder.</param>
    /// <returns>True when it was in the list.</returns>
    public bool Forget(string path)
    {
        var fullPath = Path.GetFullPath(path);
        lock (gate)
        {
            var removed = entries.RemoveAll(e => PathComparer.Equals(e.Path, fullPath)) > 0;
            if (removed)
            {
                SaveInBackground();
            }

            return removed;
        }
    }

    /// <summary>Waits for the last save, so it isn't lost when the process ends.</summary>
    /// <param name="timeout">How long to wait.</param>
    public void Flush(TimeSpan timeout)
    {
        Task last;
        lock (gate)
        {
            last = saving;
        }

        try
        {
            last.Wait(timeout);
        }
        catch (AggregateException)
        {
            // Already ignored by the save; nothing to keep.
        }
    }

    private static List<BuiltInMessages.RememberedPath> Read(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            var saved = JsonSerializer.Deserialize(File.ReadAllText(filePath), BridgeJsonContext.Default.IReadOnlyListRememberedPath);
            return saved is null
                ? []
                : [.. saved.Where(e => !string.IsNullOrWhiteSpace(e.Path) && Path.IsPathFullyQualified(e.Path)).Take(MaxEntries)];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return [];
        }
    }

    private void SaveInBackground()
    {
        // Called under the gate: saves run one after another and each writes the list as it is then.
        saving = saving.ContinueWith(_ => Save(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private void Save()
    {
        IReadOnlyList<BuiltInMessages.RememberedPath> snapshot = GetAll();
        lock (fileGate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, BridgeJsonContext.Default.IReadOnlyListRememberedPath));
                File.Move(temp, FilePath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not saved: the paths are still usable in this run.
            }
        }
    }
}