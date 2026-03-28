using System.Text.Json;

namespace ExchangeAPI.Parser.Execution;

public static class JsonObjectConverter
{
    public static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String  => element.GetString(),
        JsonValueKind.Number  => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True    => true,
        JsonValueKind.False   => false,
        JsonValueKind.Null    => null,
        JsonValueKind.Object  => element.EnumerateObject()
                                     .ToDictionary(p => p.Name, p => Convert(p.Value)),
        JsonValueKind.Array   => element.EnumerateArray()
                                     .Select(Convert)
                                     .ToList<object?>(),
        _                     => element.GetRawText()
    };
}
