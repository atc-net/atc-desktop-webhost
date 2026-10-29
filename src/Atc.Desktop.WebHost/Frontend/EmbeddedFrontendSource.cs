namespace Atc.Desktop.WebHost.Frontend;

/// <summary>Frontend files embedded in an assembly: one file to ship, nothing on disk to tamper with.</summary>
internal sealed class EmbeddedFrontendSource : FrontendSource
{
    private readonly Assembly assembly;
    private readonly Dictionary<string, string> resourceNames;

    public EmbeddedFrontendSource(
        Assembly assembly,
        string prefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(prefix);

        this.assembly = assembly;

        // MSBuild's %(RecursiveDir) uses '\' on Windows; normalize so "assets/app.js" always matches.
        var normalizedPrefix = prefix.Replace('\\', '/');
        resourceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var normalized = name.Replace('\\', '/');
            if (normalized.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            {
                resourceNames[normalized[normalizedPrefix.Length..]] = name;
            }
        }

        if (resourceNames.Count == 0)
        {
            throw new InvalidOperationException(
                $"No embedded resources starting with '{prefix}' in {assembly.GetName().Name}. Was the frontend built and embedded?");
        }
    }

    public override Stream? TryOpen(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        return resourceNames.TryGetValue(relativePath.Replace('\\', '/'), out var name)
            ? assembly.GetManifestResourceStream(name)
            : null;
    }
}