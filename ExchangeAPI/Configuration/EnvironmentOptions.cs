namespace ExchangeAPI.Configuration;

public class EnvironmentOptions
{
    public string WorkingFolder { get; set; } = "Scripts";
    public string EndpointsFile { get; set; } = "endpoints.json";
    public string ScriptDataFile { get; set; } = "configurations.json";
    public string HandlerFolder { get; set; } = "Handler";
    public string ScriptsFile { get; set; } = "scripts.json";
    public string ScriptFolder { get; set; } = "Script";
}