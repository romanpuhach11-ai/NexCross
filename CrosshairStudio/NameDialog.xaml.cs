using System.Windows;
namespace CrosshairStudio;
public partial class NameDialog : Window
{
    public string PresetName => NameBox.Text.Trim();
    public NameDialog() { InitializeComponent(); }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PresetName)) return;
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
