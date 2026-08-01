using System.Text.Json.Serialization;

namespace DesktopOrganizer.Services;

public class AppSettings
{
    public bool RoundedIconsEnabled { get; set; } = true;
}

[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 全局设置（目前只有"圆角图标"开关）。用静态属性方便 XAML 转换器直接读取，
/// 改设置后调用 NotifyChanged() 让界面重新渲染一次。
/// </summary>
public static class AppSettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopOrganizer", "settings.json");

    public static bool RoundedIconsEnabled { get; private set; } = true;

    public static event Action? Changed;

    static AppSettingsService()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = System.Text.Json.JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
                if (settings != null) RoundedIconsEnabled = settings.RoundedIconsEnabled;
            }
        }
        catch { /* 读取失败就用默认值 */ }
    }

    public static void SetRoundedIconsEnabled(bool enabled)
    {
        RoundedIconsEnabled = enabled;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = System.Text.Json.JsonSerializer.Serialize(
                new AppSettings { RoundedIconsEnabled = enabled }, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(FilePath, json);
        }
        catch { /* 保存失败不影响本次使用 */ }

        Changed?.Invoke();
    }
}
