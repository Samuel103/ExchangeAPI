using ExchangeAPI.Configuration;
using ExchangeAPI.Contracts;
using Microsoft.Extensions.Options;

namespace ExchangeAPI.Services;

public interface IDynamicApiServer
{
    void MapEndpoints(WebApplication app);
}

public class DynamicApiServer : IDynamicApiServer
{
    private readonly IEndpointDefinitionProvider _endpointDefinitionProvider;
    private readonly IHandlerParser _handlerParser;
    private readonly IHandlerExecutor _handlerExecutor;
    private readonly EnvironmentOptions _options;
    private readonly ILogger<DynamicApiServer> _logger;

    public DynamicApiServer(
        IEndpointDefinitionProvider endpointDefinitionProvider,
        IHandlerParser handlerParser,
        IHandlerExecutor handlerExecutor,
        IOptions<EnvironmentOptions> options,
        ILogger<DynamicApiServer> logger)
    {
        _endpointDefinitionProvider = endpointDefinitionProvider;
        _handlerParser = handlerParser;
        _handlerExecutor = handlerExecutor;
        _options = options.Value;
        _logger = logger;
    }

    public void MapEndpoints(WebApplication app)
    {
        var endpoints = _endpointDefinitionProvider.GetEndpoints();

        foreach (var endpoint in endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Path) ||
                string.IsNullOrWhiteSpace(endpoint.Method) ||
                string.IsNullOrWhiteSpace(endpoint.Handler))
            {
                _logger.LogWarning("Skipping invalid endpoint definition.");
                continue;
            }

            var method = endpoint.Method.Trim().ToUpperInvariant();
            var handlerPath = Path.Combine(_options.WorkingFolder, _options.HandlerFolder, $"{endpoint.Handler}.xml");

            app.MapMethods(endpoint.Path, new[] { method }, async (HttpContext context) =>
            {
                var steps = _handlerParser.Parse(handlerPath);
                var response = await _handlerExecutor.ExecuteAsync(steps, context.RequestAborted);

                if (response.Data is null)
                {
                    return Results.StatusCode(response.StatusCode);
                }

                return Results.Json(response.Data, statusCode: response.StatusCode);
            });

            _logger.LogInformation("Mapped {Method} {Path} with handler {HandlerPath}", method, endpoint.Path, handlerPath);
        }
    }
}