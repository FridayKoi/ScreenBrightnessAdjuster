using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ScreenBrightnessAdjuster;

/// <summary>
/// 设置持久化：%APPDATA%\ScreenBrightnessAdjuster\settings.json
/// 包含全局开关、每屏压暗比例和自定义热键。
/// </summary>
public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public int DefaultPercent { get; set; } = 30;
    public string Language { get; set; } = ""; // 空 = 跟随系统；"zh" / "en"
    public Dictionary<string, int> PerMonitorPercent { get; set; } = new();
    public Dictionary<string, bool> PerMonitorEnabled { get; set; } = new();
    public Dictionary<string, HotkeySpec> Hotkeys { get; set; } = new(); // 键为 HotkeyAction 枚举名

    /// <summary>首次启动（设置文件不存在）时为 true，用于弹出面板引导新用户。</summary>
    public bool FirstRun { get; private set; }

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenBrightnessAdjuster");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        // 设置文件不存在 = 首次启动
        if (!File.Exists(FilePath))
        {
            return new AppSettings { FirstRun = true };
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
            if (settings != null)
            {
                settings.DefaultPercent = Math.Clamp(settings.DefaultPercent, 0, DimController.MaxPercent);

                var clean = new Dictionary<string, int>();
                foreach (var kv in settings.PerMonitorPercent)
                {
                    clean[kv.Key] = Math.Clamp(kv.Value, 0, DimController.MaxPercent);
                }
                settings.PerMonitorPercent = clean;
                return settings;
            }
        }
        catch
        {
            // 文件损坏时使用默认值即可
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 保存失败不影响运行（比如磁盘只读）
        }
    }
}
