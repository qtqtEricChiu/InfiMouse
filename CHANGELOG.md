# InfiMouse Changelog

## 2025-06-14 — 稳定性修复与功能完善

### 核心修复：AccessViolationException 根因定位与修复

**问题**：应用启动后出现 `AccessViolationException (0xC000027B)` 或 `InvalidOperationException` 原生崩溃，导致窗口无法正常打开。

**根因**：
1. `App.xaml` 文件意外丢失，导致 WinUI 3 XAML 资源管线缺失 `XamlControlsResources`
2. `InfiMouse.UI.csproj` 中 `EnableDefaultApplicationDefinition=false` 进一步阻断了 XAML 资源编译
3. 原生 XAML 编译器（`XamlCompiler.exe`）在此环境中存在兼容性问题（退出码 1）

**修复**：
- 创建 `App.xaml`，包含 `XamlControlsResources` 合并字典
- 设置 `EnableDefaultApplicationDefinition=true`
- 保留 `UseXamlCompilerExecutable=false`（托管编译器）
- `App` 类改为 `partial class`，构造函数中调用 `InitializeComponent()`
- csproj 新增 `DefineConstants=DISABLE_XAML_GENERATED_MAIN` 解决与 `Program.cs` 的冲突

**影响范围**：`App.xaml`、`App.xaml.cs`、`InfiMouse.UI.csproj`

---

### 已知限制：Frame.Navigate 在 unpackaged WinUI 3 中不兼容

**问题**：`Frame.Navigate(typeof(自定义Page))` 在 unpackaged WinUI 3 环境中对程序集内自定义 Page 类型触发 AccessViolationException。`Frame.Navigate(typeof(Page))`（框架基类）可正常工作。

**解决方案**：全局使用 `Frame.Content = new XxxPage()` 替代。页面切换动画通过自定义 `TransitionToPage` 方法实现（Storyboard + DoubleAnimation 淡入淡出）。

---

### 功能新增

#### 编辑器顶部控制栏
- `EditorPage.BuildUI()` 顶部新增 Row 0，包含播放/暂停/停止/新建/打开/保存 6 个按钮
- 按钮间用垂直分隔线分组（播放控制 vs 文件操作）
- 全部绑定到 `MainViewModel` 对应命令

#### 全局快捷键系统
- 新增 `GlobalHotkeyManager`，基于 Win32 `RegisterHotKey` 实现全局热键注册
- 默认快捷键：`Ctrl+Enter` 播放、`Ctrl+P` 暂停、`Ctrl+S` 停止、`Ctrl+N` 新建、`Ctrl+O` 打开、`Ctrl+Shift+S` 保存
- 冲突检测：注册前检查 `GetLastError()==1409`，冲突时弹窗提示
- Space 保留为前台局部处理（不占用全局热键以避免冲突）

#### 快捷键可编辑（SettingsPage）
- 设置页快捷键指南区域每行改为可点击按钮
- 点击进入捕获模式，按下新键位后读取修饰键 + 主键
- 通过 `GlobalHotkeyManager` 注销旧热键、注册新热键，冲突时弹窗提示
- 键位变更持久化到 `settings.json`

#### 新手引导系统
- `EditorPage.ShowTutorialAsync()` 实现 5 步引导：工具栏 → Canvas → 参数面板 → 时间轴 → 日志
- 半透明遮罩 + 文字说明 + 逐步导航
- 引导完成后状态持久化到 LocalSettings，不再重复弹出

#### 状态持久化系统
- 新增 `SettingsManager` 类，所有设置项 JSON 序列化到 `%APPDATA%\InfiMouse\settings.json`
- 持久化内容：帧率、速度、灵敏度、快捷键绑定、侧边栏折叠状态、侧边栏选中标签页、教程完成状态
- 应用启动时自动加载，变更时实时保存
- 页面切换时设置和编辑器侧边栏状态不再丢失

---

### UI 改进

#### 云母视效适配
- 编辑器全部控件背景设为极低不透明度 `SolidColorBrush`（0x08~0x15 透明通道）
- 顶级栏按钮、Canvas 画布、日志面板、侧边栏、路径信息栏全部透明化
- 窗口 MicaBackdrop 完全透出，实现统一云母视觉效果

#### 布局重构
- **侧边栏跨行**：`SetRowSpan(_sidebarGrid, 2)`，侧边栏同时覆盖 Canvas（Row 1）和日志（Row 2）整列高度
- **日志下移**：日志面板移至编辑器底部（Canvas 下方），仅占 Col 0
- **新布局**：
  ```
  Row 0: 工具栏
  Row 1: Canvas (Col 0) | 侧边栏 (Col 1, RowSpan=2)
  Row 2: 日志 (Col 0)
  ```

#### SettingsPage 改进
- 快捷键指南区域展示全部 6 个快捷键及功能说明
- 快捷键行可点击编辑（捕获模式）
- 所有设置卡片使用半透明 AcrylicBrush 背景
- 「浮窗模式」ToggleSwitch 与 FloatingWindow 状态双向同步

#### WinUI 3 标题栏适配
- `ExtendsContentIntoTitleBar = true`
- `AppWindow.TitleBar` 按钮四色配置（激活/非激活背景 #141424，前景白色）
- 32px 可拖拽标题栏区域

#### 浮窗模式完善
- FloatingWindow 设为无标题栏窗口
- 添加自定义关闭按钮（点击关闭浮窗，返回主窗口）
- 关闭时通过 `App.FloatingWindowStateChanged` 事件同步 SettingsPage 的 ToggleSwitch 状态
- 双向同步：设置页开关 ↔ 浮窗显示/隐藏

---

### 编辑器功能

#### 直线绘制支持
- `DrawBezierPreview` 检测 Linear 缓动函数时绘制直线段（替代贝塞尔曲线）
- `DrawControlPointHandles` 同步跳过控制点渲染
- 缓动函数切换时立即重绘预览

#### 光标移动默认帧率
- 默认值通过 `SettingsManager.GetScreenRefreshRate()` 获取屏幕刷新率
- 使用 `EnumDisplaySettings` Win32 API，获取失败时 fallback 到 60Hz
- 设置页显示当前检测到的屏幕刷新率参考值

---

### 编译配置变更

| 配置项 | 旧值 | 新值 | 说明 |
|--------|------|------|------|
| `EnableDefaultApplicationDefinition` | false | true | 启用 XAML 应用定义编译 |
| `UseXamlCompilerExecutable` | false | false | 保持托管编译器（原生编译器不兼容） |
| `DefineConstants` | — | `DISABLE_XAML_GENERATED_MAIN` | 阻止自动生成的 Program 类 |
| `WindowsAppSDKSelfContained` | true | true | 自包含部署 |

---

### 已知限制

1. `Frame.Navigate(typeof(自定义Page))` 在 unpackaged WinUI 3 中不兼容，使用 `Frame.Content = new` 替代
2. 原生 XAML 编译器（`XamlCompiler.exe`）在当前环境退出码 1，需使用托管编译器
