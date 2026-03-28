using ExchangeAPI.Models.Responses;

namespace ExchangeAPI.Parser.Execution;

public class HandlerExecutionContext
{
    public HandlerResponse Response { get; } = new();

    public bool HasReturned { get; private set; }

    public Dictionary<string, object?> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> VariableTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HttpRequest? HttpRequest { get; }

    public HandlerExecutionContext(HttpRequest? httpRequest = null)
    {
        HttpRequest = httpRequest;
    }

    public void DeclareVariable(string name, string typeName)
    {
        var normalized = VariableTypeConverter.NormalizeTypeName(typeName);
        VariableTypes[name] = normalized;

        if (!Variables.ContainsKey(name))
        {
            Variables[name] = null;
        }
    }

    public void SetVariable(string name, object? value)
    {
        if (VariableTypes.TryGetValue(name, out var typeName))
        {
            value = VariableTypeConverter.ConvertValue(value, typeName);
        }

        Variables[name] = value;
    }

    public object? GetVariable(string name) =>
        Variables.TryGetValue(name, out var v) ? v : null;

    public void Return(int statusCode, object? data)
    {
        Response.StatusCode = statusCode;
        Response.Data = data;
        HasReturned = true;
    }
}
