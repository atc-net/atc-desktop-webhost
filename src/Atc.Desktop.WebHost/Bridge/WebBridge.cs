namespace Atc.Desktop.WebHost.Bridge;

/// <summary>
/// The message bridge between the host and the frontend. Messages are JSON text:
/// <list type="bullet">
/// <item>page → host: <c>{ kind: "request", id, type, payload }</c> (no <c>id</c> = no response)</item>
/// <item>page → host: <c>{ kind: "cancel", id }</c></item>
/// <item>host → page: <c>{ kind: "response", id, ok: true, result }</c> or <c>{ kind: "response", id, ok: false, error: { code, message } }</c></item>
/// <item>host → page: <c>{ kind: "event", type, payload }</c></item>
/// </list>
/// <para>
/// Handlers start on the UI thread, so they can call window functions directly. CPU-heavy work
/// should move to the thread pool (<see cref="Task.Run(Action)"/>) to keep the window responsive.
/// </para>
/// </summary>
public sealed class WebBridge
{
    private readonly ConcurrentDictionary<string, BridgeHandler> handlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> outbox = new();
    private readonly Lock sendLock = new();
    private Action<string>? transport;
    private Action<string>? eventSender;

    /// <summary>Gets the request types that have a handler.</summary>
    /// <returns>The request types.</returns>
    public IReadOnlyCollection<string> GetRequestTypes()
        => handlers.Keys.ToArray();

    /// <summary>Adds or replaces the handler for a request type. Apps may replace built-in handlers.</summary>
    /// <param name="type">The request type, e.g. "md.render".</param>
    /// <param name="handler">The handler.</param>
    public void Handle(
        string type,
        BridgeHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(handler);
        handlers[type] = handler;
    }

    /// <summary>Adds a typed handler using source-generated JSON metadata (trimming and AOT safe).</summary>
    /// <typeparam name="TRequest">The payload type.</typeparam>
    /// <typeparam name="TResponse">The result type.</typeparam>
    /// <param name="type">The request type.</param>
    /// <param name="requestType">Metadata for the payload.</param>
    /// <param name="responseType">Metadata for the result.</param>
    /// <param name="handler">The handler.</param>
    public void Handle<TRequest, TResponse>(
        string type,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<TResponse> responseType,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);
        ArgumentNullException.ThrowIfNull(handler);

