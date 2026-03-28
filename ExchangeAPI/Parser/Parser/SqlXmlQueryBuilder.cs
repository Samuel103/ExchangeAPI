using System.Text;
using System.Xml.Linq;

namespace ExchangeAPI.Parser;

public static class SqlXmlQueryBuilder
{
    public static string Build(XElement queryElement)
    {
        var raw = GetChild(queryElement, "String")?.Value;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Trim();
        }

        var insert = GetChild(queryElement, "Insert");
        if (insert is not null)
        {
            return BuildInsert(insert);
        }

        var select = GetChild(queryElement, "Select");
        if (select is not null)
        {
            return BuildSelect(select);
        }

        var fallbackRaw = queryElement.Value;
        if (!string.IsNullOrWhiteSpace(fallbackRaw))
        {
            return fallbackRaw.Trim();
        }

        throw new InvalidOperationException("Query element must contain either <String>, <Insert>, or <Select>.");
    }

    private static string BuildInsert(XElement insert)
    {
        var tableName = GetAttribute(insert, "Table") ?? GetChild(insert, "Into")?.Value;
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException("Insert query requires a table name (Table attribute or <Into> element).");
        }

        var valuesContainer = GetChild(insert, "Values");
        var valueElements = valuesContainer?.Elements().ToList() ?? [];

        if (valueElements.Count == 0)
        {
            throw new InvalidOperationException("Insert query requires at least one value inside <Values>.");
        }

        var columns = new List<string>();
        var expressions = new List<string>();

        foreach (var element in valueElements)
        {
            var columnName = GetAttribute(element, "Name") ?? element.Name.LocalName;
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new InvalidOperationException("Insert value element is missing a column name.");
            }

            var expression = GetAttribute(element, "Value");
            if (string.IsNullOrWhiteSpace(expression))
            {
                expression = element.Value;
            }

            // If value is empty, default to variable binding with same name as the column
            expression = string.IsNullOrWhiteSpace(expression)
                ? $"{{{{{columnName}}}}}"
                : expression.Trim();

            columns.Add(EscapeQualifiedIdentifier(columnName));
            expressions.Add(expression);
        }

        return $"INSERT INTO {EscapeQualifiedIdentifier(tableName)} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", expressions)})";
    }

    private static string BuildSelect(XElement select)
    {
        var tableName = GetAttribute(select, "Table") ?? GetChild(select, "From")?.Value;
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException("Select query requires a source table (Table attribute or <From> element).");
        }

        var columns = BuildSelectColumns(GetChild(select, "Columns"));
        var sb = new StringBuilder();
        sb.Append("SELECT ").Append(columns).Append(" FROM ").Append(EscapeQualifiedIdentifier(tableName));

        var where = GetChild(select, "Where")?.Value;
        if (!string.IsNullOrWhiteSpace(where))
        {
            sb.Append(" WHERE ").Append(where.Trim());
        }

        var orderBy = GetChild(select, "OrderBy")?.Value;
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            sb.Append(" ORDER BY ").Append(orderBy.Trim());
        }

        return sb.ToString();
    }

    private static string BuildSelectColumns(XElement? columnsElement)
    {
        if (columnsElement is null)
        {
            return "*";
        }

        var byChildElements = columnsElement.Elements().ToList();
        if (byChildElements.Count > 0)
        {
            var cols = byChildElements
                .Select(e =>
                {
                    var raw = e.Attribute("Name")?.Value ?? e.Value;
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        raw = e.Name.LocalName;
                    }

                    return raw.Trim();
                })
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(EscapeQualifiedIdentifier)
                .ToList();

            return cols.Count == 0 ? "*" : string.Join(", ", cols);
        }

        var commaSeparated = columnsElement.Value;
        if (string.IsNullOrWhiteSpace(commaSeparated))
        {
            return "*";
        }

        var split = commaSeparated
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(EscapeQualifiedIdentifier)
            .ToList();

        return split.Count == 0 ? "*" : string.Join(", ", split);
    }

    private static string EscapeQualifiedIdentifier(string identifier)
    {
        return string.Join(
            ".",
            identifier
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => $"[{part.Replace("]", "]]", StringComparison.Ordinal)}]"));
    }

    private static string? GetAttribute(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(a =>
            string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static XElement? GetChild(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
    }
}
