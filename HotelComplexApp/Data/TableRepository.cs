using System.Data;
using HotelComplexApp.Models;
using Npgsql;
using NpgsqlTypes;

namespace HotelComplexApp.Data;


public static class TableRepository
{
 
    private static string Q(string ident) => "\"" + ident.Replace("\"", "\"\"") + "\"";

    
    private static string ReadExpr(ColumnDef c) => c.Type switch
    {
        ColType.Enum => $"t.{Q(c.Name)}::text",
        ColType.Date => $"t.{Q(c.Name)}::timestamp",
        _ => $"t.{Q(c.Name)}"
    };

  
    public static DataTable Load(TableDef def)
    {
        var parts = new List<string>();

        for (int i = 0; i < def.Pk.Length; i++)
            parts.Add($"t.{Q(def.Pk[i])} AS {Q("__pk" + i)}");

        foreach (var c in def.Columns)
            parts.Add($"{ReadExpr(c)} AS {Q("__c_" + c.Name)}");

        foreach (var c in def.Columns)
        {
            string shown = c.Type == ColType.Lookup
                ? $"(SELECT {c.LookupDisplay} FROM {Q(c.LookupTable!)} l " +
                  $"WHERE l.{Q(c.LookupKey!)} = t.{Q(c.Name)})"
                : ReadExpr(c);
            parts.Add($"{shown} AS {Q(c.Title)}");
        }

        string sql = $"SELECT {string.Join(", ", parts)} FROM {Q(def.Table)} t " +
                     $"ORDER BY t.{Q(def.Pk[0])}";

        using var conn = Db.Open();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var adapter = new NpgsqlDataAdapter(cmd);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }

    // Элементы выпадающего списка для столбца-внешнего ключа
    public static List<LookupItem> LoadLookup(ColumnDef c)
    {
        var list = new List<LookupItem>();
        string sql = $"SELECT l.{Q(c.LookupKey!)}, {c.LookupDisplay} " +
                     $"FROM {Q(c.LookupTable!)} l ORDER BY 2";

        using var conn = Db.Open();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new LookupItem(r.GetInt32(0), r.IsDBNull(1) ? "" : r.GetString(1)));
        return list;
    }

    public static void Insert(TableDef def, Dictionary<string, object?> values)
    {
        var cols = def.Columns;
        string names = string.Join(", ", cols.Select(c => Q(c.Name)));
        string vals = string.Join(", ", cols.Select((c, i) => Placeholder(c, i)));

        using var conn = Db.Open();
        using var cmd = new NpgsqlCommand(
            $"INSERT INTO {Q(def.Table)} ({names}) VALUES ({vals})", conn);
        for (int i = 0; i < cols.Count; i++)
            cmd.Parameters.Add(MakeParam($"p{i}", cols[i], values[cols[i].Name]));
        cmd.ExecuteNonQuery();
    }

    public static void Update(TableDef def, object?[] oldPk, Dictionary<string, object?> values)
    {
        var cols = def.Columns;
        string sets = string.Join(", ", cols.Select((c, i) => $"{Q(c.Name)} = {Placeholder(c, i)}"));
        string where = string.Join(" AND ", def.Pk.Select((k, i) => $"{Q(k)} = @k{i}"));

        using var conn = Db.Open();
        using var cmd = new NpgsqlCommand(
            $"UPDATE {Q(def.Table)} SET {sets} WHERE {where}", conn);
        for (int i = 0; i < cols.Count; i++)
            cmd.Parameters.Add(MakeParam($"p{i}", cols[i], values[cols[i].Name]));
        for (int i = 0; i < def.Pk.Length; i++)
            cmd.Parameters.Add(new NpgsqlParameter($"k{i}", oldPk[i] ?? DBNull.Value));
        cmd.ExecuteNonQuery();
    }

    public static void Delete(TableDef def, object?[] pk)
    {
        string where = string.Join(" AND ", def.Pk.Select((k, i) => $"{Q(k)} = @k{i}"));

        using var conn = Db.Open();
        using var cmd = new NpgsqlCommand($"DELETE FROM {Q(def.Table)} WHERE {where}", conn);
        for (int i = 0; i < def.Pk.Length; i++)
            cmd.Parameters.Add(new NpgsqlParameter($"k{i}", pk[i] ?? DBNull.Value));
        cmd.ExecuteNonQuery();
    }

    private static string Placeholder(ColumnDef c, int i)
        => c.Type == ColType.Enum ? $"@p{i}::{c.EnumType}" : $"@p{i}";

    private static NpgsqlParameter MakeParam(string name, ColumnDef c, object? value)
    {
        var p = new NpgsqlParameter { ParameterName = name };
        p.NpgsqlDbType = c.Type switch
        {
            ColType.Int or ColType.Lookup => NpgsqlDbType.Integer,
            ColType.Decimal => NpgsqlDbType.Numeric,
            ColType.Date => NpgsqlDbType.Date,
            _ => NpgsqlDbType.Text
        };
        p.Value = value ?? DBNull.Value;
        return p;
    }
}