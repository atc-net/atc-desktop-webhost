namespace Atc.Desktop.WebHost.Bridge;

/// <summary>A request from the frontend.</summary>
public sealed class BridgeRequest
{
    internal BridgeRequest(
        string type,
        JsonElement payload)
    {
        Type = type;
        Payload = payload;
    }

    /// <summary>Gets the request type, e.g. "settings.get".</summary>
    public string Type { get; }

    /// <summary>
    /// Gets the payload. Valid only until the handler's task completes; call
    /// <see cref="JsonElement.Clone"/> to keep it longer.
    /// </summary>
    public JsonElement Payload { get; }

    /// <summary>Deserializes the payload with source-generated metadata.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="typeInfo">The source-generated type info.</param>
    /// <returns>The payload.</returns>
    /// <exception cref="BridgeException">The payload is missing or doesn't match <typeparamref name="T"/>.</exception>
    public T Deserialize<T>(JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        try
        {
            return Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? throw new BridgeException("bad_request", $"'{Type}' needs a payload.")
                : Payload.Deserialize(typeInfo)
                  ?? throw new BridgeException("bad_request", $"'{Type}' needs a payload.");
        }
        catch (JsonException ex)
        {
            throw new BridgeException("bad_request", $"Invalid payload for '{Type}': {ex.Message}");
        }
    }
}