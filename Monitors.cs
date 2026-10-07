using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenBrightnessAdjuster;

public sealed class MonitorInfo
{
    public required string Device { get; init; }
    public bool Primary { get; init; }
    public Rect Rect { get; init; }
}

/// <summary>
/// 显示器枚举：返回设备名、是否主屏和物理像素矩形（按屏幕位置从左到右排序）。
/// </summary>
internal static class Monitors
{
    public static List<MonitorInfo> GetMonitors()
    {
        var result = new List<MonitorInfo>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                var info = new NativeMethods.MONITORINFOEX();
                info.cbSize = Marshal.SizeOf(info);

                if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                {
                    result.Add(new MonitorInfo
                    {
                        Device = info.szDevice,
                        Primary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                        Rect = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
                    });
                }
                return true;
            },
            IntPtr.Zero);

        // 按虚拟桌面坐标排序，保证“屏幕 1 / 屏幕 2”的顺序和摆放位置一致
        result.Sort((a, b) => (a.Rect.X, a.Rect.Y).CompareTo((b.Rect.X, b.Rect.Y)));
        return result;
    }
}
