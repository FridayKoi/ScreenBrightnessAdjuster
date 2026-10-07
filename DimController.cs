using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ScreenBrightnessAdjuster;

/// <summary>
/// 压暗总控制器：管理每台显示器的遮罩窗口和独立压暗比例，负责置顶保活与状态文本。
/// </summary>
public sealed class DimController : IDisposable
{
    /// <summary>最大压暗比例（%）。留 10% 余量，避免屏幕完全变黑找不到界面。</summary>
    public const int MaxPercent = 90;

    public sealed class MonitorSlot
    {
        public required string Device { get; init; }  // "\\.\DISPLAY1"，持久化键
        public string Label { get; set; } = "";        // "屏幕 1（主）"，随语言刷新
        public bool Primary { get; init; }
        public Rect Rect { get; init; }
        public int Percent { get; set; }
        public bool IsEnabled { get; set; } = true;   // 每屏独立开关
        public OverlayWindow Overlay { get; set; } = null!;
    }

    private readonly List<MonitorSlot> slots = new();
    private DispatcherTimer? topmostTimer;

    public bool Enabled { get; private set; }
    public int DefaultPercent { get; private set; }

    /// <summary>面板/托盘当前选中的显示器；null = 全部屏幕。</summary>
    public string? SelectedDevice { get; private set; }

    public IReadOnlyList<MonitorSlot> Slots => slots;

    /// <summary>任何影响 UI 的状态变化后触发。</summary>
    public event Action? StateChanged;

    public void Initialize(bool enabled, int defaultPercent,
        IReadOnlyDictionary<string, int> perMonitorPercent, IReadOnlyDictionary<string, bool>? perMonitorEnabled = null)
    {
        Enabled = enabled;
        DefaultPercent = Math.Clamp(defaultPercent, 0, MaxPercent);

        RebuildOverlays(perMonitorPercent, perMonitorEnabled);

        // 定期把遮罩压回最顶层并补写穿透样式，防止被其他置顶窗口盖住或样式被重置
        topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        topmostTimer.Tick += (_, _) =>
        {
            foreach (var slot in slots)
            {
                slot.Overlay.Reassert();
            }
        };
        topmostTimer.Start();
    }

    /// <summary>把当前每屏压暗比例导出为持久化字典。</summary>
    public Dictionary<string, int> CapturePerMonitor() =>
        slots.ToDictionary(s => s.Device, s => s.Percent);

    /// <summary>把当前每屏开关状态导出为持久化字典。</summary>
    public Dictionary<string, bool> CapturePerMonitorEnabled() =>
        slots.ToDictionary(s => s.Device, s => s.IsEnabled);

    /// <summary>按当前显示器布局重建遮罩（启动、分辨率或显示器列表变化时调用）。</summary>
    public void RebuildOverlays(IReadOnlyDictionary<string, int>? perMonitorPercent = null,
        IReadOnlyDictionary<string, bool>? perMonitorEnabled = null)
    {
        foreach (var slot in slots)
        {
            slot.Overlay.Close();
        }
        slots.Clear();

        var monitors = Monitors.GetMonitors();
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            int percent = perMonitorPercent != null && perMonitorPercent.TryGetValue(m.Device, out var p)
                ? Math.Clamp(p, 0, MaxPercent)
                : DefaultPercent;
            bool isEnabled = perMonitorEnabled == null
                || !perMonitorEnabled.TryGetValue(m.Device, out var en)
                || en;

            slots.Add(new MonitorSlot
            {
                Device = m.Device,
                Label = Strings.ScreenName(i + 1, m.Primary),
                Primary = m.Primary,
                Rect = m.Rect,
                Percent = percent,
                IsEnabled = isEnabled,
                Overlay = new OverlayWindow(m.Rect),
            });
        }

        foreach (var slot in slots)
        {
            slot.Overlay.Show(); // Show 触发 OnSourceInitialized，在那里完成穿透样式与定位
        }

        Apply();
    }

    private IEnumerable<MonitorSlot> Targets =>
        SelectedDevice == null ? slots : slots.Where(s => s.Device == SelectedDevice);

    /// <summary>当前选择对应的显示值（“全部屏幕”时取各屏平均）。</summary>
    public int GetDisplayPercent()
    {
        var targets = Targets.ToList();
        if (targets.Count == 0) return DefaultPercent;
        return (int)Math.Round(targets.Average(s => s.Percent));
    }

    public void SetSelectedDevice(string? device)
    {
        if (SelectedDevice == device) return;
        SelectedDevice = device;
        StateChanged?.Invoke();
    }

    /// <summary>语言切换后刷新各屏标签并触发 UI 同步。</summary>
    public void ApplyLanguage()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].Label = Strings.ScreenName(i + 1, slots[i].Primary);
        }
        StateChanged?.Invoke();
    }

    /// <summary>设置单个显示器的独立开关（device 为 null 的“全部屏幕”不适用）。</summary>
    public void SetScreenEnabled(string? device, bool enabled)
    {
        if (device == null) return;
        var slot = slots.FirstOrDefault(s => s.Device == device);
        if (slot == null || slot.IsEnabled == enabled) return;

        slot.IsEnabled = enabled;
        Apply();
        PersistAndNotify();
    }

    public bool GetScreenEnabled(string? device) =>
        device != null && slots.FirstOrDefault(s => s.Device == device)?.IsEnabled != false;

    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        Apply();
        PersistAndNotify();
    }

    public void Toggle() => SetEnabled(!Enabled);

    public void SetPercent(int percent)
    {
        percent = Math.Clamp(percent, 0, MaxPercent);
        bool changed = false;
        foreach (var slot in Targets)
        {
            if (slot.Percent != percent)
            {
                slot.Percent = percent;
                changed = true;
            }
        }
        if (!changed && Enabled) return;
        Enabled = true; // 在关闭状态下拖滑条 / 按热键 = 顺手打开
        Apply();
        PersistAndNotify();
    }

    public void Nudge(int delta)
    {
        bool changed = false;
        foreach (var slot in Targets)
        {
            int p = Math.Clamp(slot.Percent + delta, 0, MaxPercent);
            if (p != slot.Percent)
            {
                slot.Percent = p;
                changed = true;
            }
        }
        if (!changed) return;
        Enabled = true;
        Apply();
        PersistAndNotify();
    }

    /// <summary>托盘悬停提示的状态文本。</summary>
    public string BuildStatusText()
    {
        if (slots.Count == 0) return Strings.AppName;
        if (!Enabled) return $"{Strings.AppName} — {Strings.StatusOff}";

        if (slots.All(s => s.IsEnabled) && slots.All(s => s.Percent == slots[0].Percent))
        {
            return $"{Strings.AppName} — {Strings.StatusDimming(slots[0].Percent, slots.Count)}";
        }
        return $"{Strings.AppName} — " + string.Join(", ",
            slots.Select(s => s.IsEnabled ? $"{s.Label} {s.Percent}%" : $"{s.Label} {Strings.ScreenOff}"));
    }

    private void Apply(bool animate = true)
    {
        foreach (var slot in slots)
        {
            // 总开关 + 每屏独立开关，双重决定该屏是否遮罩
            slot.Overlay.ApplyDim(Enabled && slot.IsEnabled, slot.Percent / 100.0, animate);
        }
    }

    private void PersistAndNotify()
    {
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        topmostTimer?.Stop();
        foreach (var slot in slots)
        {
            slot.Overlay.Close();
        }
        slots.Clear();
    }
}
