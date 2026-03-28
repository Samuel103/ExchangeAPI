using System.Text.Json;
using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class BodyReadStep : IHandlerStep
{
    public string OutputVariable { get; set; } = "body";

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (context.HttpRequest is null)
        {
            return;
        }

        context.HttpRequest.Body.Position = 0;

        using var reader = new StreamReader(context.HttpRequest.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return;
        }

        using var doc = JsonDocument.Parse(rawBody);

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var dict = doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonObjectConverter.Convert(p.Value));

            // Store full body object
            context.SetVariable(OutputVariable, dict);

            // Also expose each top-level field as its own variable for easy use in SQL/CSV
            foreach (var (key, value) in dict)
            {
                context.SetVariable(key, value);
            }
        }
        else
        {
            // Array or primitive — store as-is
            context.SetVariable(OutputVariable, JsonObjectConverter.Convert(doc.RootElement));
        }
    }
}
