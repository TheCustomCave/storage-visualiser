using System.Text.RegularExpressions;

namespace StorageVisualiser.Core.Export;

public static partial class ExportSecurityHelper
{
    private static readonly char[] InjectionPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    public static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "\"\"";
        }

        // CSV Injection mitigation (formula injection: =, +, -, @, tab, cr)
        // Prefix with single quote if it begins with dangerous characters
        var safeField = field;
        if (field.Length > 0 && Array.IndexOf(InjectionPrefixes, field[0]) >= 0)
        {
            safeField = "'" + field;
        }

        // Escape double quotes by doubling them
        safeField = safeField.Replace("\"", "\"\"", StringComparison.Ordinal);

        return $"\"{safeField}\"";
    }

    public static string RedactPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        // Redact user profile folder names: e.g. C:\Users\john.doe\Documents -> C:\Users\<user>\Documents
        return UserProfileRegex().Replace(path, @"$1<user>$2");
    }

    [GeneratedRegex(@"(?i)([\\/](?:users|home)[\\/])[^\\/]+(\b|[\\/])", RegexOptions.Compiled)]
    private static partial Regex UserProfileRegex();
}
