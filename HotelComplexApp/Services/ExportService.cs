using System.Data;
using System.Globalization;
using System.Text;

namespace HotelComplexApp.Services;

public static class ExportService
{
    private static readonly CultureInfo Ru = new("ru-RU");

    public static void ToCsv(DataTable table, string path)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));

        writer.WriteLine(string.Join(";",
            table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName))));

        foreach (DataRow row in table.Rows)
            writer.WriteLine(string.Join(";", row.ItemArray.Select(v => Escape(Format(v)))));
    }

    private static string Format(object? value) => value switch
    {
        null => "",
        DBNull => "",
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd.MM.yyyy") : dt.ToString("dd.MM.yyyy HH:mm"),
        DateOnly d => d.ToString("dd.MM.yyyy"),
        decimal m => m.ToString(Ru),
        double x => x.ToString(Ru),
        float f => f.ToString(Ru),
        bool b => b ? "Да" : "Нет",
        _ => Convert.ToString(value, Ru) ?? ""
    };

    private static string Escape(string s)
    {
        if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
