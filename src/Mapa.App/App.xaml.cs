using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

namespace Mapa.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    /// <summary>
    /// Safety net: WPF's default behavior for any unhandled exception on the UI thread is to hard
    /// crash the whole process with no explanation. Show the user what happened instead and keep
    /// the app running whenever it's safe to do so.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}\n\n" +
            "O programa vai tentar continuar aberto. Se o erro se repetir, anote esta mensagem.",
            "Erro inesperado", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;
    }
}
