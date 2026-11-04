namespace Atc.Desktop.WebHost.Frontend;

/// <summary>Routes requests on the files origin to the app's <see cref="FrontendMount"/>s. Thread-safe (read-only after creation).</summary>
internal sealed class MountTable
{
    private readonly Dictionary<string, FrontendMount> mounts;

    public MountTable(IReadOnlyList<FrontendMount> mounts)
    {
        ArgumentNullException.ThrowIfNull(mounts);

        this.mounts = new Dictionary<string, FrontendMount>(StringComparer.OrdinalIgnoreCase);
        foreach (var mount in mounts)
        {
            if (!IsValidName(mount.Name))
            {
                throw new ArgumentException($"The mount name '{mount.Name}' must be one path segment of letters, digits, '-', '_' or '.'.", nameof(mounts));
            }

            if (!this.mounts.TryAdd(mount.Name, mount))
            {
                throw new ArgumentException($"The mount name '{mount.Name}' is used twice.", nameof(mounts));
            }
        }
    }

    public bool IsEmpty
        => mounts.Count == 0;

    /// <summary>Opens a file by its path below the files origin, e.g. "doc/images/logo.png".</summary>
    /// <param name="path">The unescaped path, without the leading '/'.</param>
    /// <returns>The file, or <see langword="null"/> (unknown mount, a ".." segment, or the source has no such file).</returns>
    public MountedFile? Open(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var segments = path.Replace('\\', '/').Split('/');
        if (segments.Length < 2 ||
            segments.Any(s => s is ".." or ".") ||
            !mounts.TryGetValue(segments[0], out var mount))
        {
            return null;
        }

        var relativePath = string.Join('/', segments.Skip(1));
        try
        {
            using var stream = mount.Source.TryOpen(relativePath);
            if (stream is null)
            {
                return null;
            }

            var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;
            return new MountedFile(memory, ContentTypes.FromPath(relativePath), mount.CacheControl);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Being written or replaced right now: the page can load it again a moment later.
            return null;
        }
    }

    private static bool IsValidName(string? name)
        => !string.IsNullOrEmpty(name) &&
           name is not ("." or "..") &&
           name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}