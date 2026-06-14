using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;

namespace InfiMouse.UI;

public sealed class SettingsPage : Page
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private ToggleSwitch ToggleFloatingWindow = null!;
    private NumberBox NumDefaultDuration = null!;
    private NumberBox NumDefaultFrameCount = null!;
    private ComboBox CmbDefaultEasing = null!;
    private ToggleSwitch ToggleDefaultHumanization = null!;
    private NumberBox NumDefaultJitter = null!;
    private NumberBox NumDefaultSpeedVariance = null!;
    private ToggleSwitch ToggleDefaultOvershoot = null!;
    private NumberBox NumDefaultOvershootPixels = null!;
    private CheckBox ChkAutoLoad = null!;
    private CheckBox ChkMinimizeTray = null!;
    private CheckBox ChkViGEm = null!;
    private CheckBox ChkGamepadIndicator = null!;

    // Key capture state
    private bool _isCapturingKey = false;
    private string? _captureTargetId = null;
    private Grid? _shortcutGrid = null;
    private Button? _captureTargetBtn = null;

    public SettingsPage()
    {
        BuildUI();
        LoadSettings();
        ToggleFloatingWindow.IsOn = App.IsFloatingWindowVisible;
        App.FloatingWindowStateChanged += OnFloatingWindowExternalChange;

        this.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x01, 0x00, 0x00, 0x00));
    }

    private void OnFloatingWindowExternalChange(bool visible)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ToggleFloatingWindow.IsOn = visible;
        });
    }

    private void LoadSettings()
    {
        var d = SettingsManager.Data;
        NumDefaultDuration.Value = d.DefaultTotalDurationMs;
        NumDefaultFrameCount.Value = d.DefaultFrameCount;
        CmbDefaultEasing.SelectedIndex = Math.Clamp(d.DefaultEasingIndex, 0, 4);
        ToggleDefaultHumanization.IsOn = d.DefaultEnableHumanization;
        ToggleDefaultOvershoot.IsOn = d.DefaultEnableOvershoot;
        NumDefaultJitter.Value = d.DefaultJitterStrength;
        NumDefaultSpeedVariance.Value = d.DefaultSpeedVariance;
        NumDefaultOvershootPixels.Value = d.DefaultOvershootPixels;
        ChkAutoLoad.IsChecked = d.AutoLoadLastProject;
        ChkMinimizeTray.IsChecked = d.MinimizeToTray;
        ChkViGEm.IsChecked = d.EnableViGEm;
        ChkGamepadIndicator.IsChecked = d.ShowGamepadIndicator;
    }

    private void SaveAllSettings()
    {
        var d = SettingsManager.Data;
        d.DefaultTotalDurationMs = NumDefaultDuration.Value;
        d.DefaultFrameCount = (int)NumDefaultFrameCount.Value;
        d.DefaultEasingIndex = CmbDefaultEasing.SelectedIndex;
        d.DefaultEnableHumanization = ToggleDefaultHumanization.IsOn;
        d.DefaultEnableOvershoot = ToggleDefaultOvershoot.IsOn;
        d.DefaultJitterStrength = NumDefaultJitter.Value;
        d.DefaultSpeedVariance = NumDefaultSpeedVariance.Value;
        d.DefaultOvershootPixels = NumDefaultOvershootPixels.Value;
        d.AutoLoadLastProject = ChkAutoLoad.IsChecked == true;
        d.MinimizeToTray = ChkMinimizeTray.IsChecked == true;
        d.EnableViGEm = ChkViGEm.IsChecked == true;
        d.ShowGamepadIndicator = ChkGamepadIndicator.IsChecked == true;
        SettingsManager.Save();
    }

    private void BuildUI()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var rootStack = new StackPanel { Spacing = 20, MaxWidth = 600 };

        rootStack.Children.Add(new TextBlock
        {
            Text = "设置", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // 常规设置（暂时隐藏项目相关选项）
        var generalCard = MakeCard("常规");
        ChkAutoLoad = new CheckBox { Content = "启动时自动加载上次项目", IsChecked = false, Visibility = Visibility.Collapsed };
        ChkAutoLoad.Checked += (_, _) => SaveAllSettings();
        ChkAutoLoad.Unchecked += (_, _) => SaveAllSettings();
        generalCard.Children.Add(ChkAutoLoad);

        ChkMinimizeTray = new CheckBox { Content = "最小化到系统托盘", IsChecked = true };
        ChkMinimizeTray.Checked += (_, _) => SaveAllSettings();
        ChkMinimizeTray.Unchecked += (_, _) => SaveAllSettings();
        generalCard.Children.Add(ChkMinimizeTray);
        generalCard.Visibility = Visibility.Collapsed; // 暂时隐藏项目相关
        rootStack.Children.Add(WrapCard(generalCard));

        // 默认动画参数
        var animCard = MakeCard("默认动画参数");
        NumDefaultDuration = new NumberBox { Value = 2000, Minimum = 200, Maximum = 10000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumDefaultDuration.ValueChanged += (_, _) => SaveAllSettings();
        animCard.Children.Add(WrapNumberBoxSetting("默认总时长 (ms)", NumDefaultDuration));

        var frameRateText = new TextBlock
        {
            Text = $"默认帧数（屏幕刷新率: {SettingsManager.GetScreenRefreshRate()}Hz）",
            FontSize = 12, Opacity = 0.7,
        };
        NumDefaultFrameCount = new NumberBox { Value = 60, Minimum = 10, Maximum = 200, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumDefaultFrameCount.ValueChanged += (_, _) => SaveAllSettings();
        var frameWrap = new StackPanel { Spacing = 4 };
        frameWrap.Children.Add(frameRateText);
        frameWrap.Children.Add(NumDefaultFrameCount);
        animCard.Children.Add(frameWrap);

        CmbDefaultEasing = new ComboBox { SelectedIndex = 2 };
        CmbDefaultEasing.Items.Add(new ComboBoxItem { Content = "线性 (Linear)" });
        CmbDefaultEasing.Items.Add(new ComboBoxItem { Content = "EaseOut Quadratic" });
        CmbDefaultEasing.Items.Add(new ComboBoxItem { Content = "EaseOut Cubic" });
        CmbDefaultEasing.Items.Add(new ComboBoxItem { Content = "EaseOut Sine" });
        CmbDefaultEasing.Items.Add(new ComboBoxItem { Content = "EaseOut Circular" });
        CmbDefaultEasing.SelectionChanged += (_, _) => SaveAllSettings();
        animCard.Children.Add(WrapComboSetting("默认缓动函数", CmbDefaultEasing));
        rootStack.Children.Add(WrapCard(animCard));

        // 反检测默认参数
        var antiCard = MakeCard("反检测默认参数");
        ToggleDefaultHumanization = new ToggleSwitch { Header = "默认启用拟人化", IsOn = false };
        ToggleDefaultHumanization.Toggled += (_, _) => SaveAllSettings();
        antiCard.Children.Add(ToggleDefaultHumanization);

        ToggleDefaultOvershoot = new ToggleSwitch { Header = "默认启用超调", IsOn = false };
        ToggleDefaultOvershoot.Toggled += (_, _) => SaveAllSettings();
        antiCard.Children.Add(ToggleDefaultOvershoot);

        NumDefaultJitter = new NumberBox { Value = 2, Minimum = 0, Maximum = 10, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumDefaultJitter.ValueChanged += (_, _) => SaveAllSettings();
        antiCard.Children.Add(WrapNumberBoxSetting("默认抖动强度 (像素)", NumDefaultJitter));

        NumDefaultSpeedVariance = new NumberBox { Value = 5, Minimum = 0, Maximum = 30, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumDefaultSpeedVariance.ValueChanged += (_, _) => SaveAllSettings();
        antiCard.Children.Add(WrapNumberBoxSetting("默认速度波动 (%)", NumDefaultSpeedVariance));

        NumDefaultOvershootPixels = new NumberBox { Value = 0, Minimum = 0, Maximum = 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumDefaultOvershootPixels.ValueChanged += (_, _) => SaveAllSettings();
        antiCard.Children.Add(WrapNumberBoxSetting("默认超调像素", NumDefaultOvershootPixels));
        rootStack.Children.Add(WrapCard(antiCard));

        // 浮窗设置
        var floatCard = MakeCard("浮窗控制");
        ToggleFloatingWindow = new ToggleSwitch { Header = "显示浮窗控制面板", IsOn = false };
        ToggleFloatingWindow.Toggled += OnFloatingWindowToggled;
        floatCard.Children.Add(ToggleFloatingWindow);
        floatCard.Children.Add(new TextBlock
        {
            Text = "启用后屏幕边缘显示 iOS 辅助触控风格小圆点，点击展开快捷操作",
            FontSize = 11, Opacity = 0.5
        });
        rootStack.Children.Add(WrapCard(floatCard));

        // 手柄设置
        var gamepadCard = MakeCard("手柄设置");
        ChkViGEm = new CheckBox { Content = "启用 ViGEm 虚拟手柄", IsChecked = false };
        ChkViGEm.Checked += (_, _) => SaveAllSettings();
        ChkViGEm.Unchecked += (_, _) => SaveAllSettings();
        gamepadCard.Children.Add(ChkViGEm);

        ChkGamepadIndicator = new CheckBox { Content = "显示手柄状态指示器", IsChecked = true };
        ChkGamepadIndicator.Checked += (_, _) => SaveAllSettings();
        ChkGamepadIndicator.Unchecked += (_, _) => SaveAllSettings();
        gamepadCard.Children.Add(ChkGamepadIndicator);
        rootStack.Children.Add(WrapCard(gamepadCard));

        // 快捷键 — 可编辑
        var shortcutCard = MakeCard("编辑器快捷键 — 点击按钮修改键位");
        _shortcutGrid = new Grid();
        _shortcutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        _shortcutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _shortcutGrid.RowSpacing = 6;

        RebuildShortcutRows();
        shortcutCard.Children.Add(_shortcutGrid);
        rootStack.Children.Add(WrapCard(shortcutCard));

        // 教程重放
        var tutorialCard = MakeCard("新手引导");
        var btnReplayTutorial = new Button
        {
            Content = "\u25B6 重放新手引导",
            FontSize = 13,
            Padding = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x8A, 0xB4, 0xF8)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8)),
            CornerRadius = new CornerRadius(6),
        };
        btnReplayTutorial.Click += (_, _) =>
        {
            SettingsManager.Data.TutorialCompleted = false;
            SettingsManager.Save();
            _ = new ContentDialog
            {
                Title = "已重置",
                Content = "新手引导已重置。下次切换到编辑器页面时将重新显示引导。",
                CloseButtonText = "确定",
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsync();
        };
        tutorialCard.Children.Add(btnReplayTutorial);
        rootStack.Children.Add(WrapCard(tutorialCard));

        // 关于
        var aboutCard = MakeCard("关于 InfiMouse");
        aboutCard.Children.Add(new TextBlock
        {
            Text = "游戏画面录制助手 —— 鼠标轨迹控制与同步输入工具",
            FontSize = 13, Opacity = 0.7
        });
        aboutCard.Children.Add(new TextBlock
        {
            Text = "版本 1.0.0 | C# + WinUI 3 | .NET 8",
            FontSize = 12, Opacity = 0.5
        });
        rootStack.Children.Add(WrapCard(aboutCard));

        rootStack.Children.Add(new TextBlock { Height = 32 });

        // KeyDown capture on the page
        this.KeyDown += OnPageKeyDown;

        scroll.Content = rootStack;
        this.Content = scroll;
    }

    private void RebuildShortcutRows()
    {
        _shortcutGrid!.Children.Clear();
        _shortcutGrid.RowDefinitions.Clear();

        var shortcuts = SettingsManager.Data.Shortcuts;
        for (int i = 0; i < shortcuts.Count; i++)
        {
            _shortcutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var entry = shortcuts[i];

            var keyBtn = new Button
            {
                Content = entry.DisplayString,
                Tag = entry.Id,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, 0x8A, 0xB4, 0xF8)),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8)),
                CornerRadius = new CornerRadius(4),
            };
            keyBtn.Click += OnShortcutBtnClick;

            var descText = new TextBlock
            {
                Text = entry.Label,
                FontSize = 12,
                Opacity = 0.8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };

            Grid.SetRow(keyBtn, i); Grid.SetColumn(keyBtn, 0);
            Grid.SetRow(descText, i); Grid.SetColumn(descText, 1);
            _shortcutGrid.Children.Add(keyBtn);
            _shortcutGrid.Children.Add(descText);
        }
    }

    private void OnShortcutBtnClick(object sender, RoutedEventArgs e)
    {
        if (_isCapturingKey) return; // already capturing another key

        var btn = (Button)sender;
        var shortcutId = btn.Tag as string;
        if (shortcutId == null) return;

        _isCapturingKey = true;
        _captureTargetId = shortcutId;
        _captureTargetBtn = btn;
        btn.Content = "按下新按键...";
        btn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x55, 0xFF, 0xD7, 0x00));
        btn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xD7, 0x00));
        this.Focus(FocusState.Programmatic);
    }

    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isCapturingKey || _captureTargetId == null || _captureTargetBtn == null) return;

        e.Handled = true; // consume the key event

        uint vk = (uint)e.Key;
        bool ctrl = (GetKeyState(0x11) & 0x8000) != 0;
        bool alt = (GetKeyState(0x12) & 0x8000) != 0;
        bool shift = (GetKeyState(0x10) & 0x8000) != 0;

        // Ignore standalone modifier keys and system reserved keys
        if (GlobalHotkeyManager.IsPureModifier(vk) || GlobalHotkeyManager.IsSystemReserved(vk)) return;

        // Find and update the shortcut entry
        var entry = SettingsManager.Data.Shortcuts.Find(s => s.Id == _captureTargetId);
        if (entry != null)
        {
            // Unregister old hotkey
            App.UnregisterGlobalHotkey(entry.Id);

            // Update entry
            entry.VirtualKey = vk;
            entry.Ctrl = ctrl;
            entry.Alt = alt;
            entry.Shift = shift;

            // Register new hotkey
            var result = App.RegisterGlobalHotkey(entry);
            if (result != null && !result.Success && result.ConflictKey != null)
            {
                _ = ShowConflictDialog(entry, result.ConflictKey);
            }

            SettingsManager.Save();
        }

        // Reset capture state
        _isCapturingKey = false;
        _captureTargetId = null;
        _captureTargetBtn.Content = entry?.DisplayString ?? "???";
        _captureTargetBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, 0x8A, 0xB4, 0xF8));
        _captureTargetBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8));
        _captureTargetBtn = null;

        // Rebuild all rows to keep them in sync
        RebuildShortcutRows();
    }

    private async System.Threading.Tasks.Task ShowConflictDialog(ShortcutEntry entry, string conflictKey)
    {
        var d = new ContentDialog
        {
            Title = "快捷键冲突",
            Content = $"{conflictKey} 已被其他应用占用。快捷键已保存但可能无法正常使用，建议更换键位。",
            CloseButtonText = "知道了",
            XamlRoot = this.Content.XamlRoot,
        };
        await d.ShowAsync();
    }

    private static Border WrapCard(StackPanel content)
    {
        return new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0x14, 0x14, 0x24)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 16, 20, 16),
            Child = content,
        };
    }

    private static StackPanel MakeCard(string title)
    {
        var s = new StackPanel { Spacing = 12 };
        s.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        return s;
    }

    private static StackPanel WrapNumberBoxSetting(string label, NumberBox numBox)
    {
        var wrap = new StackPanel { Spacing = 4 };
        wrap.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.7 });
        wrap.Children.Add(numBox);
        return wrap;
    }

    private static StackPanel WrapComboSetting(string label, ComboBox cmb)
    {
        var wrap = new StackPanel { Spacing = 4 };
        wrap.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.7 });
        wrap.Children.Add(cmb);
        return wrap;
    }

    private void OnFloatingWindowToggled(object sender, RoutedEventArgs e)
        => App.ToggleFloatingWindow(ToggleFloatingWindow.IsOn, null);
}
