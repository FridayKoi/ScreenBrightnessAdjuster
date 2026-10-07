using System;
using System.Globalization;
using System.Windows.Input;

namespace ScreenBrightnessAdjuster;

/// <summary>
/// 轻量中英双语字符串表。
/// 语言来源：设置项（"zh" / "en"）；设置为空时跟随系统 UI 语言。
/// 切换语言后调用各处的 ApplyLanguage / Sync 即可立即生效。
/// </summary>
public static class Strings
{
    public static string Language { get; private set; } = "zh";

    /// <summary>应用语言；setting 为空表示跟随系统。</summary>
    public static void SetLanguage(string? setting)
    {
        var lang = string.IsNullOrWhiteSpace(setting)
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : setting;
        Language = lang.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
    }

    public static bool IsEnglish => Language == "en";

    private static string T(string zh, string en) => IsEnglish ? en : zh;

    // ---- 通用 ----
    public static string AppName => T("屏幕调暗器", "Screen Dimmer");
    public static string NotRegistered => T("未注册", "Not registered");
    public static string FollowSystem => T("跟随系统", "Follow system");
    public static string LanguageLabel => T("语言：", "Language:");

    // ---- 面板 ----
    public static string PanelSubtitle => T("在屏幕上叠一层黑色遮罩，突破显示器最低亮度",
        "Overlay a black layer on the screen to go below the monitor's minimum brightness");
    public static string ScreenLabel => T("屏幕：", "Display:");
    public static string AllScreens => T("全部屏幕", "All displays");
    public static string EnableThisScreen => T("启用此屏", "Enable this display");
    public static string EnableAll => T("启用压暗（对所有屏幕生效）", "Enable dimming (applies to all displays)");
    public static string HotkeyHint => T("全局热键（点击输入框后按下新组合，Esc 恢复默认）",
        "Global hotkeys (click a box and press a new combo; Esc restores default)");
    public static string Brighter => T("调亮", "Brighter");
    public static string Darker => T("调暗", "Darker");
    public static string Toggle => T("开 / 关", "On / Off");
    public static string CloseHint => T("点 × 关闭窗口会最小化到托盘，程序继续生效",
        "Closing with × hides to tray; dimming keeps running");

    public static string ScreenName(int index, bool primary) => primary
        ? T($"屏幕 {index}（主）", $"Display {index} (Primary)")
        : T($"屏幕 {index}", $"Display {index}");

    // ---- 托盘菜单 ----
    public static string MenuOpenPanel => T("调节面板", "Open Panel");
    public static string MenuEnableDim => T("启用压暗", "Enable dimming");
    public static string MenuSelectScreen => T("选择屏幕", "Display");
    public static string MenuExit => T("退出", "Exit");

    // ---- 托盘状态 ----
    public static string StatusOff => T("已关闭", "Off");
    public static string StatusDimming(int percent, int count) =>
        T($"已压暗 {percent}%（{count} 台显示器）", $"Dimming to {percent}% ({count} display(s))");
    public static string ScreenOff => T("关", "Off");

    // ---- 气泡通知 ----
    public static string BalloonFirstRunTitle => T("已在后台运行", "Running in your tray");
    public static string BalloonFirstRunText => T("屏幕调暗器已启动，随时点托盘图标打开设置。",
        "Screen Dimmer is running. Click the tray icon to open settings anytime.");
    public static string BalloonHotkeyTakenTitle => T("热键被占用", "Hotkey in use");
    public static string BalloonHotkeyTaken(string combo) => T(
        $"{combo} 无法注册（已被其他程序占用），已恢复默认热键。",
        $"Failed to register {combo} (already in use). Default hotkeys restored.");
    public static string BalloonRegisterFailedTitle => T("热键注册失败", "Hotkey registration failed");
    public static string BalloonRegisterFailed(string action) => T(
        $"“{action}”的热键被其他程序占用，请打开面板重新设置。",
        $"The hotkey for \"{action}\" is taken by another app. Open the panel to change it.");

    // ---- 虚拟键名 ----
    public static string KeyName(uint vk) => vk switch
    {
        0x20 => T("空格", "Space"),
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        0x21 => "PgUp",
        0x22 => "PgDn",
        0x2D => "Ins",
        0x2E => "Del",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
        _ => ((Key)(int)vk).ToString(),
    };
}