        Handle(type, async (request, cancellationToken) =>
        {
            var payload = request.Deserialize(requestType);
            var result = await handler(payload, cancellationToken).ConfigureAwait(true);
            return JsonPayload.From(result, responseType);
        });
    }

    /// <summary>Adds a typed handler for a request without a payload.</summary>
    /// <typeparam name="TResponse">The result type.</typeparam>
    /// <param name="type">The request type.</param>
    /// <param name="responseType">Metadata for the result.</param>
    /// <param name="handler">The handler.</param>
    public void Handle<TResponse>(
        string type,
        JsonTypeInfo<TResponse> responseType,
        Func<CancellationToken, ValueTask<TResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(responseType);
        ArgumentNullException.ThrowIfNull(handler);

        Handle(type, async (_, cancellationToken) =>
        {
            var result = await handler(cancellationToken).ConfigureAwait(true);
            return JsonPayload.From(result, responseType);
        });
    }

    /// <summary>Sends an event to the frontend. Safe to call from any thread, also before the page has loaded.</summary>
    /// <param name="type">The event type, e.g. "file.changed".</param>
    /// <param name="payload">The event data.</param>
    public void Emit(
        string type,
        JsonPayload payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(payload);
        SendEvent(BuildEvent(type, payload));
    }

    /// <summary>Sends a typed event to the frontend.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The event data.</param>
    /// <param name="typeInfo">Metadata for the payload.</param>
    public void Emit<T>(
        string type,
        T payload,
        JsonTypeInfo<T> typeInfo)
        => Emit(type, JsonPayload.From(payload, typeInfo));

    internal static string BuildEvent(
        string type,
        JsonPayload payload)
        => string.Concat(
            "{\"kind\":\"event\",\"type\":\"",
            JsonEncodedText.Encode(type).ToString(),
            "\",\"payload\":",
            payload.Json,
            "}");

    internal static string BuildResponse(
        string id,
        JsonPayload result)
        => string.Concat(
            "{\"kind\":\"response\",\"id\":",
            id,
            ",\"ok\":true,\"result\":",
            result.Json,
            "}");

    internal static string BuildError(
        string id,
        string code,
        string message)
        => string.Concat(
            "{\"kind\":\"response\",\"id\":",
            id,
            ",\"ok\":false,\"error\":{\"code\":\"",
            JsonEncodedText.Encode(code).ToString(),
            "\",\"message\":\"",
            JsonEncodedText.Encode(message).ToString(),
            "\"}}");

    /// <summary>Connects the bridge to the window. Responses flow from now on; events wait for <see cref="OpenEvents"/>.</summary>
    /// <param name="send">Delivers one message to the page. Must be safe to call from any thread.</param>
    internal void Connect(Action<string> send)
        => transport = send;

    /// <summary>The page is listening (<c>host.ready</c>): deliver queued events in order, then send events directly.</summary>
    internal void OpenEvents()
    {
        var send = transport ?? throw new InvalidOperationException("The bridge is not connected.");
        lock (sendLock)
        {
            while (outbox.TryDequeue(out var message))
            {
                send(message);
            }

            eventSender = send;
        }
    }

    /// <summary>Handles one message from the page. Completes after the response is sent.</summary>
    /// <param name="message">The JSON message.</param>
    /// <returns>A task that completes when the message is handled.</returns>
    internal async Task DispatchAsync(string message)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(message);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var kind = GetString(root, "kind") ?? "request";
            string? id = null;
            if (root.TryGetProperty("id", out var idElement) &&
                idElement.ValueKind is JsonValueKind.Number or JsonValueKind.String)
            {
                id = idElement.GetRawText();
            }

            if (kind == "cancel")
            {
                if (id is not null && inflight.TryGetValue(id, out var running))
                {
                    await running.CancelAsync().ConfigureAwait(true);
                }

                return;
            }

            if (kind != "request")
            {
                return;
            }

            var response = await InvokeHandlerAsync(root, id).ConfigureAwait(true);
            if (id is not null)
            {
                SendResponse(response);
            }
        }
    }

    private static string? GetString(
        JsonElement element,
        string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<string> InvokeHandlerAsync(
        JsonElement root,
        string? id)
    {
        var responseId = id ?? "null";
        using var cancellation = new CancellationTokenSource();
        if (id is not null)
        {
            inflight[id] = cancellation;
        }

        try
        {
            var type = GetString(root, "type")
                ?? throw new BridgeException("bad_request", "The request has no 'type'.");
            if (!handlers.TryGetValue(type, out var handler))
            {
                throw new BridgeException("unknown_type", $"Unknown request '{type}'.");
            }

            var payload = root.TryGetProperty("payload", out var value) ? value : default;
            var result = await handler(new BridgeRequest(type, payload), cancellation.Token).ConfigureAwait(true);
            return BuildResponse(responseId, result ?? JsonPayload.Null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return BuildError(responseId, "cancelled", "The request was cancelled.");
        }
        catch (BridgeException ex)
        {
            return BuildError(responseId, ex.Code, ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return BuildError(responseId, "handler_error", ex.Message);
        }
        finally
        {
            if (id is not null)
            {
                inflight.TryRemove(id, out _);
            }
        }
    }

    private void SendResponse(string message)
        => transport?.Invoke(message);

    private void SendEvent(string message)
    {
        var send = eventSender;
        if (send is null)
        {
            lock (sendLock)
            {
                send = eventSender;
                if (send is null)
                {
                    outbox.Enqueue(message);
                    return;
                }
            }
        }

        send(message);
    }
}