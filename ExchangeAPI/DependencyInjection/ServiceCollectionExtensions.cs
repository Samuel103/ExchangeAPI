using System.Text.Json;
using ExchangeAPI.Configuration;
using ExchangeAPI.Contracts;
using ExchangeAPI.Parser;
using ExchangeAPI.Services;

namespace ExchangeAPI.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExchangeApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EnvironmentOptions>(configuration);
        services.AddHttpClient();

        // Load sources (DB connections, file paths) from configurations.json
        var workingFolder = configuration["WorkingFolder"] ?? "Scripts";
        var dataFile = configuration["ScriptDataFile"] ?? "configurations.json";
        var configPath = Path.Combine(workingFolder, dataFile);
        var sourcesConfig = LoadSourcesConfiguration(configPath);

        services.AddSingleton(sourcesConfig);
        services.AddSingleton<ISourceRegistry, SourceRegistry>();

        services.AddSingleton<IEndpointDefinitionProvider, EndpointParser>();
        services.AddSingleton<IScriptDefinitionProvider, ScriptDefinitionParser>();
        services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();
        services.AddSingleton<IHandlerParser, HandlerParser>();
        services.AddSingleton<IHandlerExecutor, HandlerExecutor>();
        services.AddSingleton<IDynamicApiServer, DynamicApiServer>();
        services.AddHostedService<ScheduledScriptWorker>();

        return services;
    }

    private static SourcesConfiguration LoadSourcesConfiguration(string path)
    {
        if (!File.Exists(path))
        {
            return new SourcesConfiguration();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SourcesConfiguration>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new SourcesConfiguration();
    }
}