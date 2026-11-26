using System.Text.Json;

namespace HotelComplexApp.Services;

public record SettingsData(string FontFamily, float FontSize, string Theme);
public static class AppSettings
{
    public static readonly string[] FontFamilies =
        { "Segoe UI", "Arial", "Calibri", "Tahoma", "Verdana", "Times New Roman" };

    public static readonly string[] Themes = { "Светлая", "Серая", "Голубая", "Бежевая" };

    public const float MinFontSize = 8f;
    public const float MaxFontSize = 14f;

    public static string FontFamily { get; set; } = "Segoe UI";
    public static float FontSize { get; set; } = 9f;
    public static string Theme { get; set; } = "Светлая";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HotelComplexApp", "settings.json");

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;

            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath));
            if (data == null) return;

            if (FontFamilies.Contains(data.FontFamily)) FontFamily = data.FontFamily;
            if (data.FontSize >= MinFontSize && data.FontSize <= MaxFontSize) FontSize = data.FontSize;
            if (Themes.Contains(data.Theme)) Theme = data.Theme;
        }
        catch
        {
            
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(new SettingsData(FontFamily, FontSize, Theme)));
        }
        catch
        {
           
        }
    }

    public static void Reset()
    {
        FontFamily = "Segoe UI";
        FontSize = 9f;
        Theme = "Светлая";
    }

    public static Font CreateFont() => new(FontFamily, FontSize);

 
    public static void Apply(Form form) => form.Font = CreateFont();

    public static Color BackColorFor(string theme) => theme switch
    {
        "Серая" => Color.FromArgb(235, 235, 235),
        "Голубая" => Color.FromArgb(225, 238, 250),
        "Бежевая" => Color.FromArgb(250, 243, 230),
        _ => SystemColors.Control
    };

    public static Color CurrentBackColor() => BackColorFor(Theme);
}
