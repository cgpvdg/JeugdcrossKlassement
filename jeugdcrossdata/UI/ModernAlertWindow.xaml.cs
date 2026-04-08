using System.Windows;
using System.Windows.Input;

namespace jeugdcrossdata.UI;

public partial class ModernAlertWindow : Window
{
    public ModernAlertWindow(string title, string message, AlertType type)
    {
        InitializeComponent();

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ApplyVisuals(type);
    }

    public static void Show(Window owner, string title, string message, AlertType type)
    {
        var dialog = new ModernAlertWindow(title, message, type)
        {
            Owner = owner
        };

        dialog.ShowDialog();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyVisuals(AlertType type)
    {
        switch (type)
        {
            case AlertType.Success:
                IconGlyphTextBlock.Text = "\uE73E";
                IconGlyphTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
                break;
            case AlertType.Warning:
                IconGlyphTextBlock.Text = "\uE7BA";
                IconGlyphTextBlock.Foreground = System.Windows.Media.Brushes.Khaki;
                break;
            case AlertType.Error:
                IconGlyphTextBlock.Text = "\uEA39";
                IconGlyphTextBlock.Foreground = System.Windows.Media.Brushes.Salmon;
                break;
            default:
                IconGlyphTextBlock.Text = "\uE946";
                IconGlyphTextBlock.Foreground = System.Windows.Media.Brushes.LightSkyBlue;
                break;
        }
    }
}
