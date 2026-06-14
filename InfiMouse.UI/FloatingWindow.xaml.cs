using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.Graphics;
using WinRT.Interop;

namespace InfiMouse.UI;

public sealed class FloatingWindow : Window
{
    private EditorPage? _editorPage;

    public FloatingWindow()
    {
        BuildUI();

        // Mica backdrop
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };

        var presenter = this.AppWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        }

        this.AppWindow.Resize(new SizeInt32(140, 200));

        this.Closed += (_, _) =>
        {
            App.ToggleFloatingWindow(false);
            App.MainWindowInstance?.Activate();
        };
    }

    public void SetEditorPage(EditorPage editor) => _editorPage = editor;

    private void BuildUI()
    {
        var rootGrid = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Close button — top-right corner
        var btnClose = new Button
        {
            Content = "\u2715",
            Width = 22, Height = 22,
            FontSize = 10,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x44, 0xE8, 0x11, 0x23)),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 4, 0),
        };
        btnClose.Click += (_, _) =>
        {
            App.ToggleFloatingWindow(false);
            App.MainWindowInstance?.Activate();
        };
        btnClose.PointerEntered += (_, _) =>
            ((SolidColorBrush)btnClose.Background).Color = Windows.UI.Color.FromArgb(0xCC, 0xE8, 0x11, 0x23);
        btnClose.PointerExited += (_, _) =>
            ((SolidColorBrush)btnClose.Background).Color = Windows.UI.Color.FromArgb(0x44, 0xE8, 0x11, 0x23);
        Grid.SetRow(btnClose, 0);
        rootGrid.Children.Add(btnClose);

        // Button panel
        var btnPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 6,
            Margin = new Thickness(8, 0, 8, 8),
        };

        btnPanel.Children.Add(MakeFloatingButton("\u25B6  播放", "#1A73E8", OnPlayClick));
        btnPanel.Children.Add(MakeFloatingButton("\u23F8  暂停", "#555555", OnPauseClick));
        btnPanel.Children.Add(MakeFloatingButton("\u23F9  停止", "#555555", OnStopClick));
        btnPanel.Children.Add(MakeFloatingButton("\u2716  清空", "#555555", OnClearClick));

        Grid.SetRow(btnPanel, 1);
        rootGrid.Children.Add(btnPanel);

        this.Content = rootGrid;
    }

    private static Button MakeFloatingButton(string text, string bgHex, RoutedEventHandler handler)
    {
        byte r = Convert.ToByte(bgHex.Substring(1, 2), 16);
        byte g = Convert.ToByte(bgHex.Substring(3, 2), 16);
        byte b = Convert.ToByte(bgHex.Substring(5, 2), 16);

        var btn = new Button
        {
            Content = text,
            Width = 110, Height = 32,
            FontSize = 11,
            Padding = new Thickness(6, 4, 6, 4),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xCC, r, g, b)),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            CornerRadius = new CornerRadius(6),
        };
        btn.Click += handler;
        return btn;
    }

    private void OnPlayClick(object sender, RoutedEventArgs e)
        => _editorPage?.ExecuteShortcut(EditorShortcut.Play);

    private void OnPauseClick(object sender, RoutedEventArgs e)
        => _editorPage?.ExecuteShortcut(EditorShortcut.Pause);

    private void OnStopClick(object sender, RoutedEventArgs e)
        => _editorPage?.ExecuteShortcut(EditorShortcut.Stop);

    private void OnClearClick(object sender, RoutedEventArgs e)
        => _editorPage?.ExecuteShortcut(EditorShortcut.New);
}
