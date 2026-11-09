using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Web.WebView2.Core;

namespace Mapa.App.Services;

/// <summary>
/// Windows 11 ships the WebView2 Evergreen Runtime by default, but older/locked-down Windows 10
/// machines may not have it. This checks for it at startup and, if missing, can silently run the
/// bundled Microsoft bootstrapper to install it. The bootstrapper is embedded as a resource
/// (rather than shipped as a loose file) because PublishSingleFile was observed to silently drop
/// a loose .exe placed under the output directory.
/// </summary>
public static class WebView2RuntimeChecker
{
    public static bool IsAvailable()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Extracts and runs the bundled bootstrapper (silent install), then re-checks availability. Requires internet access.</summary>
    public static bool TryInstall()
    {
        string tempPath;
        try
        {
            tempPath = ExtractBootstrapperToTempFile();
        }
        catch
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(tempPath, "/silent /install")
            {
                UseShellExecute = true, // lets Windows handle the UAC elevation prompt if needed
            };
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch
        {
            return false;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best effort cleanup */ }
        }

        return IsAvailable();
    }

    private static string ExtractBootstrapperToTempFile()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("MicrosoftEdgeWebView2Setup.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Bootstrapper do WebView2 nao encontrado nos recursos embutidos.");

        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Nao foi possivel abrir o recurso embutido '{resourceName}'.");

        var tempPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebView2Setup.exe");
        using (var fileStream = File.Create(tempPath))
        {
            resourceStream.CopyTo(fileStream);
        }

        return tempPath;
    }
}
