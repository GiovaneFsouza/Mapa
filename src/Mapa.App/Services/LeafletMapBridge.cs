using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Mapa.App.Services;

/// <summary>
/// Wraps a WebView2 control hosting map.html (Leaflet + Leaflet.markercluster). Owns the
/// virtual-host mapping, the C#-&gt;JS marker push, and the JS-&gt;C# marker-click back-channel.
/// </summary>
public sealed class LeafletMapBridge
{
    private readonly WebView2 _webView;
    private readonly string _webFolder;
    private readonly string _hostName;
    private readonly TaskCompletionSource<bool> _readyTcs = new();

    private CoreWebView2? _coreWebView2;

    public event EventHandler<string>? MarkerClicked;

    public LeafletMapBridge(WebView2 webView, string webFolder, string hostName = "mapa.local")
    {
        _webView = webView;
        _webFolder = webFolder;
        _hostName = hostName;
    }

    public async Task InitializeAsync(CoreWebView2Environment environment, string page = "map.html")
    {
        await _webView.EnsureCoreWebView2Async(environment);
        _coreWebView2 = _webView.CoreWebView2;

        _coreWebView2.SetVirtualHostNameToFolderMapping(
            _hostName, _webFolder, CoreWebView2HostResourceAccessKind.Allow);

        _coreWebView2.WebMessageReceived += OnWebMessageReceived;
        _coreWebView2.NavigationCompleted += OnNavigationCompleted;
        _coreWebView2.Navigate($"https://{_hostName}/{page}");

        await _readyTcs.Task;
    }

    public void SetMarkers(IReadOnlyList<MarkerPayload> markers)
    {
        EnsureReady();
        var envelope = new WebMessageEnvelope<SetMarkersPayload>("setMarkers", new SetMarkersPayload(markers));
        _coreWebView2!.PostWebMessageAsJson(JsonSerializer.Serialize(envelope));
    }

    public void SetHeatmapEnabled(bool enabled)
    {
        EnsureReady();
        var envelope = new WebMessageEnvelope<SetHeatmapPayload>("setHeatmap", new SetHeatmapPayload(enabled));
        _coreWebView2!.PostWebMessageAsJson(JsonSerializer.Serialize(envelope));
    }

    public void FitBounds()
    {
        EnsureReady();
        _coreWebView2!.PostWebMessageAsJson(JsonSerializer.Serialize(new FitBoundsEnvelope("fitBounds")));
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
            _readyTcs.TrySetResult(true);
        else
            _readyTcs.TrySetException(new InvalidOperationException($"Falha ao carregar o mapa: {e.WebErrorStatus}"));
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<IncomingWebMessage>(e.WebMessageAsJson);
            if (msg is { Type: "markerClicked", IdName: not null })
                MarkerClicked?.Invoke(this, msg.IdName);
        }
        catch (JsonException)
        {
            // Malformed message from the web content: ignore rather than crash the host.
        }
    }

    private void EnsureReady()
    {
        if (_coreWebView2 is null)
            throw new InvalidOperationException("LeafletMapBridge nao foi inicializado. Chame InitializeAsync primeiro.");
    }
}
