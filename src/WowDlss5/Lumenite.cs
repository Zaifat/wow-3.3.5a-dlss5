using System.Net.Http;
using System.Security.Cryptography;

namespace WowDlss5;

/// <summary>
/// LumeniteFX (векторы движения) — единственное, что не вшито в exe: его лицензия (AGNYA)
/// разрешает распространять проект только по официальным ссылкам автора. Поэтому семь нужных
/// файлов качаются при установке прямо из репозитория автора, по закреплённому коммиту,
/// и каждый сверяется с SHA-256 проверенной версии. Обновление у автора ничего не сломает:
/// ссылки ведут на конкретный коммит. Нет сети или файл не совпал — стек работает на
/// вшитом VORT (MIT).
/// </summary>
static class Lumenite
{
    const string Commit = "f8cbbb4eccfcb7adf0d74bb358ba349272e3c1e9";
    const string BaseUrl = "https://raw.githubusercontent.com/umar-afzaal/LumeniteFX/" + Commit + "/";

    static readonly (string Path, string Sha256)[] Files =
    {
        ("Shaders/lumenite_Kernel.fx",                   "DC44D101C568A8492606884037C86059A31B844FD5E144E733FB70DABC91F25C"),
        ("Shaders/lumenite_QuantMotion.fx",              "4A27B2F3676CD836E460BE3BB7A094CBF7DDD74E29B19D88FF7381066FCB94D8"),
        ("Shaders/include/lumenite_ColorManagement.fxh", "116D3C41CD48F7787A2ED87EEFC65FB2D9083FC1AB609766A3FBCCA3E62649A6"),
        ("Shaders/include/lumenite_Compute.fxh",         "736E3F39FC0C48A405C4D10B0D158502009DB940E2673FF778E6B919D3C1A55F"),
        ("Shaders/include/lumenite_Helpers.fxh",         "8826D613944BE27E14095982B20098FFF6D45E91C7D4028473C2F5B784C80028"),
        ("Shaders/include/lumenite_Projections.fxh",     "709EC414649B74E573CA0F12A5EF25998D332238F7CAB14A3D91053C8D388CAB"),
        ("Textures/lumenite_bluenoise256.png",           "2AD9E5B0BD28DDFF569DBD756A551FE8E681B67EB72A83BB027434E1BE24122B"),
    };

    static string CacheDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                           "WoW-DLSS5", "lumenite-" + Commit.Substring(0, 12));

    static bool Valid(string file, string sha) =>
        File.Exists(file) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) == sha;

    /// <summary>Установлен ли Lumenite в папке игры (иначе ReShadePreset должен ссылаться на VORT).</summary>
    public static bool InstalledIn(string wow) =>
        File.Exists(Path.Combine(wow, "reshade-shaders", "Shaders", "lumenite_Kernel.fx"));

    /// <summary>Скачать (или взять из кэша) и положить в reshade-shaders. false — остаёмся на VORT.</summary>
    public static bool Install(string wow, IReport r)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WoW-DLSS5/" + Installer.Version);

            int downloaded = 0;
            foreach (var (path, sha) in Files)
            {
                var cached = Path.Combine(CacheDir, path.Replace('/', '\\'));
                if (Valid(cached, sha)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(cached));
                var bytes = http.GetByteArrayAsync(BaseUrl + path).GetAwaiter().GetResult();
                if (Convert.ToHexString(SHA256.HashData(bytes)) != sha)
                    throw new InvalidDataException(path + ": файл у автора не совпадает с проверенной версией");
                File.WriteAllBytes(cached, bytes);
                downloaded++;
            }

            var shaders = Path.Combine(wow, "reshade-shaders");
            foreach (var (path, _) in Files)
            {
                var dest = Path.Combine(shaders, path.Replace('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(Path.Combine(CacheDir, path.Replace('/', '\\')), dest, true);
            }
            r.Log(LogKind.Ok, downloaded > 0
                ? "LumeniteFX скачан с GitHub автора (коммит " + Commit.Substring(0, 7) + ") и проверен"
                : "LumeniteFX взят из кэша (коммит " + Commit.Substring(0, 7) + ")");
            return true;
        }
        catch (Exception ex)
        {
            var msg = ex is HttpRequestException or TaskCanceledException ? "нет доступа к GitHub" : ex.Message;
            r.Log(LogKind.Warn, "LumeniteFX не установлен (" + msg + ") — векторы движения даст встроенный VORT. " +
                                "Позже можно переустановить, когда появится интернет.");
            return false;
        }
    }
}
