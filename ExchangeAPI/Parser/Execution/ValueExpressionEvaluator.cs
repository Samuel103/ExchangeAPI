using System.Globalization;
using System.Xml.Linq;

namespace ExchangeAPI.Parser.Execution;

public static class ValueExpressionEvaluator
{
    public static object? Evaluate(XElement element, HandlerExecutionContext context)
    {
        var nodeName = element.Name.LocalName.ToLowerInvariant();

        return nodeName switch
        {
            "value" => EvaluateValueNode(element, context),
            "get" => EvaluateGetNode(element, context),
            "stringformat" => EvaluateStringFormatNode(element, context),
            "stringtoint" => VariableTypeConverter.ConvertValue(EvaluateUnaryNode(element, context), "int"),
            "inttostring" => VariableTypeConverter.ConvertValue(EvaluateUnaryNode(element, context), "string"),
            "stringtodatetime" => VariableTypeConverter.ConvertValue(EvaluateUnaryNode(element, context), "datetime"),
            "stringsubstring" => EvaluateStringSubstringNode(element, context),
            "arg" => EvaluateArgNode(element, context),
            _ => EvaluateUnknownNode(element, context)
        };
    }

    private static object? EvaluateValueNode(XElement element, HandlerExecutionContext context)
    {
        var firstChild = element.Elements().FirstOrDefault();
        if (firstChild is not null)
        {
            return Evaluate(firstChild, context);
        }

        var raw = element.Value;
        return VariableInterpolator.Interpolate(string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(), context.Variables);
    }

    private static object? EvaluateGetNode(XElement element, HandlerExecutionContext context)
    {
        var name = GetAttribute(element, "Name") ?? GetAttribute(element, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Get expression requires Name attribute.");
        }

        return context.GetVariable(name);
    }

    private static object? EvaluateStringFormatNode(XElement element, HandlerExecutionContext context)
    {
        var format = GetAttribute(element, "Format") ?? string.Empty;
        var args = element.Elements()
            .Select(arg => Evaluate(arg, context))
            .ToArray();

        return string.Format(CultureInfo.InvariantCulture, format, args);
    }

    private static object? EvaluateStringSubstringNode(XElement element, HandlerExecutionContext context)
    {
        var start = ParseIntOrDefault(GetAttribute(element, "Start"), 0);
        var length = ParseNullableInt(GetAttribute(element, "Length"));

        var sourceValue = EvaluateUnaryNode(element, context)?.ToString() ?? string.Empty;

        if (start < 0 || start > sourceValue.Length)
        {
            throw new InvalidOperationException($"StringSubstring invalid Start={start} for value '{sourceValue}'.");
        }

        return length.HasValue
            ? sourceValue.Substring(start, Math.Min(length.Value, sourceValue.Length - start))
            : sourceValue.Substring(start);
    }

    private static object? EvaluateArgNode(XElement element, HandlerExecutionContext context)
    {
        var firstChild = element.Elements().FirstOrDefault();
        if (firstChild is not null)
        {
            return Evaluate(firstChild, context);
        }

        var raw = element.Value;
        return VariableInterpolator.Interpolate(string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(), context.Variables);
    }

    private static object? EvaluateUnknownNode(XElement element, HandlerExecutionContext context)
    {
        if (!element.HasElements)
        {
            var raw = element.Value;
            return VariableInterpolator.Interpolate(string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(), context.Variables);
        }

        return Evaluate(element.Elements().First(), context);
    }

    private static object? EvaluateUnaryNode(XElement element, HandlerExecutionContext context)
    {
        var firstChild = element.Elements().FirstOrDefault();
        if (firstChild is not null)
        {
            return Evaluate(firstChild, context);
        }

        // Prefer nested <Arg> payload when present in text form
        var argElement = element.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, "Arg", StringComparison.OrdinalIgnoreCase));
        if (argElement is not null)
        {
            return EvaluateArgNode(argElement, context);
        }

        var raw = element.Value;
        return VariableInterpolator.Interpolate(string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(), context.Variables);
    }

    private static string? GetAttribute(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(a =>
            string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static int ParseIntOrDefault(string? raw, int defaultValue)
    {
        return int.TryParse(raw, out var value) ? value : defaultValue;
    }

    private static int? ParseNullableInt(string? raw)
    {
        return int.TryParse(raw, out var value) ? value : null;
    }
}
