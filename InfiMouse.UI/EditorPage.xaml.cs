using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using InfiMouse.Model;
using InfiMouse.Input.Native;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using WinRT.Interop;
using Windows.Storage;

namespace InfiMouse.UI;

public sealed class EditorPage : Page
{
    private readonly MainViewModel _vm = new MainViewModel();
    private readonly PathEditorViewModel _pathVM = new PathEditorViewModel();

    public MainViewModel ViewModel => _vm;

    private double _canvasScale = 1.0;
    private double _canvasOffsetX = 0;
    private double _canvasOffsetY = 0;
    private bool _isDragging;
    private int _dragIndex = -1;
    private System.Drawing.PointF _dragStartPos;

    // Bezier control point dragging
    private bool _isDraggingCp = false;
    private int _dragCpIndex = -1; // 1 or 2

    private bool _isRecording;
    private CancellationTokenSource? _recordCts;
    private readonly HashSet<ushort> _pressedKeys = new();
    private readonly object _keyLock = new();

    private string? _currentFilePath;

    // State persistence — now backed by SettingsManager
    private static double s_sidebarWidth = 300;

    private static bool SidebarCollapsed
    {
        get => SettingsManager.Data.SidebarCollapsed;
        set => SettingsManager.Data.SidebarCollapsed = value;
    }
    private static int SidebarTabIdx
    {
        get => SettingsManager.Data.SidebarTabIndex;
        set => SettingsManager.Data.SidebarTabIndex = value;
    }

    private static readonly SolidColorBrush BlueBrush = new(Microsoft.UI.Colors.DodgerBlue);
    private static readonly SolidColorBrush RedBrush = new(Microsoft.UI.Colors.OrangeRed);
    private static readonly SolidColorBrush GreenBrush = new(Microsoft.UI.Colors.LimeGreen);
    private static readonly SolidColorBrush WhiteBrush = new(Microsoft.UI.Colors.White);
    private static readonly SolidColorBrush YellowBrush = new(Microsoft.UI.Colors.Gold);

    private Canvas PathCanvas = null!;
    private TextBlock TxtCursorPos = null!;
    private TextBlock TxtPathInfo = null!;
    private Slider SliderDuration = null!;
    private NumberBox NumDuration = null!;
    private ComboBox CmbEasing = null!;
    private NumberBox NumFrameCount = null!;
    private ToggleSwitch ToggleRandomize = null!;
    private ToggleSwitch ToggleOvershoot = null!;
    private Slider SliderJitter = null!;
    private NumberBox NumJitter = null!;
    private Slider SliderSpeedVariance = null!;
    private NumberBox NumSpeedVariance = null!;
    private NumberBox NumOvershoot = null!;
    private TextBlock TxtTimelineCount = null!;
    private ListView TimelineListView = null!;
    private ListView LogListView = null!;
    private ToggleSwitch ToggleLoop = null!;
    private ToggleSwitch ToggleHoldLeft = null!;
    private ToggleSwitch ToggleRecord = null!;

    // Sidebar controls
    private ColumnDefinition _sidebarCol = null!;
    private Grid _sidebarGrid = null!;
    private Button _sidebarToggle = null!;
    private TextBlock _sidebarHeader = null!;
    private Grid _sidebarContent = null!;
    private ScrollViewer _paramsScroll = null!;
    private Grid _timelinePanel = null!;

    public EditorPage()
    {
        LoadViewModelDefaults();
        BuildUI();
        this.Loaded += (_, _) =>
        {
            App.CurrentEditor = this;
            RestoreSidebarState();
        };
        this.Unloaded += (_, _) =>
        {
            if (App.CurrentEditor == this) App.CurrentEditor = null;
            SettingsManager.Save();
        };
    }

    private void LoadViewModelDefaults()
    {
        var d = SettingsManager.Data;
        _vm.Animation.TotalDuration = TimeSpan.FromMilliseconds(d.DefaultTotalDurationMs);
        _vm.Animation.FrameCount = d.DefaultFrameCount;
        _vm.Animation.Easing = (EasingType)d.DefaultEasingIndex;
        _vm.Animation.EnableRandomization = d.DefaultEnableHumanization;
        _vm.AntiDetection.JitterStrength = d.DefaultJitterStrength;
        _vm.AntiDetection.SpeedVariancePercent = d.DefaultSpeedVariance;
        _vm.AntiDetection.EnableOvershoot = d.DefaultEnableOvershoot;
        _vm.AntiDetection.OvershootPixels = d.DefaultOvershootPixels;
    }

    public void ExecuteShortcut(EditorShortcut shortcut)
    {
        switch (shortcut)
        {
            case EditorShortcut.Play:
                SaveCurrentProject();
                _ = _vm.PlayCommand.ExecuteAsync(null);
                break;
            case EditorShortcut.Pause:
                _vm.PausePlayCommand.Execute(null);
                break;
            case EditorShortcut.Stop:
                _vm.StopPlayCommand.Execute(null);
                break;
            case EditorShortcut.New:
                _pathVM.ClearAllCommand.Execute(null);
                _vm.ClearProjectCommand.Execute(null);
                DrawAllPathPoints();
                UpdateTimelineCount();
                UpdatePathInfo();
                break;
            case EditorShortcut.Open:
                _ = OpenFileAsync();
                break;
            case EditorShortcut.Save:
                _ = SaveOrSaveAsAsync();
                break;
        }
    }

