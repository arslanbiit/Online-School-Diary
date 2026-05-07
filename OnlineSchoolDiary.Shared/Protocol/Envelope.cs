using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnlineSchoolDiary.Shared.Protocol;

public sealed record RpcRequest(string Method, string RequestId, JsonElement? Payload);

public sealed record RpcResponse(string RequestId, bool Ok, string? Error, JsonElement? Payload);

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

public static class JsonExt
{
    public static JsonElement ToJsonElement<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Options);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }
}

