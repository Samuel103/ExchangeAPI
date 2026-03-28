using System.Globalization;
using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class StringToDateTimeStep : IHandlerStep
{
    public string Name { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var value = context.GetVariable(From)?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"StringToDateTime missing source variable '{From}'.");
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            && !DateTime.TryParse(value, out dt))
        {
            throw new InvalidOperationException($"StringToDateTime could not parse variable '{From}' value '{value}'.");
        }

        context.SetVariable(Name, dt);
        return Task.CompletedTask;
    }
}
