using System.Text.Json;
using ExchangeAPI.Configuration;
using ExchangeAPI.Contracts;
using ExchangeAPI.Models;
using Microsoft.Extensions.Options;

namespace ExchangeAPI.Services;

public class EndpointParser : IEndpointDefinitionProvider
{
    private readonly EnvironmentOptions _options;

    public EndpointParser(IOptions<EnvironmentOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<DynamicEndpoint> GetEndpoints()
    {
        var endpointsFilePath = Path.Combine(_options.WorkingFolder, _options.EndpointsFile);

        if (!File.Exists(endpointsFilePath))
        {
            throw new FileNotFoundException($"Endpoint config file not found: {endpointsFilePath}");
        }

        var json = File.ReadAllText(endpointsFilePath);
        var endpoints = JsonSerializer.Deserialize<List<DynamicEndpoint>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return endpoints ?? [];
    }
}
