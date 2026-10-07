using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace ScreenBrightnessAdjuster;

/// <summary>
/// 单个显示器的黑色遮罩窗口。
/// 透明度用 WPF 原生的 Opacity（AllowsTransparency 由 WPF 管理分层，不会被系统重置），
/// 变暗快、关闭柔和的不对称渐变；点击穿透用 WS_EX_TRANSPARENT，并在显示后、置顶保活时反复补写。
/// </summary>
public partial class OverlayWindow : Window
{
    private const int FadeInMilliseconds = 80;   // 变暗要快，避免“延迟一下才黑”的迟滞感
    private const int FadeOutMilliseconds = 220; // 关闭/淡出保持柔和

    private readonly Rect physicalBounds;
    private bool hideAfterFadeOut;

    public OverlayWindow(Rect physicalBounds)
    {
        InitializeComponent();
        this.physicalBounds = physicalBounds;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        ApplyClickThroughStyles();

        // Win11 关闭圆角，避免遮罩四角漏光
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int preference = NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref preference, sizeof(int));

        SizeToMonitor();
    }

    /// <summary>WPF 在 Show 的过程中可能重置扩展样式，首次渲染完成后补写一次。</summary>
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ApplyClickThroughStyles();
    }

    private void ApplyClickThroughStyles()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle
            | NativeMethods.WS_EX_TRANSPARENT   // 鼠标/键盘穿透
            | NativeMethods.WS_EX_TOOLWINDOW    // 不进 Alt+Tab
            | NativeMethods.WS_EX_NOACTIVATE);  // 点击时不激活
    }

    /// <summary>把窗口精确铺满目标显示器（物理像素坐标，天然兼容多屏 DPI 差异）。</summary>
    public void SizeToMonitor()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            (int)physicalBounds.X, (int)physicalBounds.Y,
            (int)physicalBounds.Width, (int)physicalBounds.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    /// <summary>定期把窗口压回最顶层并补写穿透样式（被其他窗口盖住或样式被重置时兜底）。</summary>
    public void Reassert()
    {
        ApplyClickThroughStyles();

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>
    /// 应用压暗状态。变暗用短渐变（接近即时），关闭压暗时淡出完成后再隐藏窗口。
    /// </summary>
    public void ApplyDim(bool enabled, double fraction, bool animate = true)
    {
        if (enabled)
        {
            hideAfterFadeOut = false; // 取消排队中的淡出隐藏
            if (!IsVisible) Show();
            AnimateOpacity(Math.Clamp(fraction, 0, 1), animate, FadeInMilliseconds);
        }
        else if (IsVisible)
        {
            if (!animate)
            {
                Hide();
                return;
            }

            hideAfterFadeOut = true;
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(FadeOutMilliseconds));
            fadeOut.Completed += (_, _) =>
            {
                if (hideAfterFadeOut)
                {
                    Hide();
                    hideAfterFadeOut = false;
                }
            };
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    private void AnimateOpacity(double target, bool animate, int durationMilliseconds)
    {
        if (!animate)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = target;
            return;
        }

        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            EasingFunction = new QuadraticEase(),
        };
        BeginAnimation(OpacityProperty, animation);
    }
}
