using System.Globalization;
using ExchangeAPI.Configuration;
using ExchangeAPI.Contracts;
using ExchangeAPI.Models;
using Microsoft.Extensions.Options;

namespace ExchangeAPI.Services;

public class ScheduledScriptWorker : BackgroundService
{
    private readonly IScriptDefinitionProvider _scriptDefinitionProvider;
    private readonly IScriptExecutionService _scriptExecutionService;
    private readonly ILogger<ScheduledScriptWorker> _logger;

    private readonly Dictionary<string, DateOnly> _lastRunByScript = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _startupRunCompleted = new(StringComparer.OrdinalIgnoreCase);

    public ScheduledScriptWorker(
        IScriptDefinitionProvider scriptDefinitionProvider,
        IScriptExecutionService scriptExecutionService,
        ILogger<ScheduledScriptWorker> logger)
    {
        _scriptDefinitionProvider = scriptDefinitionProvider;
        _scriptExecutionService = scriptExecutionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledScriptWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scripts = _scriptDefinitionProvider.GetScripts()
                    .Where(s => s.Enabled)
                    .ToList();

                var nowUtc = DateTimeOffset.UtcNow;

                foreach (var script in scripts)
                {
                    await TryRunOnStartup(script, stoppingToken);
                    await TryRunOnSchedule(script, nowUtc, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in scheduled script loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    private async Task TryRunOnStartup(DynamicScript script, CancellationToken cancellationToken)
    {
        var key = GetScriptKey(script);
        if (!script.RunOnStartup || _startupRunCompleted.Contains(key))
        {
            return;
        }

        _startupRunCompleted.Add(key);
        await RunScript(script, "startup", cancellationToken);
    }

    private async Task TryRunOnSchedule(DynamicScript script, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var key = GetScriptKey(script);
        if (!TryResolveTimeZone(script, out var timeZone, out var usedFallback))
        {
            return;
        }

        if (usedFallback)
        {
            _logger.LogWarning(
                "Unknown timezone '{TimeZone}' for script '{ScriptName}', using local timezone '{LocalTimeZone}'.",
                script.TimeZone,
                script.Name,
                timeZone.Id);
        }

        if (!TryParseScriptTime(script.Time, out var runHour, out var runMinute))
        {
            _logger.LogWarning("Invalid time '{Time}' for script '{ScriptName}'. Expected HH:mm.", script.Time, script.Name);
            return;
        }

        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        if (localNow.Hour != runHour || localNow.Minute != runMinute)
        {
            return;
        }

        var today = DateOnly.FromDateTime(localNow.Date);
        if (_lastRunByScript.TryGetValue(key, out var lastRunDate) && lastRunDate == today)
        {
            return;
        }

        _lastRunByScript[key] = today;
        await RunScript(script, $"schedule {script.Time} ({timeZone.Id})", cancellationToken);
    }

    private async Task RunScript(DynamicScript script, string trigger, CancellationToken cancellationToken)
    {
        var executionName = !string.IsNullOrWhiteSpace(script.Name) ? script.Name : script.Script;

        try
        {
            _logger.LogInformation("Executing scheduled script '{ScriptName}' ({Trigger}).", script.Name, trigger);
            var result = await _scriptExecutionService.ExecuteByNameAsync(executionName, request: null, cancellationToken: cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Scheduled script '{ScriptName}' failed: {Error}", script.Name, result.Error);
                return;
            }

            _logger.LogInformation(
                "Scheduled script '{ScriptName}' completed with status {StatusCode}.",
                script.Name,
                result.HandlerResponse?.StatusCode ?? StatusCodes.Status200OK);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled script '{ScriptName}' failed.", script.Name);
        }
    }

    private static bool TryResolveTimeZone(DynamicScript script, out TimeZoneInfo timeZone, out bool usedFallback)
    {
        usedFallback = false;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(script.TimeZone);
            return true;
        }
        catch
        {
            timeZone = TimeZoneInfo.Local;
            usedFallback = true;
            return true;
        }
    }

    private static string GetScriptKey(DynamicScript script)
    {
        if (!string.IsNullOrWhiteSpace(script.Name))
        {
            return script.Name;
        }

        if (!string.IsNullOrWhiteSpace(script.Script))
        {
            return script.Script;
        }

        return "__unnamed_script__";
    }

    private static bool TryParseScriptTime(string value, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;

        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        hour = parsed.Hour;
        minute = parsed.Minute;
        return true;
    }
}