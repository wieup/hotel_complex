using System.Text.RegularExpressions;

namespace HotelComplexApp.Services;

// Проверка текста запроса, введённого вручную в разделе «Документы».
public static class SqlGuard
{
    private static readonly Regex StringLiterals = new("'(?:[^']|'')*'", RegexOptions.Compiled);
    private static readonly Regex QuotedIdentifiers = new("\"(?:[^\"]|\"\")*\"", RegexOptions.Compiled);
    private static readonly Regex BlockComments = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex LineComments = new(@"--[^\r\n]*", RegexOptions.Compiled);

    // не должны бфть в запросе 
    private static readonly Regex Forbidden = new(
        @"\b(insert|update|delete|drop|alter|create|truncate|grant|revoke|copy|call|execute|do|" +
        @"vacuum|reindex|cluster|lock|listen|notify|reset|set|begin|commit|rollback|savepoint|" +
        @"prepare|deallocate|refresh|merge|into)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "Введите запрос.";

        
        string clean = StringLiterals.Replace(sql, "''");
        clean = QuotedIdentifiers.Replace(clean, "\"\"");
        clean = BlockComments.Replace(clean, " ");
        clean = LineComments.Replace(clean, " ");
        clean = clean.Trim().TrimEnd(';').Trim();

        if (clean.Contains(';'))
            return "Разрешён только один запрос: уберите «;» внутри текста.";

        if (!(clean.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
              clean.StartsWith("with", StringComparison.OrdinalIgnoreCase)))
            return "Разрешены только запросы на чтение: они должны начинаться со слова SELECT (или WITH).";

        var match = Forbidden.Match(clean);
        if (match.Success)
            return $"В запросе есть запрещённое слово «{match.Value.ToUpper()}».\n" +
                   "В этом окне разрешено только чтение данных (SELECT).";

        return null;
    }
}
