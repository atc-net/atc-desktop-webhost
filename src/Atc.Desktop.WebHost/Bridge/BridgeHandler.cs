namespace Atc.Desktop.WebHost.Bridge;

/// <summary>Handles one request from the frontend.</summary>
/// <param name="request">The request.</param>
/// <param name="cancellationToken">Cancelled when the page cancels the request.</param>
/// <returns>The result, sent back as <c>result</c>.</returns>
public delegate ValueTask<JsonPayload> BridgeHandler(
    BridgeRequest request,
    CancellationToken cancellationToken);