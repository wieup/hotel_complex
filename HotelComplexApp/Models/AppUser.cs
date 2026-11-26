namespace HotelComplexApp.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public int? HotelId { get; set; }
}

public static class Session
{
    public static AppUser? Current { get; set; }
}