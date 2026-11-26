using Npgsql;
using NpgsqlTypes;

namespace HotelComplexApp.Data;

public static class AuditLogger
{
    public static void Log(int? userId, string action, string? details = null)
    {
        try
        {
            using var conn = Db.Open();
            using var cmd = new NpgsqlCommand(
                "INSERT INTO audit_log (id_user, action, details) VALUES (@u, @a, @d)", conn);
            cmd.Parameters.Add(new NpgsqlParameter("u", NpgsqlDbType.Integer)
            { Value = (object?)userId ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("a", NpgsqlDbType.Varchar)
            { Value = action });
            cmd.Parameters.Add(new NpgsqlParameter("d", NpgsqlDbType.Text)
            { Value = (object?)details ?? DBNull.Value });
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Сбой журнала не должен ронять приложение
        }
    }
}