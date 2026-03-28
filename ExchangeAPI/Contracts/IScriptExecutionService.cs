using ExchangeAPI.Models.Responses;

namespace ExchangeAPI.Contracts;

public interface IScriptExecutionService
{
    Task<ScriptExecutionResult> ExecuteByNameAsync(
        string scriptName,
        HttpRequest? request = null,
        CancellationToken cancellationToken = default);
}

public class ScriptExecutionResult
{
    public bool Success { get; set; }
    public string TriggeredName { get; set; } = string.Empty;
    public string? ResolvedScriptName { get; set; }
    public string? ScriptFilePath { get; set; }
    public string? Error { get; set; }
    public HandlerResponse? HandlerResponse { get; set; }
}