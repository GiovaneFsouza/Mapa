using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Mapa.App.Services;
using Mapa.App.ViewModels;
using Mapa.App.Views;
using Mapa.Core.Models;
using Microsoft.Web.WebView2.Core;

namespace Mapa.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.MarkersReady += OnMarkersReady;
        _viewModel.FitBoundsRequested += OnFitBoundsRequested;
        _viewModel.Occurrences.MarkersReady += markers => OccurrencesPanelView.SetMarkers(markers);
        _viewModel.Occurrences.FitBoundsRequested += () => OccurrencesPanelView.FitBounds();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!WebView2RuntimeChecker.IsAvailable() && !await EnsureWebView2RuntimeAsync())
        {
            Application.Current.Shutdown();
            return;
        }

        // Explicit per-user userDataFolder: without this, WebView2 defaults to a folder next to
        // the .exe, which (a) fails if the app is installed somewhere the user can't write to,
        // and (b) leaves browser profile/cache junk sitting inside the app's own install folder.
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MPleno_GeoEventos", "WebView2");
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        var webFolder = Path.Combine(AppContext.BaseDirectory, "Web");

        await VoltagePanelView.InitializeMapAsync(environment, webFolder);

        OccurrencesPanelView.PrepareMap(environment, webFolder);

        // The constructor's initial load already computed state, but no one was listening
        // for MarkersReady/FitBoundsRequested at that point (the maps didn't exist yet).
        _viewModel.RepublishCurrentState();
    }

    /// <summary>Offers to install the bundled WebView2 bootstrapper when the runtime is missing
    /// (expected on some Windows 10 machines; Windows 11 ships it by default).</summary>
    private static async Task<bool> EnsureWebView2RuntimeAsync()
    {
        var confirmed = MessageBox.Show(
            "Este programa precisa do componente \"WebView2 Runtime\" (Microsoft Edge) para exibir o mapa, " +
            "e ele não foi encontrado neste computador.\n\n" +
            "Deseja instalá-lo agora? É necessária conexão com a internet (download rápido, poucos MB).",
            "Componente necessário", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmed != MessageBoxResult.Yes)
            return false;

        var installed = await Task.Run(WebView2RuntimeChecker.TryInstall);

        if (!installed)
        {
            MessageBox.Show(
                "Não foi possível instalar o WebView2 Runtime automaticamente.\n\n" +
                "Baixe manualmente em https://developer.microsoft.com/microsoft-edge/webview2/ e abra o programa novamente.",
                "Não foi possível continuar", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return installed;
    }

    private void OnMarkersReady(EventCategory category, IReadOnlyList<MarkerPayload> markers)
    {
        PanelFor(category).SetMarkers(markers);
    }

    private void OnFitBoundsRequested(EventCategory category)
    {
        PanelFor(category).FitBounds();
    }

    private GrandezaPanel PanelFor(EventCategory category) => VoltagePanelView;
}
