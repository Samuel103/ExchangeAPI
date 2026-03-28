using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class ReturnStep : IHandlerStep
{
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    /// <summary>Raw template string. May contain {{varName}} references resolved at execute time.</summary>
    public string? DataTemplate { get; set; }

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var resolved = VariableInterpolator.Interpolate(DataTemplate, context.Variables);

        // If interpolation returned a plain string, try to parse it as JSON
        var data = resolved is string str
            ? ReturnPayloadParser.Parse(str)
            : resolved;

        context.Return(StatusCode, data);
        return Task.CompletedTask;
    }
}