namespace Atc.Desktop.WebHost.Bridge;

/// <summary>Source-generated JSON metadata for the host's built-in messages.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, double>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(BuiltInMessages.PathRequest))]
[JsonSerializable(typeof(BuiltInMessages.TitleRequest))]
[JsonSerializable(typeof(BuiltInMessages.UrlRequest))]
[JsonSerializable(typeof(BuiltInMessages.FileDialogRequest))]
[JsonSerializable(typeof(BuiltInMessages.FileDialogResult))]
[JsonSerializable(typeof(BuiltInMessages.FileChangedEvent))]
[JsonSerializable(typeof(BuiltInMessages.DocumentError))]
[JsonSerializable(typeof(BuiltInMessages.FirstPaintRequest))]
[JsonSerializable(typeof(BuiltInMessages.HostInfo))]
internal sealed partial class BridgeJsonContext : JsonSerializerContext;