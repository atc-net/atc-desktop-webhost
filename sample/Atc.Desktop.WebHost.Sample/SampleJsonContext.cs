namespace Atc.Desktop.WebHost.Sample;

/// <summary>Source-generated JSON metadata for the sample's messages.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TextDocument))]
[JsonSerializable(typeof(BurstRequest))]
[JsonSerializable(typeof(BurstResult))]
[JsonSerializable(typeof(BurstItem))]
[JsonSerializable(typeof(SampleMode))]
[JsonSerializable(typeof(DelayRequest))]
[JsonSerializable(typeof(DelayState))]
[JsonSerializable(typeof(SelfTestFiles))]
[JsonSerializable(typeof(SelfTestReport))]
public sealed partial class SampleJsonContext : JsonSerializerContext;