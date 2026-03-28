using System.Text.Json;

namespace ExchangeAPI.Parser.Execution;

public static class ReturnPayloadParser
{
    public static object? Parse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return trimmed;
        }
    }
}
