namespace Atc.Desktop.WebHost.Bridge;

/// <summary>
/// A JSON value that is already serialized. The bridge splices it into messages as-is, so a large
/// document is serialized once and never parsed again on the .NET side.
/// </summary>
public sealed class JsonPayload
{
    private JsonPayload(string json)
        => Json = json;

    /// <summary>Gets the JSON <c>null</c> value.</summary>
    public static JsonPayload Null { get; } = new("null");

    /// <summary>Gets the serialized JSON text.</summary>
    public string Json { get; }

    /// <summary>Wraps JSON text that is already serialized. The text must be one valid JSON value.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The payload.</returns>
    public static JsonPayload FromRawJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return new JsonPayload(json);
    }

    /// <summary>Serializes a value with source-generated metadata (trimming and AOT safe).</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="typeInfo">The source-generated type info.</param>
    /// <returns>The payload.</returns>
    public static JsonPayload From<T>(
        T value,
        JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return new JsonPayload(JsonSerializer.Serialize(value, typeInfo));
    }

    /// <summary>Serializes a JSON node.</summary>
    /// <param name="node">The node; <see langword="null"/> becomes JSON <c>null</c>.</param>
    /// <returns>The payload.</returns>
    public static JsonPayload From(JsonNode? node)
        => node is null ? Null : new JsonPayload(node.ToJsonString());

    /// <summary>Serializes a string as a JSON string value.</summary>
    /// <param name="value">The string; <see langword="null"/> becomes JSON <c>null</c>.</param>
    /// <returns>The payload.</returns>
    public static JsonPayload FromString(string? value)
        => value is null ? Null : new JsonPayload(JsonSerializer.Serialize(value, BridgeJsonContext.Default.String));

    /// <inheritdoc />
    public override string ToString()
        => Json;
}