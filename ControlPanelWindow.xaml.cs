using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenBrightnessAdjuster;

/// <summary>
/// 调节面板：屏幕选择 + 滑条 + 开关 + 热键设置。点 × 只隐藏到托盘，不退出程序。
/// </summary>
public partial class ControlPanelWindow : Window
{
    private readonly DimController controller;
    private readonly Func<HotkeyAction, HotkeySpec?, bool> applyHotkey;
    private readonly Func<IReadOnlyDictionary<HotkeyAction, string>> getHotkeyText;
    private readonly Action<string> applyLanguage;
    private bool syncing; // 防止“UI 同步 -> 触发事件 -> 改状态 -> 再同步”死循环

    public ControlPanelWindow(DimController controller,
        Func<HotkeyAction, HotkeySpec?, bool> applyHotkey,
        Func<IReadOnlyDictionary<HotkeyAction, string>> getHotkeyText,
        Action<string> applyLanguage)
    {
        InitializeComponent();
        this.controller = controller;
        this.applyHotkey = applyHotkey;
        this.getHotkeyText = getHotkeyText;
        this.applyLanguage = applyLanguage;
        controller.StateChanged += SyncFromController;
        Closed += (_, _) => controller.StateChanged -= SyncFromController;
        ApplyLanguage();
        SyncFromController();
    }

    /// <summary>语言切换后刷新面板全部静态文案。</summary>
    public void ApplyLanguage()
    {
        syncing = true;
        try
        {
            Title = Strings.AppName;
            TitleText.Text = Strings.AppName;
            SubtitleText.Text = Strings.PanelSubtitle;
            ScreenLabelText.Text = Strings.ScreenLabel;
            ScreenEnabledCheck.Content = Strings.EnableThisScreen;
            EnableCheck.Content = Strings.EnableAll;
            HotkeyHintText.Text = Strings.HotkeyHint;
            BrighterLabelText.Text = Strings.Brighter;
            DarkerLabelText.Text = Strings.Darker;
            ToggleLabelText.Text = Strings.Toggle;
            CloseHintText.Text = Strings.CloseHint;
            LanguageLabelText.Text = Strings.LanguageLabel;

            RebuildLanguageCombo();
        }
        finally
        {
            syncing = false;
        }
    }

    private void RebuildLanguageCombo()
    {
        string current = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

        LanguageCombo.Items.Clear();
        LanguageCombo.Items.Add(new ComboBoxItem { Content = Strings.FollowSystem, Tag = "" });
        LanguageCombo.Items.Add(new ComboBoxItem { Content = "中文", Tag = "zh" });
        LanguageCombo.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });

        var match = LanguageCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(it => (string?)it.Tag == current);
        LanguageCombo.SelectedItem = match ?? LanguageCombo.Items[0];
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing) return;
        applyLanguage((LanguageCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "");
        ApplyLanguage(); // “跟随系统”文本需要按新语言刷新
    }

    public void SyncFromController()
    {
        syncing = true;
        try
        {
            RebuildScreenCombo();
            RefreshSliderAndText();
            EnableCheck.IsChecked = controller.Enabled;
            SyncHotkeyTexts();
        }
        finally
        {
            syncing = false;
        }
    }

    private void RebuildScreenCombo()
    {
        // 记住当前选择（按设备名），显示器列表变化后尽量恢复
        string? current = (ScreenCombo.SelectedItem as ComboBoxItem)?.Tag as string;

        ScreenCombo.Items.Clear();
        ScreenCombo.Items.Add(new ComboBoxItem { Content = "全部屏幕", Tag = null });
        foreach (var slot in controller.Slots)
        {
            ScreenCombo.Items.Add(new ComboBoxItem { Content = slot.Label, Tag = slot.Device });
        }

        var match = ScreenCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(it => (string?)it.Tag == current);
        ScreenCombo.SelectedItem = match ?? ScreenCombo.Items[0];

        // 每屏独立开关只对具体屏幕有意义，“全部屏幕”时隐藏
        var device = (ScreenCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        if (device == null)
        {
            ScreenEnabledCheck.Visibility = Visibility.Collapsed;
        }
        else
        {
            ScreenEnabledCheck.Visibility = Visibility.Visible;
            ScreenEnabledCheck.IsChecked = controller.GetScreenEnabled(device);
        }
    }

    private void RefreshSliderAndText()
    {
        int percent = controller.GetDisplayPercent();
        DimSlider.Value = percent;
        PercentText.Text = $"{percent}%";
    }

    private void SyncHotkeyTexts()
    {
        var texts = getHotkeyText();
        BrighterBox.Text = texts.GetValueOrDefault(HotkeyAction.Brighter, "未注册");
        DarkerBox.Text = texts.GetValueOrDefault(HotkeyAction.Darker, "未注册");
        ToggleBox.Text = texts.GetValueOrDefault(HotkeyAction.Toggle, "未注册");
    }

    private void ScreenCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing) return;
        controller.SetSelectedDevice((ScreenCombo.SelectedItem as ComboBoxItem)?.Tag as string);
    }

    private void ScreenEnabledCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (syncing || controller is null) return;

        var device = (ScreenCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        if (device == null) return; // “全部屏幕”不适用

        controller.SetScreenEnabled(device, ScreenEnabledCheck.IsChecked == true);
    }

    private void DimSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (syncing || controller is null) return;

        PercentText.Text = $"{(int)DimSlider.Value}%";
        controller.SetPercent((int)DimSlider.Value);
    }

    private void EnableCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (syncing || controller is null) return;
        controller.SetEnabled(EnableCheck.IsChecked == true);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var box = (TextBox)sender;
        var action = ActionOf(box);

        // Esc = 清除自定义，恢复默认键位链
        if (e.Key == Key.Escape)
        {
            applyHotkey(action, null);
            SyncHotkeyTexts();
            return;
        }

        // 只按了修饰键时等待下一个按键
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            box.Text = "需包含 Ctrl/Alt/Shift/Win";
            return;
        }

        var spec = new HotkeySpec(
            Hotkeys.ToWin32(Keyboard.Modifiers),
            (uint)KeyInterop.VirtualKeyFromKey(key));

        applyHotkey(action, spec);
        SyncHotkeyTexts();
    }

    private HotkeyAction ActionOf(TextBox box) =>
        box == BrighterBox ? HotkeyAction.Brighter
        : box == DarkerBox ? HotkeyAction.Darker
        : HotkeyAction.Toggle;

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        // 关闭 = 藏到托盘；真正退出走托盘菜单“退出”
        if (!App.IsShuttingDown)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
