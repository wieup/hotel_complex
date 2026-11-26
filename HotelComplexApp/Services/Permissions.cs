namespace HotelComplexApp.Services;

public static class Permissions
{
    public const string DirectorComplex = "director_complex";
    public const string DirectorHotel = "director_hotel";
    public const string Commandant = "commandant";
    public const string BookingManager = "booking_manager";
    public const string Administrator = "administrator";

    public static string RoleTitle(string role) => role switch
    {
        DirectorComplex => "Директор комплекса",
        DirectorHotel => "Директор гостиницы",
        Commandant => "Комендант гостиницы",
        BookingManager => "Менеджер по бронированию",
        Administrator => "Администратор",
        _ => role
    };

    private static readonly string[] All =
        { DirectorComplex, DirectorHotel, Commandant, BookingManager, Administrator };

    //  Разделы меню
    private static readonly Dictionary<string, string[]> MenuAccess = new()
    {
        ["docs"]          = new[] { DirectorComplex, DirectorHotel, BookingManager, Administrator },
        ["analytics"]     = new[] { DirectorComplex, DirectorHotel },
        ["help.manual"]   = All,
        ["help.about"]    = All,
        ["misc.settings"] = All,
        ["misc.password"] = All,
        ["misc.switch"]   = All,
    };

    public static bool CanSee(string menuKey, string role)
        => MenuAccess.TryGetValue(menuKey, out var roles) && roles.Contains(role);

    //  Права на таблицы 
    private static Dictionary<string, string> A(
        string dc = "", string dh = "", string cm = "", string bm = "", string ad = "")
    {
        var d = new Dictionary<string, string>();
        if (dc != "") d[DirectorComplex] = dc;
        if (dh != "") d[DirectorHotel] = dh;
        if (cm != "") d[Commandant] = cm;
        if (bm != "") d[BookingManager] = bm;
        if (ad != "") d[Administrator] = ad;
        return d;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> TableAccess = new()
    {
        // Гостиничный фонд: комендант вводит и меняет характеристики корпусов
        ["hotel"]          = A(dc: "R", dh: "R", cm: "CRU"),
        ["room"]           = A(dc: "R", dh: "R", cm: "CRU"),
        ["hotel_services"] = A(dc: "R", dh: "R", cm: "CRUD"),

        // Клиенты: регистрируют менеджер и администратор
        ["client"]         = A(dc: "R", dh: "R", bm: "CRU", ad: "CRU"),
        ["natural_person"] = A(dc: "R", dh: "R", bm: "CRU", ad: "CRU"),
        ["legal_entity"]   = A(dc: "R", dh: "R", bm: "CRU"),
        ["bank"]           = A(dc: "CRUD", dh: "R", bm: "R"),

        // Бронирование
        ["reservation"]          = A(dc: "R", dh: "R", bm: "CRUD", ad: "CRU"),
        ["reservation_room"]     = A(dc: "R", dh: "R", bm: "CRUD", ad: "CRUD"),
        ["reservation_guest"]    = A(dc: "R", dh: "R", bm: "CRUD", ad: "CRUD"),
        ["contract"]             = A(dc: "R", dh: "R", bm: "CRUD"),
        ["contract_reservation"] = A(dc: "R", dh: "R", bm: "CRUD"),

        // Услуги и жалобы
        ["service"]         = A(dc: "CRUD", dh: "R", ad: "R"),
        ["client_services"] = A(dc: "R", dh: "R", bm: "R", ad: "CRUD"),
        ["complaint"]       = A(dc: "R", dh: "R", ad: "CRU"),

        // Кадры
        ["staff"]               = A(dc: "CRUD", dh: "CRUD"),
        ["employment_record"]   = A(dc: "CRUD", dh: "CRUD"),
        ["position"]            = A(dc: "CRUD", dh: "R"),
        ["profession"]          = A(dc: "CRUD", dh: "R"),
        ["qualification"]       = A(dc: "CRUD", dh: "R"),
        ["specialization"]      = A(dc: "CRUD", dh: "R"),
        ["structural_division"] = A(dc: "CRUD", dh: "R"),
        ["work_place"]          = A(dc: "CRUD", dh: "R"),

        // Адреса
        ["city"]   = A(dc: "CRUD", dh: "R"),
        ["street"] = A(dc: "CRUD", dh: "R"),
    };

    public static string TableOps(string table, string role)
        => TableAccess.TryGetValue(table, out var byRole) && byRole.TryGetValue(role, out var ops)
            ? ops
            : "";
}