    private async Task OpenFileAsync()
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(App.MainWindowInstance!);
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            picker.FileTypeFilter.Add(".infimouse");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _currentFilePath = file.Path;
                await _vm.LoadProjectCommand.ExecuteAsync(file.Path);
                SyncViewModelToUI();
                DrawAllPathPoints();
                UpdateTimelineCount();
                UpdatePathInfo();
            }
        }
        catch (Exception ex) { _vm.AddLog($"Open failed: {ex.Message}"); }
    }

    private async Task SaveOrSaveAsAsync()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            await SaveAsAsync();
            return;
        }
        SaveCurrentProject();
        await _vm.SaveProjectCommand.ExecuteAsync(_currentFilePath);
    }

    private async Task SaveAsAsync()
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(App.MainWindowInstance!);
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            picker.FileTypeChoices.Add("InfiMouse Project", new List<string> { ".infimouse" });
            picker.SuggestedFileName = "project.infiMouse";
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                _currentFilePath = file.Path;
                SaveCurrentProject();
                await _vm.SaveProjectCommand.ExecuteAsync(_currentFilePath);
            }
        }
        catch (Exception ex) { _vm.AddLog($"Save As failed: {ex.Message}"); }
    }

    private void SaveCurrentProject()
    {
        _vm.CurrentProject.PathPoints = new List<PathPoint>(_pathVM.PathPoints);
        _vm.CurrentProject.Events = new List<TimedEvent>(_vm.TimelineEvents);
    }

    // ========== UI Construction ==========

    private void BuildUI()
    {
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Row 0: Toolbar
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Canvas + Sidebar
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) }); // Row 2: Log
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Col 0: Canvas
        _sidebarCol = new ColumnDefinition { Width = new GridLength(s_sidebarWidth) }; // Col 1: Sidebar
        mainGrid.ColumnDefinitions.Add(_sidebarCol);

        // ── Region 0: Toolbar (Row 0, spans 2 cols) ──
        var toolbar = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x10, 0x14, 0x14, 0x24)),
            Padding = new Thickness(8, 4, 8, 4),
            Height = 40,
        };
        var toolbarStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

        var btnPlay = MakeToolButton("▶ 播放", () => { SaveCurrentProject(); _ = _vm.PlayCommand.ExecuteAsync(null); });
        var btnPause = MakeToolButton("⏸ 暂停", () => _vm.PausePlayCommand.Execute(null));
        var btnStop = MakeToolButton("■ 停止", () => _vm.StopPlayCommand.Execute(null));
        toolbarStack.Children.Add(btnPlay);
        toolbarStack.Children.Add(btnPause);
        toolbarStack.Children.Add(btnStop);
        toolbarStack.Children.Add(new Border { Width = 1, Height = 20, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(4, 0, 4, 0) });

        var btnClear = MakeToolButton("\u2716 清空", () =>
        {
            _pathVM.ClearAllCommand.Execute(null);
            _vm.ClearProjectCommand.Execute(null);
            _vm.CurrentProject.ControlPoint1 = null;
            _vm.CurrentProject.ControlPoint2 = null;
            DrawAllPathPoints(); UpdateTimelineCount(); UpdatePathInfo();
        });
        toolbarStack.Children.Add(btnClear);
        toolbarStack.Children.Add(new Border { Width = 1, Height = 20, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(4, 0, 4, 0) });

        var btnNew = MakeToolButton("📄 新建", () =>
        {
            _pathVM.ClearAllCommand.Execute(null);
            _vm.ClearProjectCommand.Execute(null);
            DrawAllPathPoints(); UpdateTimelineCount(); UpdatePathInfo();
        });
        var btnOpen = MakeToolButton("📂 打开", () => _ = OpenFileAsync());
        var btnSave = MakeToolButton("💾 保存", () => _ = SaveOrSaveAsAsync());
        btnNew.Visibility = Visibility.Collapsed;
        btnOpen.Visibility = Visibility.Collapsed;
        btnSave.Visibility = Visibility.Collapsed;
        toolbarStack.Children.Add(btnNew);
        toolbarStack.Children.Add(btnOpen);
        toolbarStack.Children.Add(btnSave);

        toolbar.Child = toolbarStack;
        Grid.SetRow(toolbar, 0); Grid.SetColumn(toolbar, 0);
        Grid.SetColumnSpan(toolbar, 2);
        mainGrid.Children.Add(toolbar);

        // ── Region 1: Canvas (Row 1, Col 0) ──
        PathCanvas = new Canvas
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x08, 0x0D, 0x0D, 0x1A)),
        };
        PathCanvas.PointerPressed += OnCanvasPointerPressed;
        PathCanvas.PointerMoved += OnCanvasPointerMoved;
        PathCanvas.PointerReleased += OnCanvasPointerReleased;
        PathCanvas.RightTapped += OnCanvasRightTapped;
        PathCanvas.PointerWheelChanged += OnCanvasWheelChanged;
        PathCanvas.Loaded += (_, _) => DrawAllPathPoints();
        Grid.SetRow(PathCanvas, 1); Grid.SetColumn(PathCanvas, 0);
        mainGrid.Children.Add(PathCanvas);

        // Cursor position overlay
        TxtCursorPos = new TextBlock
        {
            Text = "", FontSize = 11,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
            Margin = new Thickness(8, 4, 0, 0),
        };
        Canvas.SetLeft(TxtCursorPos, 8); Canvas.SetTop(TxtCursorPos, 4);
        PathCanvas.Children.Add(TxtCursorPos);

        // Fit-to-window (100%) button — top-right of Canvas
        var btnFit = new Button
        {
            Content = "100%",
            Width = 42, Height = 24,
            FontSize = 10,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x44, 0x8A, 0xB4, 0xF8)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 8, 0),
        };
        btnFit.Click += (_, _) => FitToWindow();
        Canvas.SetLeft(btnFit, PathCanvas.ActualWidth - 50); Canvas.SetTop(btnFit, 4);
        PathCanvas.Children.Add(btnFit);

        // Reposition fit button when canvas resizes
        PathCanvas.SizeChanged += (_, _) =>
        {
            Canvas.SetLeft(btnFit, Math.Max(0, PathCanvas.ActualWidth - 50));
        };

        // Path info bottom bar
        TxtPathInfo = new TextBlock
        {
            Text = "Click canvas to add path points", FontSize = 12, Opacity = 0.7, Foreground = WhiteBrush,
        };
        var pathInfoBorder = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0x0D, 0x0D, 0x1A)),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Bottom, Child = TxtPathInfo,
        };
        Grid.SetRow(pathInfoBorder, 1); Grid.SetColumn(pathInfoBorder, 0);
        mainGrid.Children.Add(pathInfoBorder);

        // ── Sidebar collapse toggle button (visible when collapsed) ──
        _sidebarToggle = new Button
        {
            Content = "\u25C0",
            Width = 18, Height = 36,
            FontSize = 10,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0x14, 0x14, 0x24)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(0, 4, 4, 0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        _sidebarToggle.Click += (_, _) => ToggleSidebar();
        Grid.SetRow(_sidebarToggle, 1); Grid.SetColumn(_sidebarToggle, 0);
        mainGrid.Children.Add(_sidebarToggle);

        // ── Region 2: Sidebar (Row 1, Col 1) ──
        _sidebarGrid = new Grid();
        _sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        _sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Tab bar
        _sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

        // Sidebar header
        var sidebarHeader = new Grid { Padding = new Thickness(12, 8, 8, 4) };
        sidebarHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sidebarHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _sidebarHeader = new TextBlock
        {
            Text = "参数面板",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_sidebarHeader, 0);
        sidebarHeader.Children.Add(_sidebarHeader);

        var btnCollapse = new Button
        {
            Content = "\u25B6",
            Width = 22, Height = 22,
            FontSize = 9,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(0),
        };
        btnCollapse.Click += (_, _) => ToggleSidebar();
        Grid.SetColumn(btnCollapse, 1);
        sidebarHeader.Children.Add(btnCollapse);

        Grid.SetRow(sidebarHeader, 0);
        _sidebarGrid.Children.Add(sidebarHeader);

        // Tab bar
        var tabBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Margin = new Thickness(12, 0, 12, 0) };
        var tabParams = MakeTabButton("参数", 0);
        var tabTimeline = MakeTabButton("时间轴", 1);
        tabBar.Children.Add(tabParams);
        tabBar.Children.Add(tabTimeline);
        Grid.SetRow(tabBar, 1);
        _sidebarGrid.Children.Add(tabBar);

        // Content area (switched by tab)
        _sidebarContent = new Grid();
        Grid.SetRow(_sidebarContent, 2);
        _sidebarGrid.Children.Add(_sidebarContent);

        BuildParamsPanel();
        BuildTimelinePanel();

        // Show initial tab
        SwitchSidebarTab(SidebarTabIdx);

        Grid.SetRow(_sidebarGrid, 1); Grid.SetColumn(_sidebarGrid, 1);
        Grid.SetRowSpan(_sidebarGrid, 2);
        mainGrid.Children.Add(_sidebarGrid);

        // ── Region 3: Log (Row 2, Col 0 only) ──
        var logContainer = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x10, 0x0A, 0x0A, 0x14)),
        };
        var logPanel = new Grid();
        logPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        logPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var logHeader = new TextBlock
        {
            Text = "日志",
            FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.7, Margin = new Thickness(8, 4, 8, 2),
        };
        Grid.SetRow(logHeader, 0);
        logPanel.Children.Add(logHeader);

        LogListView = new ListView
        {
            ItemsSource = _vm.LogEntries, FontSize = 11,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Padding = new Thickness(4),
        };
        Grid.SetRow(LogListView, 1);
        logPanel.Children.Add(LogListView);

        logContainer.Child = logPanel;
        Grid.SetRow(logContainer, 2); Grid.SetColumn(logContainer, 0);
        mainGrid.Children.Add(logContainer);

        this.Content = mainGrid;
    }

    private void BuildParamsPanel()
    {
        _paramsScroll = new ScrollViewer { Padding = new Thickness(14, 8, 14, 12), Visibility = Visibility.Collapsed };
        var paramsStack = new StackPanel { Spacing = 12 };

        var onSlider = (RangeBaseValueChangedEventHandler)((s, e) => SyncUItoViewModel());
        var onNum = (Action<NumberBox, NumberBoxValueChangedEventArgs>)((s, e) => SyncUItoViewModel());

        // Animation
        paramsStack.Children.Add(new TextBlock
        {
            Text = "动画参数", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8)),
        });
        paramsStack.Children.Add(MakeLabeledSlider("总时长 (ms)", 200, 10000,
            _vm.Animation.TotalDuration.TotalMilliseconds, 100,
            out SliderDuration, out NumDuration, onSlider, onNum));
        paramsStack.Children.Add(MakeEasingCombo());
        paramsStack.Children.Add(MakeLabeledNumberOnly("帧数",
            _vm.Animation.FrameCount, 10, 200, out NumFrameCount, onNum));
        paramsStack.Children.Add(MakeLabeledNumberOnly("超调像素",
            _vm.AntiDetection.OvershootPixels, 0, 100, out NumOvershoot, onNum));

        // Advanced
        paramsStack.Children.Add(new TextBlock
        {
            Text = "高级选项", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8)),
            Margin = new Thickness(0, 4, 0, 0),
        });
        ToggleRandomize = new ToggleSwitch { Header = "启用随机化", IsOn = _vm.Animation.EnableRandomization, FontSize = 12 };
        ToggleRandomize.Toggled += (s, e) => SyncUItoViewModel();
        paramsStack.Children.Add(ToggleRandomize);
        ToggleOvershoot = new ToggleSwitch { Header = "启用超调", IsOn = _vm.AntiDetection.EnableOvershoot, FontSize = 12 };
        ToggleOvershoot.Toggled += (s, e) => SyncUItoViewModel();
        paramsStack.Children.Add(ToggleOvershoot);
        ToggleHoldLeft = new ToggleSwitch { Header = "按住左键", IsOn = _vm.HoldLeftButton, FontSize = 12 };
        ToggleHoldLeft.Toggled += (s, e) => SyncUItoViewModel();
        paramsStack.Children.Add(ToggleHoldLeft);
        ToggleLoop = new ToggleSwitch { Header = "循环播放", IsOn = _vm.IsLooping, FontSize = 12 };
        ToggleLoop.Toggled += (s, e) => _vm.ToggleLoopCommand.Execute(null);
        paramsStack.Children.Add(ToggleLoop);

        // Anti-detection
        paramsStack.Children.Add(new TextBlock
        {
            Text = "反检测", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8)),
            Margin = new Thickness(0, 4, 0, 0),
        });
        paramsStack.Children.Add(MakeLabeledSlider("抖动强度 (px)", 0, 10,
            _vm.AntiDetection.JitterStrength, 0.5,
            out SliderJitter, out NumJitter, onSlider, onNum));
        paramsStack.Children.Add(MakeLabeledSlider("速度波动 (%)", 0, 30,
            _vm.AntiDetection.SpeedVariancePercent, 1,
            out SliderSpeedVariance, out NumSpeedVariance, onSlider, onNum));

        _paramsScroll.Content = paramsStack;
        _sidebarContent.Children.Add(_paramsScroll);
    }

    private void BuildTimelinePanel()
    {
        _timelinePanel = new Grid { Visibility = Visibility.Collapsed };
        _timelinePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _timelinePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var timelineHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(8, 4, 8, 4),
        };
        ToggleRecord = new ToggleSwitch { Header = "录制键盘", FontSize = 11 };
        ToggleRecord.Toggled += OnRecordToggled;
        timelineHeader.Children.Add(ToggleRecord);
        var btnAddKey = new Button { Content = "+ Key", FontSize = 11, Padding = new Thickness(8, 2, 8, 2) };
        btnAddKey.Click += (_, _) => AddDefaultKeyEvent();
        timelineHeader.Children.Add(btnAddKey);
        TxtTimelineCount = new TextBlock { Text = "(0 events)", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        timelineHeader.Children.Add(TxtTimelineCount);
        Grid.SetRow(timelineHeader, 0);
        _timelinePanel.Children.Add(timelineHeader);

        TimelineListView = new ListView
        {
            ItemsSource = _vm.TimelineEvents,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };
        TimelineListView.ContainerContentChanging += OnTimelineItemContentChanging;
        Grid.SetRow(TimelineListView, 1);
        _timelinePanel.Children.Add(TimelineListView);

        _sidebarContent.Children.Add(_timelinePanel);
    }

    private Button MakeTabButton(string text, int index)
    {
        var btn = new Button
        {
            Content = text, FontSize = 11,
            Padding = new Thickness(12, 5, 12, 5),
            CornerRadius = new CornerRadius(4, 4, 0, 0),
            BorderThickness = new Thickness(0),
        };
        UpdateTabStyle(btn, index == SidebarTabIdx);
        btn.Click += (_, _) => SwitchSidebarTab(index);
        return btn;
    }

    private void UpdateTabStyle(Button btn, bool active)
    {
        if (active)
        {
            btn.Background = new AcrylicBrush
            {
                TintColor = Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x1E, 0x32),
                TintOpacity = 0.85,
                FallbackColor = Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x1E, 0x32),
            };
            btn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8));
        }
        else
        {
            btn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            btn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
        }
    }

    private void SwitchSidebarTab(int index)
    {
        SidebarTabIdx = index;
        if (index == 0)
        {
            _paramsScroll.Visibility = Visibility.Visible;
            _timelinePanel.Visibility = Visibility.Collapsed;
            _sidebarHeader.Text = "参数面板";
        }
        else
        {
            _paramsScroll.Visibility = Visibility.Collapsed;
            _timelinePanel.Visibility = Visibility.Visible;
            _sidebarHeader.Text = "时间轴";
        }
        // Rebuild tab bar buttons to update active state
        var tabBar = _sidebarGrid.Children.OfType<StackPanel>().FirstOrDefault();
        if (tabBar != null && tabBar.Children.Count >= 2)
        {
            UpdateTabStyle((Button)tabBar.Children[0], index == 0);
            UpdateTabStyle((Button)tabBar.Children[1], index == 1);
        }
    }

    private void ToggleSidebar()
    {
        SidebarCollapsed = !SidebarCollapsed;
        ApplySidebarState();
        SettingsManager.Save();
    }

    private void ApplySidebarState()
    {
        if (SidebarCollapsed)
        {
            _sidebarCol.Width = new GridLength(0);
            _sidebarGrid.Visibility = Visibility.Collapsed;
            _sidebarToggle.Visibility = Visibility.Visible;
        }
        else
        {
            _sidebarCol.Width = new GridLength(s_sidebarWidth);
            _sidebarGrid.Visibility = Visibility.Visible;
            _sidebarToggle.Visibility = Visibility.Collapsed;
        }
    }

    private void RestoreSidebarState()
    {
        ApplySidebarState();
        SwitchSidebarTab(SidebarTabIdx);
    }

    // ========== Tutorial ==========

    public async System.Threading.Tasks.Task ShowTutorialAsync()
    {
        if (SettingsManager.Data.TutorialCompleted) return;

        var overlay = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xCC, 0x00, 0x00, 0x00)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = true,
        };

        var steps = new (string title, string body)[]
        {
            ("工具栏", "顶部按钮栏：播放/暂停/停止控制动画，新建/打开/保存管理项目文件。\n快捷键：Ctrl+Enter 播放 | Ctrl+P 暂停 | Ctrl+S 停止"),
            ("Canvas 画布", "点击画布添加路径点，拖拽移动已有节点，右键删除节点。\n滚轮缩放，拖拽空白区域平移画布。"),
            ("参数面板", "右侧面板配置动画参数：时长、缓动函数、帧率、超调像素等。\n切换至「时间轴」标签管理键盘事件序列。"),
            ("时间轴", "时间轴标签页：管理同步键盘按键事件。\n录制模式可实时捕获按键，手动添加 +Key 插入默认事件。"),
            ("日志", "底部日志面板：实时输出播放状态、项目加载/保存、错误信息等。\n运行过程中随时查看历史记录。"),
        };

        var currentStep = 0;

        var card = new Border
        {
            Background = new AcrylicBrush { TintColor = Windows.UI.Color.FromArgb(0xFF, 0x1A, 0x1A, 0x2E), TintOpacity = 0.95, FallbackColor = Windows.UI.Color.FromArgb(0xFF, 0x1A, 0x1A, 0x2E) },
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24, 20, 24, 20),
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var cardStack = new StackPanel { Spacing = 12 };
        var stepTitle = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8)) };
        var stepBody = new TextBlock { FontSize = 13, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
        var stepCounter = new TextBlock { FontSize = 11, Opacity = 0.5, HorizontalAlignment = HorizontalAlignment.Center };

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8 };

        var btnPrev = new Button { Content = "上一步", FontSize = 12, Padding = new Thickness(14, 6, 14, 6), IsEnabled = false };
        var btnNext = new Button { Content = "下一步", FontSize = 12, Padding = new Thickness(14, 6, 14, 6) };
        var btnSkip = new Button { Content = "跳过引导", FontSize = 12, Padding = new Thickness(14, 6, 14, 6), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)) };

        void RefreshStep()
        {
            stepTitle.Text = steps[currentStep].title;
            stepBody.Text = steps[currentStep].body;
            stepCounter.Text = $"{currentStep + 1} / {steps.Length}";
            btnPrev.IsEnabled = currentStep > 0;
            btnNext.Content = currentStep < steps.Length - 1 ? "下一步" : "完成";
        }

        btnPrev.Click += (_, _) => { if (currentStep > 0) { currentStep--; RefreshStep(); } };
        btnNext.Click += async (_, _) =>
        {
            if (currentStep < steps.Length - 1) { currentStep++; RefreshStep(); }
            else
            {
                ((Grid)this.Content).Children.Remove(overlay);
                SettingsManager.Data.TutorialCompleted = true;
                SettingsManager.Save();
                await System.Threading.Tasks.Task.CompletedTask;
            }
        };
        btnSkip.Click += async (_, _) =>
        {
            ((Grid)this.Content).Children.Remove(overlay);
            SettingsManager.Data.TutorialCompleted = true;
            SettingsManager.Save();
            await System.Threading.Tasks.Task.CompletedTask;
        };

        btnRow.Children.Add(btnPrev);
        btnRow.Children.Add(btnNext);
        btnRow.Children.Add(btnSkip);
        cardStack.Children.Add(stepTitle);
        cardStack.Children.Add(stepBody);
        cardStack.Children.Add(stepCounter);
        cardStack.Children.Add(btnRow);
        card.Child = cardStack;
        overlay.Child = card;

        RefreshStep();

        // Add overlay to root grid
        if (this.Content is Grid rootGrid)
        {
            Grid.SetRowSpan(overlay, 100);
            Grid.SetColumnSpan(overlay, 100);
            overlay.SetValue(Grid.RowProperty, 0);
            overlay.SetValue(Grid.ColumnProperty, 0);
            rootGrid.Children.Add(overlay);
        }

        await System.Threading.Tasks.Task.CompletedTask;
    }

    // ========== Helper Methods ==========

    private async void SaveAsWrapper() { SaveCurrentProject(); await SaveAsAsync(); }

    private static Button MakeToolButton(string text, Action handler)
    {
        var btn = new Button { Content = text, Padding = new Thickness(10, 6, 10, 6) };
        btn.Click += (_, _) => handler();
        return btn;
    }

    private static StackPanel MakeLabeledSlider(string label, double min, double max, double val, double tick,
        out Slider slider, out NumberBox numBox,
        RangeBaseValueChangedEventHandler sliderHandler,
        Action<NumberBox, NumberBoxValueChangedEventArgs> numHandler)
    {
        var wrap = new StackPanel { Spacing = 4 };
        wrap.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.7 });

        slider = new Slider { Minimum = min, Maximum = max, Value = val, TickFrequency = tick };
        slider.ValueChanged += sliderHandler;
        wrap.Children.Add(slider);

        numBox = new NumberBox { Value = val, Minimum = min, Maximum = max };
        numBox.ValueChanged += (s, e) => numHandler(s, e);
        wrap.Children.Add(numBox);

        return wrap;
    }

    private static StackPanel MakeLabeledNumberOnly(string label, double val, double min, double max,
        out NumberBox numBox, Action<NumberBox, NumberBoxValueChangedEventArgs> handler)
    {
        var wrap = new StackPanel { Spacing = 4 };
        wrap.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.7 });
        numBox = new NumberBox { Value = val, Minimum = min, Maximum = max };
        numBox.ValueChanged += (s, e) => handler(s, e);
        wrap.Children.Add(numBox);
        return wrap;
    }

    private StackPanel MakeEasingCombo()
    {
        var wrap = new StackPanel { Spacing = 4 };
        wrap.Children.Add(new TextBlock { Text = "Easing Function", FontSize = 12, Opacity = 0.7 });
        CmbEasing = new ComboBox { SelectedIndex = 2 };
        CmbEasing.Items.Add(new ComboBoxItem { Content = "Linear", Tag = "0" });
        CmbEasing.Items.Add(new ComboBoxItem { Content = "EaseOut Quadratic", Tag = "1" });
        CmbEasing.Items.Add(new ComboBoxItem { Content = "EaseOut Cubic", Tag = "2" });
        CmbEasing.Items.Add(new ComboBoxItem { Content = "EaseOut Sine", Tag = "3" });
        CmbEasing.Items.Add(new ComboBoxItem { Content = "EaseOut Circular", Tag = "4" });
        CmbEasing.SelectionChanged += (s, e) =>
        {
            if (CmbEasing.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int idx))
            {
                _vm.Animation.Easing = (EasingType)idx;
                DrawAllPathPoints();
            }
        };
        wrap.Children.Add(CmbEasing);
        return wrap;
    }

    private static SolidColorBrush ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, r, g, b));
    }

    private void OnTimelineItemContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not TimedEvent evt) return;
        if (args.ItemContainer.Content is not null) return;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Padding = new Thickness(4, 2, 4, 2);

        var tsText = new TextBlock { Text = $"{evt.TimestampMs}ms", FontFamily = new FontFamily("Consolas"), FontSize = 11, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(tsText, 0);
        grid.Children.Add(tsText);

        var typeText = new TextBlock { Text = TimelineViewModel.GetEventSummary(evt), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = ParseColor("#8AB4F8") };
        Grid.SetColumn(typeText, 1);
        grid.Children.Add(typeText);

        var delBtn = new Button { Content = "X", FontSize = 10, Padding = new Thickness(4, 0, 4, 0) };
        delBtn.Click += (_, _) =>
        {
            _vm.TimelineEvents.Remove(evt);
            UpdateTimelineCount();
            _vm.AddLog($"Removed event: t={evt.TimestampMs}ms");
        };
        Grid.SetColumn(delBtn, 2);
        grid.Children.Add(delBtn);

        args.ItemContainer.Content = grid;
    }

    private void AddDefaultKeyEvent()
    {
        int baseMs = _vm.TimelineEvents.Count > 0
            ? _vm.TimelineEvents[^1].TimestampMs + 200 : 500;
        var vk = (ushort)(baseMs % 4 == 0 ? VirtualKeyCodes.VK_W :
                           baseMs % 4 == 1 ? VirtualKeyCodes.VK_A :
                           baseMs % 4 == 2 ? VirtualKeyCodes.VK_S : VirtualKeyCodes.VK_D);
        _vm.AddKeyEvent(vk, true, baseMs);
        _vm.AddKeyEvent(vk, false, baseMs + 200);
        UpdateTimelineCount();
    }

    // ========== Keyboard Recording ==========

    private void OnRecordToggled(object sender, RoutedEventArgs e)
    {
        _isRecording = ToggleRecord.IsOn;
        if (_isRecording)
        {
            _vm.AddLog("Key recording started - press any key to record");
            _recordCts = new CancellationTokenSource();
            _ = Task.Run(() => PollKeysLoop(_recordCts.Token));
        }
        else
        {
            _recordCts?.Cancel();
            _pressedKeys.Clear();
            _vm.AddLog("Key recording stopped");
        }
    }

    private async Task PollKeysLoop(CancellationToken ct)
    {
        var recordStartMs = Environment.TickCount;
        ushort[] watchKeys = { VirtualKeyCodes.VK_W, VirtualKeyCodes.VK_A, VirtualKeyCodes.VK_S, VirtualKeyCodes.VK_D,
            VirtualKeyCodes.VK_UP, VirtualKeyCodes.VK_DOWN, VirtualKeyCodes.VK_LEFT, VirtualKeyCodes.VK_RIGHT,
            VirtualKeyCodes.VK_SPACE, VirtualKeyCodes.VK_SHIFT, VirtualKeyCodes.VK_CONTROL, VirtualKeyCodes.VK_MENU };

        while (!ct.IsCancellationRequested)
        {
            foreach (var vk in watchKeys)
            {
                short state = GetAsyncKeyState(vk);
                bool isDown = (state & 0x8000) != 0;
                bool wasDown;
                lock (_keyLock) { wasDown = _pressedKeys.Contains(vk); }
                if (isDown && !wasDown) { lock (_keyLock) { _pressedKeys.Add(vk); } int ts = Environment.TickCount - recordStartMs; DispatcherQueue.TryEnqueue(() => _vm.AddKeyEvent(vk, true, ts)); }
                else if (!isDown && wasDown) { lock (_keyLock) { _pressedKeys.Remove(vk); } int ts = Environment.TickCount - recordStartMs; DispatcherQueue.TryEnqueue(() => _vm.AddKeyEvent(vk, false, ts)); }
            }
            await Task.Delay(10, ct);
        }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // ========== Canvas Interaction ==========

    private void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(PathCanvas);
        var (lx, ly) = CanvasToLogical(pt.Position.X, pt.Position.Y);
        if (pt.Properties.IsLeftButtonPressed)
        {
            // Check control point hit first (for non-linear easing)
            if (_vm.Animation.Easing != EasingType.Linear && _pathVM.PathPoints.Count >= 2)
            {
                var (cp1, cp2) = GetControlPoints();
                if (HitTestControlPoint(lx, ly, cp1))
                {
                    _isDraggingCp = true; _dragCpIndex = 1;
                    return;
                }
                if (HitTestControlPoint(lx, ly, cp2))
                {
                    _isDraggingCp = true; _dragCpIndex = 2;
                    return;
                }
            }

            int hitIndex = HitTestPathPoint(lx, ly);
            if (hitIndex >= 0)
            {
                _isDragging = true; _dragIndex = hitIndex;
                _pathVM.SelectedIndex = hitIndex;
                _dragStartPos = new System.Drawing.PointF((float)lx, (float)ly);
                DrawAllPathPoints(); UpdatePathInfo();
                _vm.AddLog($"Selected path point {hitIndex}");
            }
            else
            {
                var newPt = new PathPoint(lx, ly);
                _pathVM.PathPoints.Add(newPt);
                _pathVM.SelectedIndex = _pathVM.PathPoints.Count - 1;
                DrawAllPathPoints(); UpdatePathInfo();
                _vm.AddLog($"Added path point: ({lx:F0}, {ly:F0})");
            }
        }
    }

    private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(PathCanvas);
        TxtCursorPos.Text = $"X: {pt.Position.X:F0}, Y: {pt.Position.Y:F0}";

        if (_isDraggingCp && pt.Properties.IsLeftButtonPressed)
        {
            var (lx, ly) = CanvasToLogical(pt.Position.X, pt.Position.Y);
            if (_dragCpIndex == 1)
                _vm.CurrentProject.ControlPoint1 = new ControlPoint(lx, ly, ControlPointRole.P1);
            else if (_dragCpIndex == 2)
                _vm.CurrentProject.ControlPoint2 = new ControlPoint(lx, ly, ControlPointRole.P2);
            DrawAllPathPoints();
            return;
        }

        if (_isDragging && _dragIndex >= 0 && pt.Properties.IsLeftButtonPressed)
        {
            var (lx, ly) = CanvasToLogical(pt.Position.X, pt.Position.Y);
            var p = _pathVM.PathPoints[_dragIndex];
            p.X = lx; p.Y = ly;
            _pathVM.PathPoints[_dragIndex] = p;
            DrawAllPathPoints(); UpdatePathInfo();
        }
    }

    private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false; _dragIndex = -1;
        _isDraggingCp = false; _dragCpIndex = -1;
    }

    private void OnCanvasRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var pos = e.GetPosition(PathCanvas);
        var (lx, ly) = CanvasToLogical(pos.X, pos.Y);
        int hitIndex = HitTestPathPoint(lx, ly);
        if (hitIndex >= 0)
        {
            _pathVM.PathPoints.RemoveAt(hitIndex);
            if (_pathVM.SelectedIndex == hitIndex) _pathVM.SelectedIndex = -1;
            else if (_pathVM.SelectedIndex > hitIndex) _pathVM.SelectedIndex--;
            DrawAllPathPoints(); UpdatePathInfo();
            _vm.AddLog($"Removed path point {hitIndex}");
        }
    }

    private void OnCanvasWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(PathCanvas).Properties.MouseWheelDelta;
        _canvasScale = Math.Clamp(_canvasScale + delta * 0.001, 0.1, 5.0);
        DrawAllPathPoints();
        _vm.AddLog($"Canvas zoom: {_canvasScale:F1}x");
    }

    // ========== Drawing ==========

    private void DrawAllPathPoints()
    {
        PathCanvas.Children.Clear();
        DrawGrid();
        DrawBezierPreview();
        DrawControlPointHandles();

        for (int i = 0; i < _pathVM.PathPoints.Count; i++)
        {
            var pt = _pathVM.PathPoints[i];
            var (cx, cy) = LogicalToCanvas(pt.X, pt.Y);
            bool isSelected = i == _pathVM.SelectedIndex;
            bool isStart = i == 0;
            bool isEnd = i == _pathVM.PathPoints.Count - 1 && _pathVM.PathPoints.Count >= 2;

            SolidColorBrush fill = isSelected ? RedBrush : isStart ? GreenBrush : isEnd ? YellowBrush : BlueBrush;
            double size = isSelected ? 10 : 8;

            var ellipse = new Ellipse { Width = size, Height = size, Fill = fill, Stroke = WhiteBrush, StrokeThickness = 1 };
            Canvas.SetLeft(ellipse, cx - size / 2); Canvas.SetTop(ellipse, cy - size / 2);
            PathCanvas.Children.Add(ellipse);

            var label = new TextBlock { Text = $"{i}", FontSize = 9, Foreground = WhiteBrush, Opacity = 0.8 };
            Canvas.SetLeft(label, cx + size / 2 + 2); Canvas.SetTop(label, cy - 6);
            PathCanvas.Children.Add(label);
        }
    }

    private void DrawGrid()
    {
        double spacing = 50 * _canvasScale;
        var gridBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.15 };
        for (double x = _canvasOffsetX % spacing; x < PathCanvas.ActualWidth; x += spacing)
            if (x >= 0) PathCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = PathCanvas.ActualHeight, Stroke = gridBrush, StrokeThickness = 0.5 });
        for (double y = _canvasOffsetY % spacing; y < PathCanvas.ActualHeight; y += spacing)
            if (y >= 0) PathCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = PathCanvas.ActualWidth, Y2 = y, Stroke = gridBrush, StrokeThickness = 0.5 });
    }

    private void DrawBezierPreview()
    {
        if (_pathVM.PathPoints.Count < 2) return;

        // Linear easing → draw straight line segments between points
        if (_vm.Animation.Easing == EasingType.Linear || CmbEasing.SelectedIndex == 0)
        {
            var lineBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) { Opacity = 0.6 };
            for (int i = 0; i < _pathVM.PathPoints.Count - 1; i++)
            {
                var p0 = _pathVM.PathPoints[i];
                var p1 = _pathVM.PathPoints[i + 1];
                var (cx0, cy0) = LogicalToCanvas(p0.X, p0.Y);
                var (cx1, cy1) = LogicalToCanvas(p1.X, p1.Y);
                PathCanvas.Children.Add(new Line { X1 = cx0, Y1 = cy0, X2 = cx1, Y2 = cy1, Stroke = lineBrush, StrokeThickness = 2 });
            }
            return;
        }

        // Non-linear easing → draw cubic bezier curve between start and end
        var start = _pathVM.PathPoints[0];
        var end = _pathVM.PathPoints[^1];
        var cp1 = _vm.CurrentProject.ControlPoint1 != null
            ? new System.Drawing.PointF((float)_vm.CurrentProject.ControlPoint1.X, (float)_vm.CurrentProject.ControlPoint1.Y)
            : new System.Drawing.PointF((float)(start.X + (end.X - start.X) * 0.3), (float)start.Y);
        var cp2 = _vm.CurrentProject.ControlPoint2 != null
            ? new System.Drawing.PointF((float)_vm.CurrentProject.ControlPoint2.X, (float)_vm.CurrentProject.ControlPoint2.Y)
            : new System.Drawing.PointF((float)(start.X + (end.X - start.X) * 0.7), (float)end.Y);

        var curve = new InfiMouse.Core.BezierCurve3(
            new System.Drawing.PointF((float)start.X, (float)start.Y), cp1, cp2,
            new System.Drawing.PointF((float)end.X, (float)end.Y));
        var curveBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) { Opacity = 0.6 };
        int samples = 60;
        for (int i = 0; i < samples - 1; i++)
        {
            double t0 = (double)i / samples, t1 = (double)(i + 1) / samples;
            var p0 = curve.Evaluate((float)t0); var p1 = curve.Evaluate((float)t1);
            var (cx0, cy0) = LogicalToCanvas(p0.X, p0.Y); var (cx1, cy1) = LogicalToCanvas(p1.X, p1.Y);
            PathCanvas.Children.Add(new Line { X1 = cx0, Y1 = cy0, X2 = cx1, Y2 = cy1, Stroke = curveBrush, StrokeThickness = 2 });
        }
        DrawControlDot(cp1, "P1"); DrawControlDot(cp2, "P2");
    }

    private void DrawControlDot(System.Drawing.PointF cp, string label)
    {
        var (cx, cy) = LogicalToCanvas(cp.X, cp.Y);
        var rect = new Rectangle { Width = 8, Height = 8, Fill = new SolidColorBrush(Microsoft.UI.Colors.Orange) { Opacity = 0.7 }, Stroke = WhiteBrush, StrokeThickness = 1 };
        Canvas.SetLeft(rect, cx - 4); Canvas.SetTop(rect, cy - 4);
        PathCanvas.Children.Add(rect);
        var txt = new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange), Opacity = 0.7 };
        Canvas.SetLeft(txt, cx + 6); Canvas.SetTop(txt, cy - 6);
        PathCanvas.Children.Add(txt);
    }

    private void DrawControlPointHandles()
    {
        if (_pathVM.PathPoints.Count < 2) return;
        // No control point handles for linear easing
        if (_vm.Animation.Easing == EasingType.Linear || CmbEasing.SelectedIndex == 0) return;
        var (cp1, cp2) = GetControlPoints();
        var dashBrush = new SolidColorBrush(Microsoft.UI.Colors.Orange) { Opacity = 0.3 };
        var (sx, sy) = LogicalToCanvas(_pathVM.PathPoints[0].X, _pathVM.PathPoints[0].Y);
        var (c1x, c1y) = LogicalToCanvas(cp1.X, cp1.Y);
        var (c2x, c2y) = LogicalToCanvas(cp2.X, cp2.Y);
        var (ex, ey) = LogicalToCanvas(_pathVM.PathPoints[^1].X, _pathVM.PathPoints[^1].Y);
        PathCanvas.Children.Add(new Line { X1 = sx, Y1 = sy, X2 = c1x, Y2 = c1y, Stroke = dashBrush, StrokeThickness = 1, StrokeDashArray = { 4, 4 } });
        PathCanvas.Children.Add(new Line { X1 = c2x, Y1 = c2y, X2 = ex, Y2 = ey, Stroke = dashBrush, StrokeThickness = 1, StrokeDashArray = { 4, 4 } });

        // Control point dots (draggable handles)
        DrawControlDot(c1x, c1y, Microsoft.UI.Colors.OrangeRed);
        DrawControlDot(c2x, c2y, Microsoft.UI.Colors.Orange);
    }

    private void DrawControlDot(double cx, double cy, Windows.UI.Color color)
    {
        var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(color) { Opacity = 0.7 }, Stroke = WhiteBrush, StrokeThickness = 1 };
        Canvas.SetLeft(dot, cx - 4); Canvas.SetTop(dot, cy - 4);
        PathCanvas.Children.Add(dot);
    }

    // ========== Coordinate Helpers ==========

    private (double x, double y) LogicalToCanvas(double lx, double ly) => (lx * _canvasScale + _canvasOffsetX, ly * _canvasScale + _canvasOffsetY);
    private (double x, double y) CanvasToLogical(double cx, double cy) => ((cx - _canvasOffsetX) / _canvasScale, (cy - _canvasOffsetY) / _canvasScale);

    private int HitTestPathPoint(double lx, double ly, double tolerance = 10)
    {
        double tol = tolerance / _canvasScale;
        for (int i = _pathVM.PathPoints.Count - 1; i >= 0; i--)
        {
            var p = _pathVM.PathPoints[i];
            if ((p.X - lx) * (p.X - lx) + (p.Y - ly) * (p.Y - ly) < tol * tol) return i;
        }
        return -1;
    }

    private (System.Drawing.PointF cp1, System.Drawing.PointF cp2) GetControlPoints()
    {
        var start = _pathVM.PathPoints[0];
        var end = _pathVM.PathPoints[^1];
        var cp1 = _vm.CurrentProject.ControlPoint1 != null
            ? new System.Drawing.PointF((float)_vm.CurrentProject.ControlPoint1.X, (float)_vm.CurrentProject.ControlPoint1.Y)
            : new System.Drawing.PointF((float)(start.X + (end.X - start.X) * 0.3), (float)start.Y);
        var cp2 = _vm.CurrentProject.ControlPoint2 != null
            ? new System.Drawing.PointF((float)_vm.CurrentProject.ControlPoint2.X, (float)_vm.CurrentProject.ControlPoint2.Y)
            : new System.Drawing.PointF((float)(start.X + (end.X - start.X) * 0.7), (float)end.Y);
        return (cp1, cp2);
    }

    private bool HitTestControlPoint(double lx, double ly, System.Drawing.PointF cp, double tolerance = 12)
    {
        double tol = tolerance / _canvasScale;
        return (cp.X - lx) * (cp.X - lx) + (cp.Y - ly) * (cp.Y - ly) < tol * tol;
    }

    /// <summary>
    /// Fit all path points to the canvas viewport (100% zoom).
    /// </summary>
    private void FitToWindow()
    {
        if (_pathVM.PathPoints.Count == 0) return;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var p in _pathVM.PathPoints)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        double w = maxX - minX;
        double h = maxY - minY;
        if (w < 10) w = 10;
        if (h < 10) h = 10;

        double padding = 40;
        double canvasW = PathCanvas.ActualWidth - padding * 2;
        double canvasH = PathCanvas.ActualHeight - padding * 2;

        _canvasScale = Math.Min(canvasW / w, canvasH / h);
        _canvasOffsetX = padding - minX * _canvasScale;
        _canvasOffsetY = padding - minY * _canvasScale;

        DrawAllPathPoints();
        _vm.AddLog($"Fit to window: scale={_canvasScale:F2}x");
    }

    // ========== ViewModel Sync ==========

    private void SyncUItoViewModel()
    {
        _vm.Animation.TotalDuration = TimeSpan.FromMilliseconds(SliderDuration.Value);
        _vm.Animation.FrameCount = (int)NumFrameCount.Value;
        _vm.Animation.EnableRandomization = ToggleRandomize.IsOn;
        _vm.AntiDetection.JitterStrength = SliderJitter.Value;
        _vm.AntiDetection.SpeedVariancePercent = SliderSpeedVariance.Value;
        _vm.AntiDetection.EnableOvershoot = ToggleOvershoot.IsOn;
        _vm.HoldLeftButton = ToggleHoldLeft.IsOn;
    }

    private void SyncViewModelToUI()
    {
        SliderDuration.Value = _vm.Animation.TotalDuration.TotalMilliseconds;
        NumDuration.Value = _vm.Animation.TotalDuration.TotalMilliseconds;
        CmbEasing.SelectedIndex = (int)_vm.Animation.Easing;
        NumFrameCount.Value = _vm.Animation.FrameCount;
        ToggleRandomize.IsOn = _vm.Animation.EnableRandomization;
        SliderJitter.Value = _vm.AntiDetection.JitterStrength;
        NumJitter.Value = _vm.AntiDetection.JitterStrength;
        SliderSpeedVariance.Value = _vm.AntiDetection.SpeedVariancePercent;
        NumSpeedVariance.Value = _vm.AntiDetection.SpeedVariancePercent;
        ToggleOvershoot.IsOn = _vm.AntiDetection.EnableOvershoot;
        NumOvershoot.Value = _vm.AntiDetection.OvershootPixels;
        ToggleHoldLeft.IsOn = _vm.HoldLeftButton;

        _pathVM.PathPoints.Clear();
        foreach (var pt in _vm.CurrentProject.PathPoints)
            _pathVM.PathPoints.Add(new PathPoint(pt.X, pt.Y));
    }

    private void UpdateTimelineCount() => TxtTimelineCount.Text = $"({_vm.TimelineEvents.Count} events)";

    private void UpdatePathInfo()
    {
        TxtPathInfo.Text = _pathVM.PathPoints.Count switch
        {
            0 => "Click canvas to add path points",
            1 => "1 point - please add an end point",
            _ => $"{_pathVM.PathPoints.Count} points - Start({_pathVM.StartPoint!.X:F0},{_pathVM.StartPoint.Y:F0}) End({_pathVM.EndPoint!.X:F0},{_pathVM.EndPoint.Y:F0})",
        };
    }
}
