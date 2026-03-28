using System.Text.RegularExpressions;
using ExchangeAPI.Parser.Execution;
using Microsoft.Data.SqlClient;

namespace ExchangeAPI.Parser;

public class SqlExecuteStep : IHandlerStep
{
    public string Source { get; set; } = string.Empty;
    public string QueryTemplate { get; set; } = string.Empty;
    public string InputVariable { get; set; } = "body";

    /// <summary>Optional: variable that will receive the number of rows affected.</summary>
    public string? OutputVariable { get; set; }

    private readonly string _connectionString;

    public SqlExecuteStep(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var paramNames = new List<string>();
        var parameterizedQuery = Regex.Replace(QueryTemplate, @"\{\{(\w+)\}\}", m =>
        {
            paramNames.Add(m.Groups[1].Value);
            return $"@{m.Groups[1].Value}";
        });

        paramNames = paramNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var expectsScalarOutput = !string.IsNullOrWhiteSpace(OutputVariable)
            && (parameterizedQuery.Contains("OUTPUT INSERTED", StringComparison.OrdinalIgnoreCase)
                || parameterizedQuery.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));

        if (TryGetRowsFromInput(context.GetVariable(InputVariable), out var rows))
        {
            var totalRowsAffected = 0L;
            var scalarResults = new List<object?>();

            foreach (var row in rows)
            {
                await using var cmd = BuildCommand(conn, parameterizedQuery, paramNames, context, row);

                if (expectsScalarOutput)
                {
                    var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
                    scalarResults.Add(scalar is DBNull ? null : scalar);
                }
                else
                {
                    totalRowsAffected += await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            if (!string.IsNullOrWhiteSpace(OutputVariable))
            {
                if (expectsScalarOutput)
                {
                    context.SetVariable(OutputVariable!, scalarResults.Count == 1 ? scalarResults[0] : scalarResults);
                }
                else
                {
                    context.SetVariable(OutputVariable, totalRowsAffected);
                }
            }

            return;
        }

        await using var singleCmd = BuildCommand(conn, parameterizedQuery, paramNames, context, row: null);

        if (expectsScalarOutput)
        {
            var scalar = await singleCmd.ExecuteScalarAsync(cancellationToken);
            context.SetVariable(OutputVariable!, scalar is DBNull ? null : scalar);
            return;
        }

        var rowsAffected = await singleCmd.ExecuteNonQueryAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(OutputVariable))
        {
            context.SetVariable(OutputVariable, (long)rowsAffected);
        }
    }

    private static SqlCommand BuildCommand(
        SqlConnection connection,
        string parameterizedQuery,
        IEnumerable<string> paramNames,
        HandlerExecutionContext context,
        Dictionary<string, object?>? row)
    {
        var cmd = new SqlCommand(parameterizedQuery, connection)
        {
            CommandTimeout = 30
        };

        foreach (var paramName in paramNames)
        {
            var hasVariable = TryResolveVariable(paramName, context, row, out var variableValue);
            if (!hasVariable)
            {
                throw new InvalidOperationException(
                    $"Missing variable '{{{{{paramName}}}}}' for SqlExecute query. " +
                    "Ensure BodyRead ran before SqlExecute and the request body contains this field.");
            }

            cmd.Parameters.AddWithValue($"@{paramName}", variableValue ?? DBNull.Value);
        }

        return cmd;
    }

    private static bool TryResolveVariable(
        string name,
        HandlerExecutionContext context,
        Dictionary<string, object?>? row,
        out object? value)
    {
        if (row is not null && row.TryGetValue(name, out value))
        {
            return true;
        }

        return context.Variables.TryGetValue(name, out value);
    }

    private static bool TryGetRowsFromInput(object? input, out List<Dictionary<string, object?>> rows)
    {
        rows = new List<Dictionary<string, object?>>();

        if (input is List<Dictionary<string, object?>> typedRows)
        {
            rows = typedRows;
            return rows.Count > 0;
        }

        if (input is IEnumerable<object?> items)
        {
            foreach (var item in items)
            {
                if (item is Dictionary<string, object?> row)
                {
                    rows.Add(row);
                }
            }

            return rows.Count > 0;
        }

        return false;
    }
}
