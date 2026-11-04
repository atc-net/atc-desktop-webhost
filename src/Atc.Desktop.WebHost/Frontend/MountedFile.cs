namespace Atc.Desktop.WebHost.Frontend;

/// <summary>A file served from a mount: its content (in memory, owned by the caller) and response headers.</summary>
/// <param name="Content">The file content, positioned at the start.</param>
/// <param name="ContentType">The Content-Type header value.</param>
/// <param name="CacheControl">The Cache-Control header value, or <see langword="null"/>.</param>
internal sealed record MountedFile(
    MemoryStream Content,
    string ContentType,
    string? CacheControl);