---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 7212bba2991f8f5303a142113b4d9c98_79b9f34967c611f1aa625254006c9bbf
    ReservedCode1: drvnSMB3TDRnY4d9lA7xz/JP4Dt2jmfg23kQbalp8n8iM3J6n0vSGcyiIJv9lXA+/OSD42Xw1skNspcW21omUdnhYKxHer1o4/s+9UOI/kIMlfgJoI9wMKKTHAn+ZpGiuTXB0HchtDO+gJC+zyqn6Zp3YD2o6LPHimQL7JpOYgZD63h7cV258xoH6AU=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 7212bba2991f8f5303a142113b4d9c98_79b9f34967c611f1aa625254006c9bbf
    ReservedCode2: drvnSMB3TDRnY4d9lA7xz/JP4Dt2jmfg23kQbalp8n8iM3J6n0vSGcyiIJv9lXA+/OSD42Xw1skNspcW21omUdnhYKxHer1o4/s+9UOI/kIMlfgJoI9wMKKTHAn+ZpGiuTXB0HchtDO+gJC+zyqn6Zp3YD2o6LPHimQL7JpOYgZD63h7cV258xoH6AU=
---

<p align="center">
  <img src="infimouse.png" width="128" alt="InfiMouse" />
</p>

<h1 align="center">InfiMouse</h1><br />

<p align="center">
  <strong>游戏画面录制助手 — 鼠标轨迹控制与同步输入工具</strong>
</p>

<p align="center">
  <em>InfiMouse 是一款 Windows 桌面工具，专为游戏画面录制设计。<br />通过高度拟人化的贝塞尔曲线模拟鼠标运动轨迹，<br />同时同步键盘与手柄输入，实现"平滑运镜 + 复合操控"的自动化。</em>
</p>

<p align="center">
  <sub>包括本文档及程序在内均包含 AI 辅助生成。不代表本人立场。</sub>
</p>

---

<br />

<p align="center">
  <strong>InfiMouse</strong><br />
  ▸ 贝塞尔曲线路径规划：交互式画布上拖拽、添加、删除路径点，支持直线模式<br />
  ▸ 缓动函数：Linear、EaseOut、Quadratic、Cubic、Sine、Circular 多种速度曲线<br />
  ▸ 键盘事件时间线：WASD / 方向键 / 修饰键与鼠标运动在同一时间线上精确同步<br />
  ▸ 手柄注入：通过 XInput 模拟 Xbox 兼容虚拟手柄<br />
  ▸ 反检测拟人化：抖动、速度方差、过冲、随机停顿模拟真人操作<br />
  ▸ 循环播放 + AssistiveTouch 浮窗 + 全局快捷键 + 新手引导
</p>

<br />

---

## 快速开始

```powershell
# 构建
dotnet build InfiMouse.sln

# 构建输出
InfiMouse.UI\bin\x64\Debug\net8.0-windows10.0.19041.0\InfiMouse.UI.exe
```

> **注意**：部分环境下 XamlCompiler.exe 存在兼容性问题，XAML 编译使用托管路径（`UseXamlCompilerExecutable=false`），所有 UI 通过 C# 代码构建。

### 前置条件

- **Windows 10 (2004+) / Windows 11** (x64)
- **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**
- **管理员权限**（跨进程输入注入所需）

---

## 使用方法

1. 以管理员身份运行
2. 在画布上点击添加路径点（起点 → 终点）
3. 在右侧面板调整动画参数
4. 在底部时间线添加键盘事件（WASD 等）
5. 按 `Ctrl+Enter` 或点击 **播放** 执行
6. 启用循环以连续播放；按 `Ctrl+S` 停止

---

## 快捷键

| 快捷键 | 操作 |
|--------|------|
| `Ctrl+Enter` | 播放 |
| `Space` | 暂停（仅前台） |
| `Ctrl+S` | 停止 |
| `Ctrl+N` | 新建项目 |
| `Ctrl+O` | 打开项目 |
| `Ctrl+Shift+S` | 保存项目 |

---

## 项目结构

```
InfiMouse/
├── InfiMouse.sln
├── CHANGELOG.md
├── InfiMouse.Core/          # 贝塞尔曲线、运动规划、事件播放
├── InfiMouse.Input/         # SendInput、XInput、虚拟键码
├── InfiMouse.Model/         # 数据模型、序列化
├── InfiMouse.AntiDetection/ # 拟人化、抖动、过冲
└── InfiMouse.UI/            # WinUI 3 应用
    ├── App.xaml / App.xaml.cs
    ├── Program.cs
    ├── MainWindow.xaml.cs
    ├── EditorPage.xaml.cs
    ├── SettingsPage.xaml.cs
    ├── FloatingWindow.xaml.cs
    ├── SettingsManager.cs
    ├── GlobalHotkeyManager.cs
    └── ViewModels/
```

---

## 技术栈

| 层级 | 技术 | 用途 |
|------|------|------|
| GUI | WinUI 3 (Windows App SDK 1.6) | 原生 Windows 11 界面 |
| 架构 | MVVM (CommunityToolkit.Mvvm) | 数据绑定、命令 |
| 鼠标/键盘 | SendInput (P/Invoke) | 硬件级注入 |
| 手柄 | XInput (P/Invoke `xinput1_4.dll`) | Xbox 手柄模拟 |
| 数学 | 贝塞尔曲线、弧长参数化 | 轨迹规划 |
| 序列化 | System.Text.Json | 项目保存／加载 (`.infimouse`) |
| 快捷键 | GlobalHotkeyManager (Win32 RegisterHotKey) | 系统级快捷键注册 |
| 设置 | SettingsManager (JSON) | 持久化到 `%APPDATA%\InfiMouse\settings.json` |
| 视觉 | Mica／Acrylic Backdrop | 现代 Windows 11 美学 |

---

## 更新日志

详见 [CHANGELOG.md](CHANGELOG.md)

---

<p align="center">
    本工具与 <strong>叠纸游戏</strong> / <strong>Infold Games</strong> 无关。<br />
    无限暖暖 © 2022 Papergames, ALL RIGHTS RESERVED.
</p>

