using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public interface IHandlerStep
{
    Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default);
}