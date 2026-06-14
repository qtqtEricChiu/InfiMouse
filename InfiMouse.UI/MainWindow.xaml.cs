using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace InfiMouse.UI;

public enum EditorShortcut { Play, Pause, Stop, New, Open, Save }

public sealed class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private EditorPage? _editorPage;
    private Border LoopIndicator = null!;
    private NavigationView MainNavigationView = null!;
    private NavigationViewItem EditorNavItem = null!;
    private Frame ContentFrame = null!;
    private GlobalHotkeyManager? _hotkeyManager;
    private readonly Dictionary<string, int> _hotkeyIdsByShortcut = new();

    // Id → Shortcut mapping
    private static readonly Dictionary<string, EditorShortcut> IdToShortcut = new()
    {
        ["Play"]  = EditorShortcut.Play,
        ["Pause"] = EditorShortcut.Pause,
        ["Stop"]  = EditorShortcut.Stop,
        ["New"]   = EditorShortcut.New,
        ["Open"]  = EditorShortcut.Open,
        ["Save"]  = EditorShortcut.Save,
    };

    public MainWindow()
    {
        this.Title = "InfiMouse";
        BuildUI();

        TransitionToPage(new EditorPage());
        EditorNavItem.IsSelected = true;

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };

        // WinUI 3 title bar — transparent buttons let Mica show through
        this.ExtendsContentIntoTitleBar = true;
        var titleBar = this.AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);

        this.AppWindow.Resize(new SizeInt32(1400, 900));

        this.Activate();

        // Global hotkeys — register after window is active
        DispatcherQueue.TryEnqueue(() =>
        {
            InitGlobalHotkeys();
        });
    }

    private void InitGlobalHotkeys()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        _hotkeyManager = new GlobalHotkeyManager(hwnd);
        _hotkeyManager.HotkeyPressed += OnGlobalHotkeyPressed;

        var conflicts = new List<string>();

        foreach (var entry in SettingsManager.Data.Shortcuts)
        {
            if (!IdToShortcut.TryGetValue(entry.Id, out var sc)) continue;
            var result = _hotkeyManager.Register(sc, entry.VirtualKey, entry.Ctrl, entry.Alt, entry.Shift);
            if (result.Success)
                _hotkeyIdsByShortcut[entry.Id] = result.Id;
            else if (result.ConflictKey != null)
                conflicts.Add(result.ConflictKey);
        }

        if (conflicts.Count > 0)
        {
            var conflictsStr = string.Join(", ", conflicts);
            DispatcherQueue.TryEnqueue(async () =>
            {
                var d = new ContentDialog
                {
                    Title = "快捷键冲突",
                    Content = $"以下快捷键已被其他应用占用：{conflictsStr}\n建议在设置中更换键位。",
                    CloseButtonText = "知道了",
                    XamlRoot = this.Content.XamlRoot,
                };
                await d.ShowAsync();
            });
        }
    }

    /// <summary>Register a single hotkey from ShortcutEntry. Used by SettingsPage for on-the-fly re-registration.</summary>
    public GlobalHotkeyManager.RegistrationResult? RegisterSingleHotkey(ShortcutEntry entry)
    {
        if (_hotkeyManager == null) return null;
        if (!IdToShortcut.TryGetValue(entry.Id, out var sc)) return null;

        var result = _hotkeyManager.Register(sc, entry.VirtualKey, entry.Ctrl, entry.Alt, entry.Shift);
        if (result.Success)
            _hotkeyIdsByShortcut[entry.Id] = result.Id;

        return result;
    }

    /// <summary>Unregister a single hotkey by shortcut ID. Used by SettingsPage before re-registration.</summary>
    public void UnregisterSingleHotkey(string id)
    {
        if (_hotkeyManager == null) return;
        if (_hotkeyIdsByShortcut.TryGetValue(id, out int hotkeyId))
        {
            _hotkeyManager.Unregister(hotkeyId);
            _hotkeyIdsByShortcut.Remove(id);
        }
    }

    private void OnGlobalHotkeyPressed(EditorShortcut shortcut)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Don't process if a text input is focused
            var focused = FocusManager.GetFocusedElement(this.Content?.XamlRoot);
            if (focused is TextBox || focused is NumberBox || focused is ComboBox || focused is PasswordBox)
                return;
            _editorPage?.ExecuteShortcut(shortcut);
        });
    }

    private void BuildUI()
    {
        // Root Grid
        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Row 0: Loop indicator
        LoopIndicator = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xAA, 0x1A, 0x1A, 0x2E)),
            Padding = new Thickness(8, 4, 8, 4),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new TextBlock
            {
                Text = "\u5FAA\u73AF\u64AD\u653E\u4E2D | \u6309 Escape \u505C\u6B62",
                FontSize = 12,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xD7, 0x00)),
                HorizontalAlignment = HorizontalAlignment.Center,
            }
        };
        Grid.SetRow(LoopIndicator, 0);
        rootGrid.Children.Add(LoopIndicator);

        // Row 1: NavigationView + Frame
        EditorNavItem = new NavigationViewItem
        {
            Icon = new SymbolIcon(Symbol.Edit),
            Content = "\u7F16\u8F91\u5668",
            Tag = "editor",
        };

        ContentFrame = new Frame();

        MainNavigationView = new NavigationView
        {
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = true,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            Content = ContentFrame,
        };
        MainNavigationView.MenuItems.Add(EditorNavItem);
        MainNavigationView.SelectionChanged += OnNavigationSelectionChanged;

        Grid.SetRow(MainNavigationView, 1);
        rootGrid.Children.Add(MainNavigationView);

        // App title bar drag region
        var titleBarGrid = new Grid
        {
            Height = 32,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = true,
        };
        rootGrid.Children.Add(titleBarGrid);
        this.SetTitleBar(titleBarGrid);

        this.Content = rootGrid;

        // KeyDown on the Window itself (via Content)
        rootGrid.KeyDown += OnWindowKeyDown;
    }

    private async void TransitionToPage(Page newPage)
    {
        bool animated = ContentFrame.Content != null;

        if (animated)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource();
            var fadeOut = new DoubleAnimation
            {
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
            };
            Storyboard.SetTarget(fadeOut, ContentFrame);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");
            var sb = new Storyboard();
            sb.Children.Add(fadeOut);
            sb.Completed += (_, _) => tcs.SetResult();
            sb.Begin();
            await tcs.Task;
        }

        // Detach old editor
        if (_editorPage != null)
        {
            _editorPage.ViewModel.LoopIndicatorChanged -= OnLoopIndicatorChanged;
            _editorPage = null;
        }

        ContentFrame.Content = newPage;

        if (newPage is EditorPage editor)
        {
            _editorPage = editor;
            editor.ViewModel.LoopIndicatorChanged += OnLoopIndicatorChanged;
            // Tutorial shown via EditorPage's own overlay system
            DispatcherQueue.TryEnqueue(() => _ = editor.ShowTutorialAsync());
        }

        if (animated)
        {
            var fadeIn = new DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            };
            Storyboard.SetTarget(fadeIn, ContentFrame);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            var sb = new Storyboard();
            sb.Children.Add(fadeIn);
            sb.Begin();
        }
    }

    private void OnLoopIndicatorChanged(object? sender, bool isLooping)
        => LoopIndicator.Visibility = isLooping ? Visibility.Visible : Visibility.Collapsed;

    private void OnWindowKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var focused = FocusManager.GetFocusedElement(this.Content?.XamlRoot);
        if (focused is TextBox || focused is NumberBox || focused is ComboBox || focused is PasswordBox)
            return;

        // Only Space is handled locally (too much conflict risk for global hotkey)
        // All other shortcuts are registered as global hotkeys via GlobalHotkeyManager
        var ctrl = (GetKeyState(0x11) & 0x8000) != 0;
        var shift = (GetKeyState(0x10) & 0x8000) != 0;

        EditorShortcut? shortcut = null;
        if (e.Key == Windows.System.VirtualKey.Space && !ctrl && !shift)
            shortcut = EditorShortcut.Pause;

        if (shortcut.HasValue)
        {
            e.Handled = true;
            _editorPage?.ExecuteShortcut(shortcut.Value);
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected) { TransitionToPage(new SettingsPage()); return; }
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag && tag == "editor")
            TransitionToPage(new EditorPage());
    }

}
