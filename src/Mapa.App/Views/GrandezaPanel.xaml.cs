using System.Windows.Controls;
using System.Windows.Input;
using Mapa.App.Services;
using Microsoft.Web.WebView2.Core;

namespace Mapa.App.Views;

public partial class GrandezaPanel : UserControl
{
    private LeafletMapBridge? _mapBridge;

    public event EventHandler<string>? MarkerClicked;

    public GrandezaPanel()
    {
        InitializeComponent();
    }

    private void ThresholdBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
    }

    public async Task InitializeMapAsync(CoreWebView2Environment environment, string webFolder)
    {
        _mapBridge = new LeafletMapBridge(MapWebView, webFolder);
        _mapBridge.MarkerClicked += (_, idName) => MarkerClicked?.Invoke(this, idName);
        await _mapBridge.InitializeAsync(environment);
    }

    public void SetMarkers(IReadOnlyList<MarkerPayload> markers)
    {
        _mapBridge?.SetMarkers(markers);
    }

    public void FitBounds()
    {
        _mapBridge?.FitBounds();
    }

    public void SetHeatmapEnabled(bool enabled)
    {
        _mapBridge?.SetHeatmapEnabled(enabled);
    }
}
