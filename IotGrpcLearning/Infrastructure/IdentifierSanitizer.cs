using System.Text.RegularExpressions;

namespace IotGrpcLearning.Infrastructure;

/// <summary>
/// Validates SQL identifiers (table/column names) to prevent SQL injection.
/// </summary>
public static partial class IdentifierSanitizer
{
    // SQLite identifiers: alphanumeric + underscore, must start with letter or underscore
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled)]
    private static partial Regex IdentifierPattern();

    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TABLE",
        "FROM", "WHERE", "JOIN", "UNION", "ORDER", "GROUP", "HAVING", "LIMIT"
    };

    /// <summary>
    /// Validates that a string is a safe SQL identifier.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if identifier is invalid.</exception>
    public static string ValidateIdentifier(string identifier, string parameterName = "identifier")
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier cannot be null or whitespace.", parameterName);

        if (identifier.Length > 128)
            throw new ArgumentException("Identifier is too long (max 128 characters).", parameterName);

        if (!IdentifierPattern().IsMatch(identifier))
            throw new ArgumentException(
                $"Identifier '{identifier}' contains invalid characters. Only alphanumeric and underscore allowed.",
                parameterName);

        if (ReservedKeywords.Contains(identifier))
            throw new ArgumentException($"Identifier '{identifier}' is a reserved SQL keyword.", parameterName);

        return identifier;
    }

    /// <summary>
    /// Safely quotes an identifier for use in SQL.
    /// </summary>
    public static string QuoteIdentifier(string identifier)
    {
        ValidateIdentifier(identifier);
        return $"\"{identifier}\"";
    }
}