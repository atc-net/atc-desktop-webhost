namespace Atc.Desktop.WebHost.Tests.Bridge;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TitleMessage))]
internal sealed partial class TestJsonContext : JsonSerializerContext;