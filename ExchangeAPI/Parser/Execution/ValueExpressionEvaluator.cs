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
            "getfield" => EvaluateGetFieldNode(element, context),
            "getpath" => EvaluateGetFieldNode(element, context),
            "addition" => EvaluateAdditionNode(element, context),
            "substract" => EvaluateSubstractNode(element, context),
            "subtract" => EvaluateSubstractNode(element, context),
            "multiply" => EvaluateMultiplyNode(element, context),
            "divide" => EvaluateDivideNode(element, context),
            "concat" => EvaluateConcatNode(element, context),
            "equals" => EvaluateEqualsNode(element, context),
            "notequals" => !EvaluateEqualsNode(element, context),
            "greaterthan" => EvaluateCompareNode(element, context) > 0,
            "greaterorequal" => EvaluateCompareNode(element, context) >= 0,
            "lessthan" => EvaluateCompareNode(element, context) < 0,
            "lessorequal" => EvaluateCompareNode(element, context) <= 0,
            "and" => EvaluateAndNode(element, context),
            "or" => EvaluateOrNode(element, context),
            "not" => !ConvertToBool(EvaluateUnaryNode(element, context)),
            "coalesce" => EvaluateCoalesceNode(element, context),
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

    private static object? EvaluateGetFieldNode(XElement element, HandlerExecutionContext context)
    {
        var from = GetAttribute(element, "From");
        var path = GetAttribute(element, "Field") ?? GetAttribute(element, "Path");

        var fromElement = element.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, "From", StringComparison.OrdinalIgnoreCase));
        var pathElement = element.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, "Path", StringComparison.OrdinalIgnoreCase));

        var source = fromElement is not null
            ? EvaluateContainerOrValue(fromElement, context)
            : (!string.IsNullOrWhiteSpace(from)
                ? context.GetVariable(from)
                : EvaluateUnaryNode(element, context));

        var resolvedPath = !string.IsNullOrWhiteSpace(path)
            ? path
            : pathElement?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            throw new InvalidOperationException("GetField/GetPath requires a Field or Path value.");
        }

        return ReadPath(source, resolvedPath);
    }

    private static object? ReadPath(object? source, string path)
    {
        var current = source;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            if (current is IDictionary<string, object?> dict)
            {
                var key = dict.Keys.FirstOrDefault(k => string.Equals(k, segment, StringComparison.OrdinalIgnoreCase));
                current = key is null ? null : dict[key];
                continue;
            }

            if (current is IList<object?> list && int.TryParse(segment, out var index))
            {
                current = index >= 0 && index < list.Count ? list[index] : null;
                continue;
            }

            return null;
        }

        return current;
    }

    private static object? EvaluateAdditionNode(XElement element, HandlerExecutionContext context)
    {
        var values = EvaluateArgs(element, context).ToList();
        if (values.Count == 0)
        {
            return 0m;
        }

        decimal total = 0m;
        foreach (var value in values)
        {
            total += ConvertToDecimal(value);
        }

        return total;
    }

    private static object? EvaluateSubstractNode(XElement element, HandlerExecutionContext context)
    {
        var values = EvaluateArgs(element, context).ToList();
        if (values.Count == 0)
        {
            return 0m;
        }

        var result = ConvertToDecimal(values[0]);
        foreach (var value in values.Skip(1))
        {
            result -= ConvertToDecimal(value);
        }

        return result;
    }

    private static object? EvaluateMultiplyNode(XElement element, HandlerExecutionContext context)
    {
        var values = EvaluateArgs(element, context).ToList();
        if (values.Count == 0)
        {
            return 0m;
        }

        decimal result = 1m;
        foreach (var value in values)
        {
            result *= ConvertToDecimal(value);
        }

        return result;
    }

    private static object? EvaluateDivideNode(XElement element, HandlerExecutionContext context)
    {
        var values = EvaluateArgs(element, context).ToList();
        if (values.Count == 0)
        {
            return 0m;
        }

        var result = ConvertToDecimal(values[0]);
        foreach (var value in values.Skip(1))
        {
            var divisor = ConvertToDecimal(value);
            if (divisor == 0m)
            {
                throw new InvalidOperationException("Divide expression cannot divide by zero.");
            }

            result /= divisor;
        }

        return result;
    }

    private static object? EvaluateConcatNode(XElement element, HandlerExecutionContext context)
    {
        var values = EvaluateArgs(element, context)
            .Select(v => v?.ToString() ?? string.Empty);

        return string.Concat(values);
    }

    private static bool EvaluateEqualsNode(XElement element, HandlerExecutionContext context)
    {
        var (left, right) = EvaluateLeftRight(element, context);

        if (TryConvertToDecimal(left, out var leftDecimal) && TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal == rightDecimal;
        }

        if (TryConvertToDateTime(left, out var leftDate) && TryConvertToDateTime(right, out var rightDate))
        {
            return leftDate == rightDate;
        }

        return string.Equals(
            left?.ToString() ?? string.Empty,
            right?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int EvaluateCompareNode(XElement element, HandlerExecutionContext context)
    {
        var (left, right) = EvaluateLeftRight(element, context);

        if (TryConvertToDecimal(left, out var leftDecimal) && TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal.CompareTo(rightDecimal);
        }

        if (TryConvertToDateTime(left, out var leftDate) && TryConvertToDateTime(right, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.Compare(
            left?.ToString() ?? string.Empty,
            right?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateAndNode(XElement element, HandlerExecutionContext context)
    {
        var args = EvaluateArgs(element, context).ToList();
        if (args.Count == 0)
        {
            return false;
        }

        return args.All(ConvertToBool);
    }

    private static bool EvaluateOrNode(XElement element, HandlerExecutionContext context)
    {
        var args = EvaluateArgs(element, context).ToList();
        if (args.Count == 0)
        {
            return false;
        }

        return args.Any(ConvertToBool);
    }

    private static object? EvaluateCoalesceNode(XElement element, HandlerExecutionContext context)
    {
        foreach (var value in EvaluateArgs(element, context))
        {
            if (value is null)
            {
                continue;
            }

            if (value is string s && string.IsNullOrWhiteSpace(s))
            {
                continue;
            }

            return value;
        }

        return null;
    }

    private static IEnumerable<object?> EvaluateArgs(XElement element, HandlerExecutionContext context)
    {
        var argElements = element.Elements()
            .Where(e => string.Equals(e.Name.LocalName, "Arg", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (argElements.Count > 0)
        {
            foreach (var arg in argElements)
            {
                yield return EvaluateArgNode(arg, context);
            }

            yield break;
        }

        foreach (var child in element.Elements())
        {
            yield return Evaluate(child, context);
        }
    }

    private static (object? left, object? right) EvaluateLeftRight(XElement element, HandlerExecutionContext context)
    {
        var leftElement = element.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, "Left", StringComparison.OrdinalIgnoreCase));
        var rightElement = element.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, "Right", StringComparison.OrdinalIgnoreCase));

        if (leftElement is not null && rightElement is not null)
        {
            return (EvaluateContainerOrValue(leftElement, context), EvaluateContainerOrValue(rightElement, context));
        }

        var evaluatedChildren = element.Elements()
            .Select(child => Evaluate(child, context))
            .Take(2)
            .ToList();

        var left = evaluatedChildren.Count > 0 ? evaluatedChildren[0] : null;
        var right = evaluatedChildren.Count > 1 ? evaluatedChildren[1] : null;
        return (left, right);
    }

    private static object? EvaluateContainerOrValue(XElement container, HandlerExecutionContext context)
    {
        var firstChild = container.Elements().FirstOrDefault();
        if (firstChild is not null)
        {
            return Evaluate(firstChild, context);
        }

        var raw = container.Value;
        return VariableInterpolator.Interpolate(string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(), context.Variables);
    }

    private static decimal ConvertToDecimal(object? value)
    {
        if (!TryConvertToDecimal(value, out var result))
        {
            throw new InvalidOperationException($"Cannot convert '{value}' to decimal for arithmetic expression.");
        }

        return result;
    }

    private static bool TryConvertToDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case null:
                result = 0m;
                return false;
            case decimal m:
                result = m;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case double d:
                result = (decimal)d;
                return true;
            case float f:
                result = (decimal)f;
                return true;
            case string s when decimal.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0m;
                return false;
        }
    }

    private static bool TryConvertToDateTime(object? value, out DateTime result)
    {
        if (value is DateTime dt)
        {
            result = dt;
            return true;
        }

        if (value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = default;
        return false;
    }

    private static bool ConvertToBool(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s when bool.TryParse(s, out var parsedBool) => parsedBool,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) => parsedInt != 0,
            int i => i != 0,
            long l => l != 0,
            decimal m => m != 0m,
            double d => Math.Abs(d) > double.Epsilon,
            _ => true
        };
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
