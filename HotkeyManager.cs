using System;
using System.Collections.Generic;

namespace ScreenBrightnessAdjuster;

/// <summary>
/// 全局热键管理：负责向系统注册 / 注销热键，处理“键位被占用”的降级逻辑。
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr hwnd;
    private readonly Dictionary<HotkeyAction, HotkeySpec> registered = new();

    /// <summary>任一热键被按下（由消息窗口转发 WM_HOTKEY）。</summary>
    public event Action<HotkeyAction>? Triggered;

    /// <summary>每个动作当前实际生效的键位显示文本（用于面板展示）。</summary>
    public IReadOnlyDictionary<HotkeyAction, string> EffectiveDisplay { get; private set; } =
        new Dictionary<HotkeyAction, string>();

    public HotkeyManager(IntPtr hwnd)
    {
        this.hwnd = hwnd;
    }

    /// <summary>
    /// 按请求重新注册全部热键，返回每个动作实际生效的键位文本（失败的动作不在字典里）。
    /// requested 中值为 null 表示使用默认降级链。
    /// </summary>
    public Dictionary<HotkeyAction, string> Rebind(IReadOnlyDictionary<HotkeyAction, HotkeySpec?> requested)
    {
        UnregisterAll();

        var display = new Dictionary<HotkeyAction, string>();
        foreach (HotkeyAction action in Enum.GetValues<HotkeyAction>())
        {
            var chain = new List<HotkeySpec>();
            if (requested.TryGetValue(action, out var custom) && custom != null)
            {
                chain.Add(custom);
            }
            chain.AddRange(Hotkeys.Defaults[action]);

            HotkeySpec? bound = null;
            foreach (var spec in chain)
            {
                if (NativeMethods.RegisterHotKey(hwnd, (int)action, spec.Modifiers, spec.Key))
                {
                    bound = spec;
                    break;
                }
            }

            if (bound != null)
            {
                registered[action] = bound;
                display[action] = Hotkeys.Format(bound);
            }
            else
            {
                display[action] = Strings.NotRegistered;
            }
        }

        EffectiveDisplay = display;
        return display;
    }

    /// <summary>语言切换后刷新键位显示文本（不重新注册热键）。</summary>
    public void RefreshDisplay()
    {
        var display = new Dictionary<HotkeyAction, string>();
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            display[action] = registered.TryGetValue(action, out var spec)
                ? Hotkeys.Format(spec)
                : Strings.NotRegistered;
        }
        EffectiveDisplay = display;
    }

    internal void RaiseTriggered(int id)
    {
        if (Enum.IsDefined(typeof(HotkeyAction), id))
        {
            Triggered?.Invoke((HotkeyAction)id);
        }
    }

    public void Dispose()
    {
        UnregisterAll();
    }

    private void UnregisterAll()
    {
        foreach (var id in registered.Keys)
        {
            NativeMethods.UnregisterHotKey(hwnd, (int)id);
        }
        registered.Clear();
    }
}
