using System.Text.Json.Serialization;

namespace AtcBench;

/// <summary>Source-generated JSON, like the host uses, so no reflection-based serializer warms up at startup.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BenchDocument))]
internal sealed partial class BenchJsonContext : JsonSerializerContext;
