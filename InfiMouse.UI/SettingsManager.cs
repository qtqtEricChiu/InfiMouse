using System.Runtime.InteropServices;
using System.Text.Json;

namespace InfiMouse.UI;

/// <summary>
/// 持久化管理器：将所有设置项 JSON 序列化到 %APPDATA%\InfiMouse\settings.json，
/// 支持屏幕刷新率检测、快捷键编辑。
/// </summary>
public static class SettingsManager
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InfiMouse");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static AppSettingsData? _data;
    private static readonly object _lock = new();

    public static AppSettingsData Data
    {
        get
        {
            if (_data == null) Load();
            return _data!;
        }
    }

    /// <summary>从文件加载设置，失败则使用默认值。</summary>
    public static void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    _data = JsonSerializer.Deserialize<AppSettingsData>(json) ?? new AppSettingsData();
                }
                else
                {
                    _data = new AppSettingsData();
                }
            }
            catch
            {
                _data = new AppSettingsData();
            }

            // 首次启动：用屏幕刷新率初始化默认帧率
            if (_data.DefaultFrameCount <= 0)
                _data.DefaultFrameCount = GetScreenRefreshRate();
        }
    }

    /// <summary>保存设置到文件。</summary>
    public static void Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(_data ?? new AppSettingsData(), JsonOpts);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }

    /// <summary>获取屏幕刷新率，失败则 fallback 到 60。</summary>
    public static int GetScreenRefreshRate()
    {
        try
        {
            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            if (EnumDisplaySettings(null, -1, ref dm))
                return (int)dm.dmDisplayFrequency;
        }
        catch { }
        return 60;
    }

    // ── P/Invoke for EnumDisplaySettings ──

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}

/// <summary>
/// 持久化设置数据结构。
/// </summary>
public class AppSettingsData
{
    // ── 默认动画参数 ──
    public double DefaultTotalDurationMs { get; set; } = 2000;
    public int DefaultFrameCount { get; set; } // 0 = 未初始化，首次加载时自动检测
    public int DefaultEasingIndex { get; set; } = 2; // EaseOut Cubic
    public double DefaultJitterStrength { get; set; } = 2.0;
    public double DefaultSpeedVariance { get; set; } = 5.0;
    public bool DefaultEnableHumanization { get; set; } = false;
    public bool DefaultEnableOvershoot { get; set; } = false;
    public double DefaultOvershootPixels { get; set; } = 0;

    // ── 通用设置 ──
    public bool AutoLoadLastProject { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool EnableViGEm { get; set; } = false;
    public bool ShowGamepadIndicator { get; set; } = true;

    // ── 快捷键配置 ──
    public List<ShortcutEntry> Shortcuts { get; set; } = new()
    {
        new ShortcutEntry { Id = "Play",        Label = "播放",            VirtualKey = 0x0D, Ctrl = true,  Alt = false, Shift = false },
        new ShortcutEntry { Id = "Pause",       Label = "暂停",            VirtualKey = 0x50, Ctrl = true,  Alt = false, Shift = false },
        new ShortcutEntry { Id = "Stop",        Label = "停止",            VirtualKey = 0x53, Ctrl = true,  Alt = false, Shift = false },
        new ShortcutEntry { Id = "New",         Label = "新建项目",         VirtualKey = 0x4E, Ctrl = true,  Alt = false, Shift = false },
        new ShortcutEntry { Id = "Open",        Label = "打开项目",         VirtualKey = 0x4F, Ctrl = true,  Alt = false, Shift = false },
        new ShortcutEntry { Id = "Save",        Label = "保存项目",         VirtualKey = 0x53, Ctrl = true,  Alt = false, Shift = true },
    };

    // ── 编辑器状态 ──
    public bool SidebarCollapsed { get; set; } = false;
    public int SidebarTabIndex { get; set; } = 0;

    // ── 教程 ──
    public bool TutorialCompleted { get; set; } = false;
}

/// <summary>
/// 单个快捷键条目。
/// </summary>
public class ShortcutEntry
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public uint VirtualKey { get; set; }
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }

    /// <summary>返回可读的快捷键字符串。</summary>
    public string DisplayString
    {
        get
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(VkToString(VirtualKey));
            return string.Join(" + ", parts);
        }
    }

    private static string VkToString(uint vk)
    {
        return vk switch
        {
            0x0D => "Enter",
            0x50 => "P",
            0x53 => "S",
            0x4E => "N",
            0x4F => "O",
            >= 0x30 and <= 0x39 => ((char)('0' + (vk - 0x30))).ToString(),
            >= 0x41 and <= 0x5A => ((char)('A' + (vk - 0x41))).ToString(),
            0x20 => "Space",
            0x1B => "Escape",
            0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
            0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
            0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
            _ => $"VK_{vk:X2}",
        };
    }
}
