using System;
using System.Windows;
using System.Windows.Interop;

namespace ScreenBrightnessAdjuster;

/// <summary>
/// 隐藏的消息窗口：接收全局热键消息和显示器变化消息，并持有 HotkeyManager。
/// 进程存活期间它一直存在（隐藏状态），保证热键不因面板关闭而失效。
/// </summary>
public sealed class MessageWindow : Window
{
    public event Action<HotkeyAction>? HotkeyPressed;
    public event Action? DisplayChanged;

    public HotkeyManager? Hotkeys { get; private set; }

    /// <summary>创建 HWND，随后立即隐藏。</summary>
    public void Install()
    {
        ShowActivated = false;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.ToolWindow;
        Width = 0;
        Height = 0;

        Show();
        Hide();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        Hotkeys = new HotkeyManager(hwnd);
        Hotkeys.Triggered += action => HotkeyPressed?.Invoke(action);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_HOTKEY:
                Hotkeys?.RaiseTriggered(wParam.ToInt32());
                handled = true;
                break;
            case NativeMethods.WM_DISPLAYCHANGE:
                DisplayChanged?.Invoke();
                break;
        }
        return IntPtr.Zero;
    }
}
