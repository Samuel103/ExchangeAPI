using System.Globalization;

namespace ExchangeAPI.Parser.Execution;

public static class VariableTypeConverter
{
    public static string NormalizeTypeName(string typeName)
    {
        return typeName.Trim().ToLowerInvariant() switch
        {
            "integer" => "int",
            "boolean" => "bool",
            "date" => "datetime",
            _ => typeName.Trim().ToLowerInvariant()
        };
    }

    public static object? ConvertValue(object? value, string typeName)
    {
        var normalized = NormalizeTypeName(typeName);

        if (value is null)
        {
            return null;
        }

        return normalized switch
        {
            "string" => value.ToString(),
            "int" => ConvertToInt(value),
            "long" => ConvertToLong(value),
            "double" => ConvertToDouble(value),
            "decimal" => ConvertToDecimal(value),
            "bool" => ConvertToBool(value),
            "datetime" => ConvertToDateTime(value),
            "object" => value,
            _ => value
        };
    }

    private static int ConvertToInt(object value)
    {
        return value switch
        {
            int i => i,
            long l => checked((int)l),
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            _ => System.Convert.ToInt32(value, CultureInfo.InvariantCulture)
        };
    }

    private static long ConvertToLong(object value)
    {
        return value switch
        {
            long l => l,
            int i => i,
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) => l,
            _ => System.Convert.ToInt64(value, CultureInfo.InvariantCulture)
        };
    }

    private static double ConvertToDouble(object value)
    {
        return value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            string s when double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) => d,
            _ => System.Convert.ToDouble(value, CultureInfo.InvariantCulture)
        };
    }

    private static decimal ConvertToDecimal(object value)
    {
        return value switch
        {
            decimal m => m,
            double d => (decimal)d,
            float f => (decimal)f,
            string s when decimal.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var m) => m,
            _ => System.Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };
    }

    private static bool ConvertToBool(object value)
    {
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i != 0,
            int i => i != 0,
            long l => l != 0,
            _ => System.Convert.ToBoolean(value, CultureInfo.InvariantCulture)
        };
    }

    private static DateTime ConvertToDateTime(object value)
    {
        return value switch
        {
            DateTime dt => dt,
            string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) => dt,
            string s when DateTime.TryParse(s, out var dt) => dt,
            _ => System.Convert.ToDateTime(value, CultureInfo.InvariantCulture)
        };
    }
}
