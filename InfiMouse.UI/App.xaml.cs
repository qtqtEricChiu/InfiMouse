using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InfiMouse.UI;

public partial class App : Application
{
    private Window? _mainWindow;
    private FloatingWindow? _floatingWindow;

    public static Window? MainWindowInstance { get; private set; }
    public static bool IsFloatingWindowVisible { get; private set; }
    public static EditorPage? CurrentEditor { get; set; }

    /// <summary>Fired when floating window opens or closes (external close by user).</summary>
    public static event Action<bool>? FloatingWindowStateChanged;

    public App()
    {
        this.InitializeComponent();
        SettingsManager.Load();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (!IsAdministrator())
        {
            RequestAdministratorPrivilege();
            return;
        }

        _mainWindow = new MainWindow();
        MainWindowInstance = _mainWindow;
        _mainWindow.Activate();
    }

    public static void ToggleFloatingWindow(bool show, EditorPage? editor = null)
    {
        var app = (App)Current;
        if (show)
        {
            editor ??= CurrentEditor;
            if (app._floatingWindow == null)
            {
                app._floatingWindow = new FloatingWindow();
                app._floatingWindow.Closed += (_, _) =>
                {
                    app._floatingWindow = null;
                    IsFloatingWindowVisible = false;
                    FloatingWindowStateChanged?.Invoke(false);
                };
            }
            if (editor != null)
                app._floatingWindow.SetEditorPage(editor);
            app._floatingWindow.Activate();
            IsFloatingWindowVisible = true;
            FloatingWindowStateChanged?.Invoke(true);
        }
        else
        {
            app._floatingWindow?.Close();
            app._floatingWindow = null;
            IsFloatingWindowVisible = false;
            FloatingWindowStateChanged?.Invoke(false);
        }
    }

    /// <summary>Register a single global hotkey from shortcut entry. Returns result with conflict info.</summary>
    public static GlobalHotkeyManager.RegistrationResult? RegisterGlobalHotkey(ShortcutEntry entry)
    {
        var mainWindow = MainWindowInstance as MainWindow;
        return mainWindow?.RegisterSingleHotkey(entry);
    }

    /// <summary>Unregister a single global hotkey by ID.</summary>
    public static void UnregisterGlobalHotkey(string id)
    {
        var mainWindow = MainWindowInstance as MainWindow;
        mainWindow?.UnregisterSingleHotkey(id);
    }

    private bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private void RequestAdministratorPrivilege()
    {
        var exePath = Environment.ProcessPath;
        if (exePath == null) return;

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas",
        };

        try { System.Diagnostics.Process.Start(startInfo); }
        catch (System.ComponentModel.Win32Exception) { }
        Environment.Exit(0);
    }
}
