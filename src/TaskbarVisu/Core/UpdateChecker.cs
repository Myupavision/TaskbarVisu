using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace TaskbarVisu.Core;

/// <param name="InstallerUrl">Installeur de la release (null s'il manque : on ouvre alors la page).</param>
/// <param name="InstallerSha256">Empreinte publiée par GitHub pour l'installeur, vérifiée après le téléchargement.</param>
/// <param name="Version">Version de la release, telle qu'affichée (« 1.0.0 », « 1.0.0-beta.2 »).</param>
internal sealed record UpdateInfo(string Version, string Tag, string PageUrl, string? InstallerUrl, string? InstallerSha256);

/// <summary>
/// Mises à jour : dernière release publiée sur GitHub. Version installée → téléchargement de l'installeur et
/// installation silencieuse (l'application se relance ensuite) ; version portable → ouverture de la page.
/// </summary>
internal static class UpdateChecker
{
    public const string Repository = "Myupavision/TaskbarVisu";
    public static string ReleasesPage => $"https://github.com/{Repository}/releases";

    /// <summary>Version de cette build, suffixe de préversion compris (« 1.0.0-beta »), sans l'empreinte du commit.</summary>
    public static string CurrentText { get; } = ReadCurrent();

    private static string ReadCurrent()
    {
        var assembly = typeof(UpdateChecker).Assembly;
        string? informational = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion;
        if (informational is { Length: > 0 })
        {
            string text = informational.Split('+')[0];
            if (TryParse(text, out _, out _)) return text;
        }
        var v = assembly.GetName().Version ?? new Version(0, 0);
        return $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
    }

    /// <summary>« 1.2.3 » ou « 1.2.3-beta.2 » : nombres, puis identifiants de préversion (règles SemVer).</summary>
    private static bool TryParse(string text, out Version numbers, out string[] prerelease)
    {
        int dash = text.IndexOf('-');
        prerelease = dash < 0 ? Array.Empty<string>() : text[(dash + 1)..].Split('.');
        string core = dash < 0 ? text : text[..dash];
        if (!Version.TryParse(core, out var parsed) || prerelease.Any(p => p.Length == 0))
        {
            numbers = new Version(0, 0);
            return false;
        }
        numbers = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build));
        return true;
    }

    /// <summary>
    /// Ordre SemVer : 1.0.0-beta &lt; 1.0.0-beta.2 &lt; 1.0.0-rc &lt; 1.0.0. Une version invalide est la plus ancienne.
    /// </summary>
    internal static int Compare(string a, string b)
    {
        bool okA = TryParse(a, out var na, out var pa), okB = TryParse(b, out var nb, out var pb);
        if (!okA || !okB) return okA.CompareTo(okB);
        int c = na.CompareTo(nb);
        if (c != 0) return c;
        // À nombres égaux, la version finale passe devant ses préversions.
        if (pa.Length == 0 || pb.Length == 0) return pb.Length.CompareTo(pa.Length);
        for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            bool numA = int.TryParse(pa[i], out int ia), numB = int.TryParse(pb[i], out int ib);
            c = numA && numB ? ia.CompareTo(ib)
                : numA != numB ? (numA ? -1 : 1)
                : string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return c;
        }
        return pa.Length.CompareTo(pb.Length);
    }

    /// <summary>Installée par l'installeur (dossier Programs de l'utilisateur), et non lancée en portable.</summary>
    public static bool IsInstalled =>
        !Paths.Portable && AppContext.BaseDirectory.StartsWith(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>La release la plus récente si elle est plus récente que cette version, sinon null.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var http = Client();
        string json = await http.GetStringAsync($"https://api.github.com/repos/{Repository}/releases/latest").ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        string version = tag.TrimStart('v', 'V');
        if (!TryParse(version, out _, out _)) return null;

        string? installer = null, sha256 = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (!name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase)) continue;
                installer = asset.GetProperty("browser_download_url").GetString();
                sha256 = asset.TryGetProperty("digest", out var digest) && digest.GetString() is { } d
                         && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? d[7..] : null;
            }
        }
        return Compare(version, CurrentText) > 0
            ? new UpdateInfo(version, tag, root.GetProperty("html_url").GetString() ?? ReleasesPage, installer, sha256)
            : null;
    }

    /// <summary>Télécharge et lance l'installeur en mode silencieux. Renvoie false si on a seulement ouvert la page.</summary>
    public static async Task<bool> InstallAsync(UpdateInfo update)
    {
        if (!IsInstalled || update.InstallerUrl is null)
        {
            OpenPage(update.PageUrl);
            return false;
        }
        string file = Path.Combine(Path.GetTempPath(), $"TaskbarVisu-{update.Version}-setup.exe");
        using (var http = Client())
        using (var stream = await http.GetStreamAsync(update.InstallerUrl).ConfigureAwait(false))
        using (var output = File.Create(file))
        {
            await stream.CopyToAsync(output).ConfigureAwait(false);
        }
        if (update.InstallerSha256 is { } expected)
        {
            string actual;
            using (var check = File.OpenRead(file))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(check).ConfigureAwait(false));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
                throw new InvalidDataException($"Empreinte de l'installeur incorrecte ({actual}, attendu {expected})");
            }
        }
        Log.Info($"Mise à jour {update.Version} téléchargée, installation…");
        // /RELAUNCH=1 : l'installeur relance TaskbarVisu à la fin (pas lors d'une installation silencieuse par winget).
        Process.Start(new ProcessStartInfo(file, "/SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1") { UseShellExecute = true });
        return true;
    }

    public static void OpenPage(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static HttpClient Client()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"TaskbarVisu/{CurrentText}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
