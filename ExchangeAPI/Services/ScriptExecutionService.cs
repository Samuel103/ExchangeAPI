using ExchangeAPI.Configuration;
using ExchangeAPI.Contracts;
using ExchangeAPI.Models;
using Microsoft.Extensions.Options;

namespace ExchangeAPI.Services;

public class ScriptExecutionService : IScriptExecutionService
{
    private readonly IScriptDefinitionProvider _scriptDefinitionProvider;
    private readonly IHandlerParser _handlerParser;
    private readonly IHandlerExecutor _handlerExecutor;
    private readonly EnvironmentOptions _options;
    private readonly ILogger<ScriptExecutionService> _logger;

    public ScriptExecutionService(
        IScriptDefinitionProvider scriptDefinitionProvider,
        IHandlerParser handlerParser,
        IHandlerExecutor handlerExecutor,
        IOptions<EnvironmentOptions> options,
        ILogger<ScriptExecutionService> logger)
    {
        _scriptDefinitionProvider = scriptDefinitionProvider;
        _handlerParser = handlerParser;
        _handlerExecutor = handlerExecutor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ScriptExecutionResult> ExecuteByNameAsync(
        string scriptName,
        HttpRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ScriptExecutionResult
        {
            Success = false,
            TriggeredName = scriptName
        };

        if (string.IsNullOrWhiteSpace(scriptName))
        {
            result.Error = "Script name is required.";
            return result;
        }

        var scriptDefinition = ResolveScript(scriptName);
        if (scriptDefinition is null)
        {
            result.Error = $"Script '{scriptName}' not found in scripts configuration.";
            return result;
        }

        var xmlName = scriptDefinition.Script;
        if (string.IsNullOrWhiteSpace(xmlName))
        {
            result.Error = $"Script '{scriptName}' has empty Script value.";
            return result;
        }

        var scriptPath = Path.Combine(_options.WorkingFolder, _options.ScriptFolder, $"{xmlName}.xml");
        result.ResolvedScriptName = xmlName;
        result.ScriptFilePath = scriptPath;

        if (!File.Exists(scriptPath))
        {
            result.Error = $"Script file not found: {scriptPath}";
            return result;
        }

        try
        {
            var steps = _handlerParser.Parse(scriptPath);
            var response = await _handlerExecutor.ExecuteAsync(steps, request, cancellationToken);

            result.Success = true;
            result.HandlerResponse = response;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script execution failed for '{ScriptName}' ({ScriptPath}).", scriptName, scriptPath);
            result.Error = ex.Message;
            return result;
        }
    }

    private DynamicScript? ResolveScript(string scriptName)
    {
        var scripts = _scriptDefinitionProvider.GetScripts();

        var byName = scripts.FirstOrDefault(s =>
            string.Equals(s.Name, scriptName, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            return byName;
        }

        return scripts.FirstOrDefault(s =>
            string.Equals(s.Script, scriptName, StringComparison.OrdinalIgnoreCase));
    }
}