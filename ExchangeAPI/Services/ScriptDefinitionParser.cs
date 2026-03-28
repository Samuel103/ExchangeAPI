using System.Text.Json;
using ExchangeAPI.Configuration;
using ExchangeAPI.Contracts;
using ExchangeAPI.Models;
using Microsoft.Extensions.Options;

namespace ExchangeAPI.Services;

public class ScriptDefinitionParser : IScriptDefinitionProvider
{
    private readonly EnvironmentOptions _options;
    private readonly ILogger<ScriptDefinitionParser> _logger;

    public ScriptDefinitionParser(
        IOptions<EnvironmentOptions> options,
        ILogger<ScriptDefinitionParser> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<DynamicScript> GetScripts()
    {
        var scriptsFilePath = Path.Combine(_options.WorkingFolder, _options.ScriptsFile);

        if (!File.Exists(scriptsFilePath))
        {
            _logger.LogWarning("Scripts config file not found: {Path}", scriptsFilePath);
            return [];
        }

        var json = File.ReadAllText(scriptsFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var scripts = JsonSerializer.Deserialize<List<DynamicScript>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return scripts ?? [];
    }
}