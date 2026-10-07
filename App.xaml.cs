using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace ScreenBrightnessAdjuster;

public partial class App : Application
{
    internal static bool IsShuttingDown;

    private const string SingleInstanceMutexName = @"Local\ScreenBrightnessAdjuster.SingleInstance";
    private const string ShowPanelEventName = @"Local\ScreenBrightnessAdjuster.ShowPanel";

    private Mutex? singleInstanceMutex;
    private EventWaitHandle? showPanelRequest;
    private DispatcherTimer? saveTimer;
    private AppSettings settings = null!;
    private DimController controller = null!;
    private MessageWindow messageWindow = null!;
    private TaskbarIcon tray = null!;
    private ControlPanelWindow? panel;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例：避免开两个进程叠两层遮罩；重复启动时唤起已有实例的面板
        singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            // 通知第一个实例打开面板后立即退出。
            // 注意：此刻 WPF 的 Run 消息循环尚未启动，调用 Shutdown() 会让进程挂起成僵尸，必须直接退出进程。
            TrySignalRunningInstance();
            Environment.Exit(0);
        }

        base.OnStartup(e);

        settings = AppSettings.Load();
        Strings.SetLanguage(settings.Language);

        controller = new DimController();
        controller.StateChanged += Controller_StateChanged;
        controller.Initialize(settings.Enabled, settings.DefaultPercent,
            settings.PerMonitorPercent, settings.PerMonitorEnabled);

        // 隐藏消息窗口：承接全局热键与显示器变化
        messageWindow = new MessageWindow();
        messageWindow.HotkeyPressed += MessageWindow_HotkeyPressed;
        messageWindow.DisplayChanged += OnDisplayChanged;
        messageWindow.Install();

        // 托盘图标
        tray = (TaskbarIcon)FindResource("TrayIcon");
        tray.Visibility = Visibility.Visible;

        // 按设置注册热键（含默认键位降级链）
        RebindHotkeys(showBalloon: true);

        // 监听“唤起面板”请求（来自第二个实例）
        showPanelRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ShowPanelEventName);
        var listener = new Thread(WaitForShowPanelRequests) { IsBackground = true };
        listener.Start();

        SyncTrayMenu();

        // 首次启动：弹出面板 + 气泡提示，避免新用户以为程序没运行
        if (settings.FirstRun)
        {
            ShowPanel();
            tray.ShowBalloonTip(Strings.BalloonFirstRunTitle,
                Strings.BalloonFirstRunText, BalloonIcon.Info);
        }
    }

    private void TrySignalRunningInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowPanelEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
        }
        catch
        {
            // 通知失败就静默退出，不影响已运行的实例
        }
    }

    private void WaitForShowPanelRequests()
    {
        while (showPanelRequest != null && showPanelRequest.WaitOne())
        {
            Dispatcher.BeginInvoke(ShowPanel);
        }
    }

    private void MessageWindow_HotkeyPressed(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.Brighter:
                // 步进 1%，按住热键借助键盘自动重复实现连续调光
                controller.Nudge(-1);
                break;
            case HotkeyAction.Darker:
                controller.Nudge(+1);
                break;
            case HotkeyAction.Toggle:
                controller.SetEnabled(!controller.Enabled);
                break;
        }
    }

    private void OnDisplayChanged()
    {
        // 显示器插拔 / 分辨率变化：保留每屏设置，重建遮罩
        controller.RebuildOverlays(controller.CapturePerMonitor(), controller.CapturePerMonitorEnabled());
        SyncTrayMenu();
        panel?.SyncFromController();
    }

    private void Controller_StateChanged()
    {
        settings.Enabled = controller.Enabled;
        settings.PerMonitorPercent = controller.CapturePerMonitor();
        settings.PerMonitorEnabled = controller.CapturePerMonitorEnabled();
        ScheduleDelayedSave();
        SyncTrayMenu();
        panel?.SyncFromController();
    }

    /// <summary>滑条/热键连续调节时避免频繁写盘：停顿 300ms 后才真正保存。</summary>
    private void ScheduleDelayedSave()
    {
        if (saveTimer == null)
        {
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            saveTimer.Tick += (_, _) =>
            {
                saveTimer.Stop();
                settings.Save();
            };
        }
        saveTimer.Stop();
        saveTimer.Start();
    }

    /// <summary>退出前把尚未落盘的改动立即写入。</summary>
    private void FlushSettings()
    {
        saveTimer?.Stop();
        settings.Save();
    }

    // ---- 热键 ----

    private Dictionary<HotkeyAction, HotkeySpec?> RequestedHotkeys() =>
        Enum.GetValues<HotkeyAction>().ToDictionary(
            action => action,
            action => settings.Hotkeys.TryGetValue(action.ToString(), out var spec) ? spec : null);

    private void RebindHotkeys(bool showBalloon)
    {
        var effective = messageWindow.Hotkeys!.Rebind(RequestedHotkeys());

        if (!showBalloon) return;
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            if (!effective.ContainsKey(action))
            {
                tray.ShowBalloonTip(Strings.BalloonRegisterFailedTitle,
                    Strings.BalloonRegisterFailed(HotkeyName(action)),
                    BalloonIcon.Warning);
            }
        }
    }

    /// <summary>面板热键输入框提交后调用：null 表示清除自定义、恢复默认链。</summary>
    private bool ApplyHotkeyFromPanel(HotkeyAction action, HotkeySpec? spec)
    {
        if (spec != null)
        {
            settings.Hotkeys[action.ToString()] = spec;
        }
        else
        {
            settings.Hotkeys.Remove(action.ToString());
        }

        var effective = messageWindow.Hotkeys!.Rebind(RequestedHotkeys());

        if (spec != null && !effective.ContainsKey(action))
        {
            // 新热键被占用：撤销自定义，回退默认键位链
            settings.Hotkeys.Remove(action.ToString());
            messageWindow.Hotkeys.Rebind(RequestedHotkeys());
            tray.ShowBalloonTip(Strings.BalloonHotkeyTakenTitle,
                Strings.BalloonHotkeyTaken(Hotkeys.Format(spec)),
                BalloonIcon.Warning);
        }

        settings.Save();
        return effective.ContainsKey(action);
    }

    private IReadOnlyDictionary<HotkeyAction, string> GetHotkeyText() =>
        messageWindow.Hotkeys!.EffectiveDisplay;

    /// <summary>面板语言下拉框提交后调用：切换语言并让所有 UI 立即刷新。</summary>
    private void ApplyLanguageFromPanel(string language)
    {
        settings.Language = language;
        Strings.SetLanguage(language);
        FlushSettings();

        messageWindow.Hotkeys!.RefreshDisplay(); // 键位显示文本换语言
        controller.ApplyLanguage();              // 触发 StateChanged → 托盘菜单/提示 + 面板动态部分
        panel?.ApplyLanguage();                  // 面板静态文案 + 语言下拉自身
    }

    private static string HotkeyName(HotkeyAction action) => action switch
    {
        HotkeyAction.Brighter => Strings.Brighter,
        HotkeyAction.Darker => Strings.Darker,
        _ => Strings.Toggle,
    };

    // ---- 托盘 ----

    private void SyncTrayMenu()
    {
        if (tray is null || tray.ContextMenu is null) return;

        // 注意：App.xaml 资源字典里的元素无法用 ContextMenu.FindName 找到（名字域不通），
        // 必须用 Tag 遍历查找
        var items = tray.ContextMenu.Items.OfType<MenuItem>().ToList();

        if (items.FirstOrDefault(i => (string?)i.Tag == "OpenPanel") is { } panelItem)
        {
            panelItem.Header = Strings.MenuOpenPanel;
        }

        if (items.FirstOrDefault(i => (string?)i.Tag == "EnableDim") is { } enableItem)
        {
            enableItem.Header = Strings.MenuEnableDim;
            enableItem.IsChecked = controller.Enabled;
        }

        if (items.FirstOrDefault(i => (string?)i.Tag == "Screens") is { } screensItem)
        {
            screensItem.Header = Strings.MenuSelectScreen;
            screensItem.Items.Clear();
            screensItem.Items.Add(MakeScreenItem(Strings.AllScreens, null));
            foreach (var slot in controller.Slots)
            {
                screensItem.Items.Add(MakeScreenItem(slot.Label, slot.Device));
            }
        }

        if (items.FirstOrDefault(i => (string?)i.Tag == "Exit") is { } exitItem)
        {
            exitItem.Header = Strings.MenuExit;
        }

        tray.ToolTipText = controller.BuildStatusText();
    }

    private MenuItem MakeScreenItem(string header, string? device)
    {
        var item = new MenuItem { Header = header, IsCheckable = true, Tag = device };
        item.IsChecked = controller.SelectedDevice == device;
        item.Click += MenuScreen_Click;
        return item;
    }

    private void MenuScreen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item)
        {
            controller.SetSelectedDevice(item.Tag as string);
        }
    }

    private void ShowPanel()
    {
        panel ??= new ControlPanelWindow(controller, ApplyHotkeyFromPanel, GetHotkeyText, ApplyLanguageFromPanel);
        panel.SyncFromController();
        panel.Show();
        panel.Activate();
    }

    private void TrayIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e) => ShowPanel();

    private void ShowPanel_Click(object sender, RoutedEventArgs e) => ShowPanel();

    private void MenuEnableDim_Click(object sender, RoutedEventArgs e)
    {
        // 点击时菜单项自身已切换勾选，这里按控制器当前状态取反，再由 StateChanged 同步回 UI
        controller.SetEnabled(!controller.Enabled);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        IsShuttingDown = true;
        FlushSettings();
        tray.Dispose();
        controller.Dispose();
        messageWindow.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        FlushSettings();
        tray?.Dispose();
        controller?.Dispose();
        base.OnExit(e);
    }
}
