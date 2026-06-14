using Microsoft.UI.Xaml.Controls;

namespace InfiMouse.UI;
public sealed class CodeOnlyPage : Page
{
    public CodeOnlyPage()
    {
        this.Content = new TextBlock { Text = "Code Only Page" };
    }
}
