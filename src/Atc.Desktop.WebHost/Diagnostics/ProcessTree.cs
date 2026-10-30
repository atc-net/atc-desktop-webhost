namespace Atc.Desktop.WebHost.Diagnostics;

/// <summary>
/// Finds a process's descendants on Linux by reading <c>/proc</c>. WebKitGTK has no API for its process ids,
/// but its web, network and GPU processes (and any sandbox helpers) are all started below the app.
/// </summary>
internal static class ProcessTree
{
    /// <summary>Gets the ids of every process below <paramref name="rootId"/>.</summary>
    /// <param name="rootId">The ancestor, usually this process.</param>
    /// <returns>The descendants; empty when <c>/proc</c> can't be read.</returns>
    public static IReadOnlyList<int> GetDescendants(int rootId)
    {
        var parents = new Dictionary<int, int>();
        try
        {
            foreach (var folder in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                {
                    continue;
                }

                try
                {
                    var parentId = ParseParentId(File.ReadAllText(Path.Combine(folder, "stat")));
                    if (parentId is not null)
                    {
                        parents[id] = parentId.Value;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The process exited while listing.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return FindDescendants(rootId, parents);
    }

    /// <summary>Reads the parent id from a <c>/proc/&lt;pid&gt;/stat</c> line.</summary>
    /// <param name="stat">The file content: <c>pid (comm) state ppid ...</c>. The name may contain spaces and ')'.</param>
    /// <returns>The parent id, or <see langword="null"/> when the line can't be read.</returns>
    public static int? ParseParentId(string stat)
    {
        ArgumentNullException.ThrowIfNull(stat);

        var nameEnd = stat.LastIndexOf(')');
        if (nameEnd < 0)
        {
            return null;
        }

        var fields = stat[(nameEnd + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 1 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parentId)
            ? parentId
            : null;
    }

    /// <summary>Walks a child → parent map down from <paramref name="rootId"/>.</summary>
    /// <param name="rootId">The ancestor.</param>
    /// <param name="parents">Process id → parent id.</param>
    /// <returns>The descendants, nearest first.</returns>
    public static IReadOnlyList<int> FindDescendants(
        int rootId,
        IReadOnlyDictionary<int, int> parents)
    {
        ArgumentNullException.ThrowIfNull(parents);

        var children = parents
            .Where(p => p.Key != p.Value)
            .ToLookup(p => p.Value, p => p.Key);
        var result = new List<int>();
        var pending = new Queue<int>([rootId]);
        while (pending.TryDequeue(out var id))
        {
            foreach (var child in children[id].Where(c => c != rootId && !result.Contains(c)))
            {
                result.Add(child);
                pending.Enqueue(child);
            }
        }

        return result;
    }
}