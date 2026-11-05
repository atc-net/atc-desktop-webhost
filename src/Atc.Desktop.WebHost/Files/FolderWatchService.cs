namespace Atc.Desktop.WebHost.Files;

/// <summary>
/// Watches folders for <c>folder.watch</c>. Every change in a watched folder (a file or folder added, changed,
/// removed or renamed) restarts a quiet timer; when it runs out, one notification lists the paths that changed.
/// The paths are hints: deleting or renaming a subfolder reports only the subfolder, not the files in it, so the
/// page lists the folder again. Changes below hidden folders such as <c>.git</c> are ignored.
/// </summary>
internal sealed class FolderWatchService : IDisposable
{
    /// <summary>More changed paths than this in one notification are dropped, and the notification says rescan.</summary>
    private const int MaxPaths = 200;

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly Action<string, IReadOnlyList<string>, bool> onChanged;
    private readonly TimeSpan debounce;
    private readonly Lock gate = new();
    private readonly Dictionary<string, WatchedFolder> watches = new(PathComparer);
    private bool disposed;

    /// <param name="onChanged">Called on a thread-pool thread with (folder, changed paths, rescan).</param>
    /// <param name="debounce">Quiet time before reporting.</param>
    public FolderWatchService(
        Action<string, IReadOnlyList<string>, bool> onChanged,
        TimeSpan debounce)
    {
        this.onChanged = onChanged;
        this.debounce = debounce;
    }

    /// <summary>Starts watching a folder. Watching it again only widens it to subfolders when asked.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="recursive">Include subfolders.</param>
    public void Watch(
        string folder,
        bool recursive)
    {
        var fullPath = Normalize(folder);
        if (!Directory.Exists(fullPath))
        {
            throw new BridgeException("not_found", "The folder doesn't exist.");
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (watches.TryGetValue(fullPath, out var existing))
            {
                existing.Watcher.IncludeSubdirectories |= recursive;
                return;
            }

            // Owned by the watched folder once it's added; disposed here if setting it up fails.
            FileSystemWatcher? created = null;
            try
            {
                created = new FileSystemWatcher(fullPath);
                watches[fullPath] = new WatchedFolder(Configure(created, fullPath, recursive));
                created = null;
            }
            finally
            {
                created?.Dispose();
            }
        }
    }

    /// <summary>Stops watching a folder.</summary>
    /// <param name="folder">The folder.</param>
    public void Unwatch(string folder)
    {
        var fullPath = Normalize(folder);
        lock (gate)
        {
            if (watches.Remove(fullPath, out var watch))
            {
                watch.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            foreach (var watch in watches.Values)
            {
                watch.Dispose();
            }

            watches.Clear();
        }
    }

    /// <summary>True when a path below the folder goes through a hidden name such as <c>.git</c>.</summary>
    /// <param name="folder">The watched folder.</param>
    /// <param name="path">A path below it.</param>
    /// <returns>True to ignore the change.</returns>
    internal static bool IsBelowHiddenName(
        string folder,
        string path)
        => Path.GetRelativePath(folder, path)
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.StartsWith('.') && segment is not "." and not "..");

    private static string Normalize(string folder)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    private FileSystemWatcher Configure(
        FileSystemWatcher watcher,
        string fullPath,
        bool recursive)
    {
        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size;
        watcher.IncludeSubdirectories = recursive;
        watcher.InternalBufferSize = 64 * 1024;
        watcher.Changed += (_, e) => Note(fullPath, e.FullPath);
        watcher.Created += (_, e) => Note(fullPath, e.FullPath);
        watcher.Deleted += (_, e) => Note(fullPath, e.FullPath);
        watcher.Renamed += (_, e) =>
        {
            Note(fullPath, e.OldFullPath);
            Note(fullPath, e.FullPath);
        };

        // Too many changes at once (Windows) or too many folders to watch (Linux): the page lists again.
        watcher.Error += (_, _) => Note(fullPath, path: null);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void Note(
        string folder,
        string? path)
    {
        if (path is not null && IsBelowHiddenName(folder, path))
        {
            return;
        }

        lock (gate)
        {
            if (disposed || !watches.TryGetValue(folder, out var watch))
            {
                return;
            }

            if (path is null || watch.Paths.Count >= MaxPaths)
            {
                watch.Rescan = true;
            }
            else
            {
                watch.Paths.Add(path);
            }

            watch.Timer ??= new Timer(_ => Flush(folder), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            watch.Timer.Change(debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush(string folder)
    {
        string[] paths;
        bool rescan;
        lock (gate)
        {
            if (disposed || !watches.TryGetValue(folder, out var watch))
            {
                return;
            }

            paths = watch.Paths.Order(PathComparer).ToArray();
            rescan = watch.Rescan;
            watch.Paths.Clear();
            watch.Rescan = false;
        }

        onChanged(folder, paths, rescan);
    }

    private sealed class WatchedFolder(FileSystemWatcher watcher) : IDisposable
    {
        public FileSystemWatcher Watcher { get; } = watcher;

        public HashSet<string> Paths { get; } = new(PathComparer);

        public bool Rescan { get; set; }

        public Timer? Timer { get; set; }

        public void Dispose()
        {
            Watcher.Dispose();
            Timer?.Dispose();
        }
    }
}