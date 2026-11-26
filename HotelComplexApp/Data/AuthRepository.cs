using Npgsql;
using HotelComplexApp.Models;

namespace HotelComplexApp.Data;

public enum ChangePasswordResult { Ok, WrongOldPassword, UserNotFound }

public static class AuthRepository
{
    public static AppUser? Login(string login, string password)
    {
        using var conn = Db.Open();
        using var cmd = new NpgsqlCommand(
            @"SELECT id_user, password_hash, full_name, role::text, id_hotel
              FROM app_user
              WHERE login = @login AND is_active", conn);
        cmd.Parameters.AddWithValue("login", login);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, r.GetString(1))) return null;

        return new AppUser
        {
            Id = r.GetInt32(0),
            Login = login,
            FullName = r.GetString(2),
            Role = r.GetString(3),
            HotelId = r.IsDBNull(4) ? null : r.GetInt32(4)
        };
    }

    // Смена пароля: сначала проверяется старый пароль, потом сохраняется хеш нового
    public static ChangePasswordResult ChangePassword(int userId, string oldPassword, string newPassword)
    {
        using var conn = Db.Open();

        string? hash;
        using (var select = new NpgsqlCommand(
            "SELECT password_hash FROM app_user WHERE id_user = @id AND is_active", conn))
        {
            select.Parameters.AddWithValue("id", userId);
            hash = select.ExecuteScalar() as string;
        }

        if (hash == null) return ChangePasswordResult.UserNotFound;
        if (!BCrypt.Net.BCrypt.Verify(oldPassword, hash)) return ChangePasswordResult.WrongOldPassword;

        string newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        using var update = new NpgsqlCommand(
            "UPDATE app_user SET password_hash = @hash WHERE id_user = @id", conn);
        update.Parameters.AddWithValue("hash", newHash);
        update.Parameters.AddWithValue("id", userId);
        update.ExecuteNonQuery();

        return ChangePasswordResult.Ok;
    }
}
