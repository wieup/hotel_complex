using Npgsql;

namespace HotelComplexApp.Data;

public static class Db
{
    public const string ConnectionString =
        "Host=localhost;Port=5432;Database=hotel_complex(final);Username=postgres;Password=sardaana";

    public static NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }
}