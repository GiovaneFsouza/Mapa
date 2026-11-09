using System.Windows.Controls;
using System.Windows.Input;

namespace Mapa.App.Views;

public partial class FrequencyCorrelationView : UserControl
{
    public FrequencyCorrelationView()
    {
        InitializeComponent();
    }

    private void SettingBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
    }
}
