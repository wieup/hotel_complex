using System.Data;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace HotelComplexApp.Data;

public static class DocumentRepository
{
    public const int MaxRows = 5000;
    public static DataTable Run(string sql, DateTime from, DateTime to, int n, string text)
    {
        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();

        using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", conn, tx))
            readOnly.ExecuteNonQuery();
        using (var timeout = new NpgsqlCommand("SET LOCAL statement_timeout = 15000", conn, tx))
            timeout.ExecuteNonQuery();

        using var cmd = new NpgsqlCommand(sql, conn, tx);

        if (Uses(sql, "from"))
            cmd.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.Date)
            { Value = DateOnly.FromDateTime(from) });
        if (Uses(sql, "to"))
            cmd.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.Date)
            { Value = DateOnly.FromDateTime(to) });
        if (Uses(sql, "n"))
            cmd.Parameters.Add(new NpgsqlParameter("n", NpgsqlDbType.Integer)
            { Value = n });
        if (Uses(sql, "s"))
            cmd.Parameters.Add(new NpgsqlParameter("s", NpgsqlDbType.Text)
            { Value = text });

        using var adapter = new NpgsqlDataAdapter(cmd);
        var table = new DataTable("result");
        adapter.Fill(0, MaxRows, table);

        tx.Rollback();
        return table;
    }

    private static bool Uses(string sql, string name)
        => Regex.IsMatch(sql, "@" + name + @"\b", RegexOptions.IgnoreCase);
}