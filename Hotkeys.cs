using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace ScreenBrightnessAdjuster;

public enum HotkeyAction
{
    Brighter = 1, // 调亮
    Darker = 2,   // 调暗
    Toggle = 3,   // 开 / 关
}

/// <summary>一个热键组合：修饰键（Win32 MOD_* 位标志）+ 虚拟键码。</summary>
public sealed class HotkeySpec
{
    public uint Modifiers { get; set; }
    public uint Key { get; set; }

    public HotkeySpec() { }

    public HotkeySpec(uint modifiers, uint key)
    {
        Modifiers = modifiers;
        Key = key;
    }
}

public static class Hotkeys
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public static uint ToWin32(ModifierKeys modifiers)
    {
        uint m = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) m |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Alt)) m |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Shift)) m |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) m |= MOD_WIN;
        return m;
    }

    /// <summary>
    /// 默认键位与降级链：主键位（Ctrl+Alt+…）被核显驱动旋转快捷键等占用时，
    /// 依次尝试后备键位（Ctrl+Shift+…）。
    /// </summary>
    public static IReadOnlyDictionary<HotkeyAction, HotkeySpec[]> Defaults { get; } =
        new Dictionary<HotkeyAction, HotkeySpec[]>
        {
            [HotkeyAction.Brighter] = new[]
            {
                new HotkeySpec(MOD_CONTROL | MOD_ALT, 0x26),   // Ctrl+Alt+↑
                new HotkeySpec(MOD_CONTROL | MOD_SHIFT, 0x26), // Ctrl+Shift+↑
            },
            [HotkeyAction.Darker] = new[]
            {
                new HotkeySpec(MOD_CONTROL | MOD_ALT, 0x28),   // Ctrl+Alt+↓
                new HotkeySpec(MOD_CONTROL | MOD_SHIFT, 0x28), // Ctrl+Shift+↓
            },
            [HotkeyAction.Toggle] = new[]
            {
                new HotkeySpec(MOD_CONTROL | MOD_ALT, 0x20),   // Ctrl+Alt+空格
                new HotkeySpec(MOD_CONTROL | MOD_SHIFT, 0x20), // Ctrl+Shift+空格
            },
        };

    public static string Format(HotkeySpec spec)
    {
        string s = "";
        if ((spec.Modifiers & MOD_CONTROL) != 0) s += "Ctrl+";
        if ((spec.Modifiers & MOD_ALT) != 0) s += "Alt+";
        if ((spec.Modifiers & MOD_SHIFT) != 0) s += "Shift+";
        if ((spec.Modifiers & MOD_WIN) != 0) s += "Win+";
        return s + Strings.KeyName(spec.Key);
    }
}
