using System.Globalization;
using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class StringFormatStep : IHandlerStep
{
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Vars { get; set; } = string.Empty;

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var names = Vars
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var args = names
            .Select(n => context.GetVariable(n))
            .ToArray();

        var formatted = string.Format(CultureInfo.InvariantCulture, Format, args);
        context.SetVariable(Name, formatted);

        return Task.CompletedTask;
    }
}
