using System.Windows;
using System.Windows.Controls;
using Mapa.App.Services;
using Microsoft.Web.WebView2.Core;

namespace Mapa.App.Views;

public partial class OccurrencesPanel : UserControl
{
    private LeafletMapBridge? _mapBridge;
    private CoreWebView2Environment? _environment;
    private string? _webFolder;
    private bool _isMapReady;
    private bool _initStarted;

    private IReadOnlyList<MarkerPayload>? _pendingMarkers;
    private bool _pendingFitBounds;

    public OccurrencesPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Called by MainWindow after the WebView2 environment is ready. If the tab is already
    /// in the visual tree (Loaded fired before this call), we start immediately; otherwise
    /// OnLoaded will pick it up when the tab is first shown.
    /// </summary>
    public void PrepareMap(CoreWebView2Environment environment, string webFolder)
    {
        _environment = environment;
        _webFolder = webFolder;

        if (IsLoaded && !_initStarted)
            _ = InitializeMapAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_environment is null || _webFolder is null || _initStarted)
            return;

        _ = InitializeMapAsync();
    }

    private async Task InitializeMapAsync()
    {
        if (_initStarted) return;
        _initStarted = true;

        try
        {
            _mapBridge = new LeafletMapBridge(MapWebView, _webFolder!);
            await _mapBridge.InitializeAsync(_environment!, "map.html?clustering=false");

            _isMapReady = true;
            MapLoadingOverlay.Visibility = Visibility.Collapsed;

            if (_pendingMarkers is not null)
            {
                _mapBridge.SetMarkers(_pendingMarkers);
                _pendingMarkers = null;
            }

            if (_pendingFitBounds)
            {
                _mapBridge.FitBounds();
                _pendingFitBounds = false;
            }
        }
        catch (Exception ex)
        {
            MapLoadingText.Text = $"Erro ao carregar mapa: {ex.Message}";
            MapLoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    public void SetMarkers(IReadOnlyList<MarkerPayload> markers)
    {
        if (_isMapReady && _mapBridge is not null)
            _mapBridge.SetMarkers(markers);
        else
            _pendingMarkers = markers;
    }

    public void FitBounds()
    {
        if (_isMapReady && _mapBridge is not null)
            _mapBridge.FitBounds();
        else
            _pendingFitBounds = true;
    }
}
