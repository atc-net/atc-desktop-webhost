namespace Atc.Desktop.WebHost.Files;

/// <summary>
/// Watches individual files. One watcher per folder; events are matched by full path and debounced,
/// so an editor's save (often write + rename, several events) becomes one notification.
/// </summary>
internal sealed class FileWatchService : IDisposable
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly Action<string, string> onChanged;
    private readonly TimeSpan debounce;
    private readonly Lock gate = new();
    private readonly Dictionary<string, FileSystemWatcher> watchersByFolder = new(PathComparer);
    private readonly HashSet<string> watchedFiles = new(PathComparer);
    private readonly Dictionary<string, (Timer Timer, string Kind)> pending = new(PathComparer);
    private bool disposed;

    /// <param name="onChanged">Called on a thread-pool thread with (full path, kind: "changed" | "deleted").</param>
    /// <param name="debounce">Quiet time before reporting.</param>
    public FileWatchService(
        Action<string, string> onChanged,
        TimeSpan debounce)
    {
        this.onChanged = onChanged;
        this.debounce = debounce;
    }

    public void Watch(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var folder = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException($"'{path}' has no folder.", nameof(path));

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!watchedFiles.Add(fullPath) || watchersByFolder.ContainsKey(folder))
            {
                return;
            }

            var watcher = new FileSystemWatcher(folder)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
            };
            watcher.Changed += (_, e) => Report(e.FullPath, "changed");
            watcher.Created += (_, e) => Report(e.FullPath, "changed");
            watcher.Deleted += (_, e) => Report(e.FullPath, "deleted");
            watcher.Renamed += (_, e) =>
            {
                // Atomic save: the editor writes "x.tmp" and renames it to "x.md".
                Report(e.FullPath, "changed");
                Report(e.OldFullPath, "deleted");
            };
            watcher.EnableRaisingEvents = true;
            watchersByFolder[folder] = watcher;
        }
    }

    public void Unwatch(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var folder = Path.GetDirectoryName(fullPath);
        lock (gate)
        {
            if (!watchedFiles.Remove(fullPath) || folder is null)
            {
                return;
            }

            var stillUsed = watchedFiles.Any(f => PathComparer.Equals(Path.GetDirectoryName(f), folder));
            if (!stillUsed && watchersByFolder.Remove(folder, out var watcher))
            {
                watcher.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            foreach (var watcher in watchersByFolder.Values)
            {
                watcher.Dispose();
            }

            foreach (var (timer, _) in pending.Values)
            {
                timer.Dispose();
            }

            watchersByFolder.Clear();
            watchedFiles.Clear();
            pending.Clear();
        }
    }

    private void Report(
        string fullPath,
        string kind)
    {
        lock (gate)
        {
            if (disposed || !watchedFiles.Contains(fullPath))
            {
                return;
            }

            // A later "changed" wins over "deleted": the file is back after an atomic save.
            if (pending.TryGetValue(fullPath, out var existing))
            {
                existing.Timer.Change(debounce, Timeout.InfiniteTimeSpan);
                pending[fullPath] = (existing.Timer, kind);
                return;
            }

            var timer = new Timer(_ => Flush(fullPath), state: null, debounce, Timeout.InfiniteTimeSpan);
            pending[fullPath] = (timer, kind);
        }
    }

    private void Flush(string fullPath)
    {
        string kind;
        lock (gate)
        {
            if (!pending.Remove(fullPath, out var entry))
            {
                return;
            }

            entry.Timer.Dispose();
            kind = entry.Kind == "deleted" && File.Exists(fullPath) ? "changed" : entry.Kind;
        }

        onChanged(fullPath, kind);
    }
}