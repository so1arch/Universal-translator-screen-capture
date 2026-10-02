using System.Text.Json;

namespace ScreenTranslator;

sealed class Settings
{
    public int Fps { get; set; } = 6;                // сколько раз в секунду проверять экран
    public int Scale { get; set; } = 0;              // 0 = 1x, 1 = 1.5x, 2 = 2x (качество OCR)
    public bool OnlyForeground { get; set; } = true; // переводить, только когда окно активно
    public bool AutoInvert { get; set; } = true;     // авто-инверсия тёмных тем для OCR
    public string LastProc { get; set; } = "";

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTranslator");

    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
        catch { return new Settings(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}
