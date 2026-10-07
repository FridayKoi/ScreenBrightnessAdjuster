# 屏幕调暗器 / Screen Dimmer

<p align="left">
  <img src="Assets/tray.png" width="64" alt="icon" />
</p>

在系统层叠一层可调的黑色遮罩，让你的屏幕**比硬件最低亮度更暗**——为黑暗环境而生。

A tiny Windows tray utility that overlays a dimming layer on your screen, letting you go **below the monitor's minimum brightness**. Built for working in the dark.

## ✨ 功能 / Features

- 🌙 突破显示器最低亮度限制 / Go below the hardware minimum brightness
- 🖥️ 多显示器独立调节（每屏亮度 + 独立开关）/ Per-display dimming & on/off
- ⌨️ 全局热键，可自定义，被占用时自动降级备用键位 / Customizable global hotkeys with automatic fallback
- 🎚️ 1% 精细调节，按住热键连续调光 / 1% steps, hold hotkey for continuous dimming
- 🌐 中英双语界面，跟随系统 / Chinese & English UI, follows system language
- 📦 绿色单文件，免安装 / Portable single exe, no installation

## 📥 下载 / Download

前往 [Releases](../../releases) 页面下载 `ScreenBrightnessAdjuster.exe`，双击即用。

Grab the latest `ScreenBrightnessAdjuster.exe` from the [Releases](../../releases) page — no installation, no runtime needed.

> 首次运行如出现 SmartScreen 提示，点“更多信息 → 仍要运行”即可。
> If SmartScreen shows a warning on first run, click "More info → Run anyway".

## ⌨️ 默认热键 / Default Hotkeys

| 热键 / Hotkey | 功能 / Action |
|---|---|
| `Ctrl+Alt+↓` | 更暗 / Dimmer |
| `Ctrl+Alt+↑` | 更亮 / Brighter |
| `Ctrl+Alt+Space` | 开 / 关 Toggle |

> 若热键被显卡驱动等占用，程序会自动降级到 `Ctrl+Shift+…`，也可以在面板里自定义。
> If the default combo is taken (e.g. by GPU driver hotkeys), the app falls back to `Ctrl+Shift+…` automatically. Custom combos are available in the panel.

## 🖼️ 截图 / Screenshots

<!-- TODO: 把截图放到 docs/ 目录后取消注释
![中文界面](docs/screenshot-zh.png)
![English UI](docs/screenshot-en.png)
-->

## 🛠️ 工作原理 / How it works

启动后在每块屏幕上放置一个**鼠标点击穿透**的黑色遮罩窗口（WPF `AllowsTransparency`），实时调整其透明度即可实现低于硬件下限的压暗，几乎零性能开销。

It places a click-through black overlay window (WPF `AllowsTransparency`) on each display and adjusts its opacity — dimming below hardware limits with near-zero overhead.

## 🧑‍💻 从源码构建 / Build

- Visual Studio 2022（含“.NET 桌面开发”工作负载）或 .NET 8 SDK
- 克隆仓库后打开 `ScreenBrightnessAdjuster.sln`，按 `F5` 运行

```bash
git clone https://github.com/FridayKoi/ScreenBrightnessAdjuster.git
```

## 📄 许可 / License

[MIT](LICENSE)
