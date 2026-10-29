namespace Atc.Desktop.WebHost.Sample;

/// <summary>Source-generated JSON metadata for the sample's messages.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TextDocument))]
[JsonSerializable(typeof(BurstRequest))]
[JsonSerializable(typeof(BurstResult))]
[JsonSerializable(typeof(BurstItem))]
public sealed partial class SampleJsonContext : JsonSerializerContext;