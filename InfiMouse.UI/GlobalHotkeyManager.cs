using System.Runtime.InteropServices;

namespace InfiMouse.UI;

/// <summary>
/// Global hotkey manager using Win32 RegisterHotKey with SetWindowSubclass
/// on the main window. Hotkeys work even when the app is in the background.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_NCDESTROY = 0x0082;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    // Subclass ID — unique per instance to avoid collisions
    private static uint s_subclassIdCounter = 1;
    private readonly uint _subclassId;

    // We need a strong reference to the delegate to prevent GC
    private readonly SUBCLASSPROC _subclassProc;
    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, (EditorShortcut shortcut, uint vk, uint mods)> _hotkeys = new();
    private int _nextId = 1;

    public event Action<EditorShortcut>? HotkeyPressed;

    // ── P/Invoke ──

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll")]
    private static extern uint GetLastError();

    // comctl32 subclassing — safer than replacing WndProc
    [DllImport("comctl32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);

    public GlobalHotkeyManager(IntPtr mainHwnd)
    {
        _hwnd = mainHwnd;
        _subclassId = s_subclassIdCounter++;
        _subclassProc = SubclassProc;

        // Pin the delegate so the function pointer stays valid
        var gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
        SetWindowSubclass(_hwnd, _subclassProc, _subclassId, GCHandle.ToIntPtr(gcHandle));
    }

    private IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeys.TryGetValue(id, out var entry))
                HotkeyPressed?.Invoke(entry.shortcut);
            return IntPtr.Zero;
        }

        if (uMsg == WM_NCDESTROY)
        {
            RemoveWindowSubclass(hWnd, _subclassProc, uIdSubclass);
            if (dwRefData != IntPtr.Zero)
                GCHandle.FromIntPtr(dwRefData).Free();
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public record RegistrationResult(bool Success, string? ConflictKey, int Id = 0);

    /// <summary>
    /// Registers a shortcut as a global hotkey. Returns success state.
    /// If another app already registered the same combination, conflictKey is non-null.
    /// </summary>
    public RegistrationResult Register(EditorShortcut shortcut, uint virtualKey, bool ctrl = false, bool alt = false, bool shift = false)
    {
        uint mods = MOD_NOREPEAT;
        if (ctrl) mods |= MOD_CONTROL;
        if (alt) mods |= MOD_ALT;
        if (shift) mods |= MOD_SHIFT;

        // Pre-validate: pure modifier keys can't be registered as hotkeys
        if (IsPureModifier(virtualKey))
            return new(false, $"Modifier-only key VK_{virtualKey:X2} is not a valid hotkey");

        bool ok = RegisterHotKey(_hwnd, _nextId, mods, virtualKey);
        if (!ok)
        {
            var err = GetLastError();
            if (err == 1409) // ERROR_HOTKEY_ALREADY_REGISTERED
            {
                string modStr = (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "");
                return new(false, $"{modStr}{VkToString(virtualKey)}");
            }
            return new(false, null);
        }

        _hotkeys[_nextId] = (shortcut, virtualKey, mods);
        var thisId = _nextId;
        _nextId++;
        return new(true, null, Id: thisId);
    }

    public void UnregisterAll()
    {
        for (int i = 1; i < _nextId; i++)
            UnregisterHotKey(_hwnd, i);
        _hotkeys.Clear();
    }

    public void Unregister(int id)
    {
        if (_hotkeys.ContainsKey(id))
        {
            UnregisterHotKey(_hwnd, id);
            _hotkeys.Remove(id);
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        // Subclass is removed in WM_NCDESTROY or manually
        RemoveWindowSubclass(_hwnd, _subclassProc, _subclassId);
    }

    /// <summary>Returns true if the virtual key is a pure modifier that can't be used as a standalone hotkey.</summary>
    public static bool IsPureModifier(uint vk)
    {
        return vk switch
        {
            0x10 => true, // VK_SHIFT
            0x11 => true, // VK_CONTROL
            0x12 => true, // VK_MENU (Alt)
            0x5B => true, // VK_LWIN
            0x5C => true, // VK_RWIN
            _ => false,
        };
    }

    /// <summary>Returns true if the key is a system-reserved key that should not be used for hotkeys.</summary>
    public static bool IsSystemReserved(uint vk)
    {
        return vk switch
        {
            0x14 => true, // VK_CAPITAL (CapsLock)
            0x90 => true, // VK_NUMLOCK
            0x91 => true, // VK_SCROLL
            0x2C => true, // VK_SNAPSHOT (PrintScreen)
            _ => false,
        };
    }

    /// <summary>Returns a human-readable name for a virtual key code.</summary>
    public static string VkToString(uint vk)
    {
        return vk switch
        {
            0x0D => "Enter",
            0x20 => "Space",
            0x1B => "Escape",
            0x09 => "Tab",
            0x08 => "Backspace",
            0x2E => "Delete",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
            0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
            0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
            >= 0x30 and <= 0x39 => ((char)('0' + (vk - 0x30))).ToString(),
            >= 0x41 and <= 0x5A => ((char)('A' + (vk - 0x41))).ToString(),
            0x6A => "Num*", 0x6B => "Num+", 0x6D => "Num-",
            0x6E => "Num.", 0x6F => "Num/",
            0x60 => "Num0", 0x61 => "Num1", 0x62 => "Num2", 0x63 => "Num3",
            0x64 => "Num4", 0x65 => "Num5", 0x66 => "Num6", 0x67 => "Num7",
            0x68 => "Num8", 0x69 => "Num9",
            0xBC => ",", 0xBE => ".", 0xBF => "/", 0xBA => ";",
            0xBB => "=", 0xBD => "-", 0xC0 => "`", 0xDB => "[",
            0xDC => "\\", 0xDD => "]", 0xDE => "'",
            _ => $"VK_{vk:X2}",
        };
    }
}
