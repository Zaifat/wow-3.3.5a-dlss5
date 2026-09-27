using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WowDlss5;

enum RenderMode { Vulkan, DX11 }

/// <summary>Всё, что настраивается в программе. Хранится в %APPDATA%\WoW-DLSS5\settings.json.</summary>
sealed class AppSettings
{
    public string WowDir { get; set; } = @"E:\WOW";
    public RenderMode Mode { get; set; } = RenderMode.DX11;   // DX11 (dgVoodoo2) — рекомендуемый

    // DLSS 5 (dlss5-feed.cfg и [RenoDX.DLSS5] в host64\ReShade.ini)
    public bool FeedEnabled { get; set; } = true;
    public bool CustomLook { get; set; } = false;
    public int NrStyle { get; set; } = 0;          // 0 стандартный, 1 Natural, 2 Cinematic
    public int NrPreset { get; set; } = 0;         // 0 стандартный, 1..3
    public double NrIntensity { get; set; } = 1.0;
    public double NrLocalTone { get; set; } = 1.0;
    public double NrStructure { get; set; } = 1.0;
    public double NrSkin { get; set; } = -1.0;       // -1 — авто: как у детализации (стандарт RenoDX)
    public bool NrUiCorrection { get; set; } = false;
    public double HoldStrength { get; set; } = 0.0;
    public int MvProvider { get; set; } = 3;       // 3 LumeniteFX Kernel, 4 QuantMotion, 2 VORT (встроенный)

    // ReShade
    public int OverlayKey { get; set; } = 36;      // Home
    public int EffectsKey { get; set; } = 19;      // Pause: DLSS 5 вкл/выкл в игре для сравнения

    // Режимы
    public int FpsLimit { get; set; } = 0;         // DXVK d3d9.maxFrameRate / dgVoodoo FPSLimit, 0 = без ограничения
    public bool DgvWatermark { get; set; } = false;

    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WoW-DLSS5", "settings.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json), new UTF8Encoding(false));
    }

    public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this));

    /// <summary>Сбросить всё, кроме папки и режима.</summary>
    public AppSettings Defaults() => new() { WowDir = WowDir, Mode = Mode };
}

/// <summary>Шаблоны конфигов стека и запись настроек в установленные файлы.</summary>
static class StackConfig
{
    static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    public const string HostReShadeIni =
        "[GENERAL]\r\nEffectSearchPaths=.\\\r\nTextureSearchPaths=.\\\r\n";

    // Корневой ReShade.ini — как его пишет Install-DLSS5Feeder 1.17.0. Без LoadFromDllMain:
    // на пути через Vulkan-слой он ломает регистрацию аддона (FO3-грабли).
    public const string ReShadeIni =
        "[ADDON]\r\nAddonPath=.\\\r\n\r\n" +
        "[DEPTH]\r\nDepthCopyBeforeClears=1\r\n\r\n" +
        "[GENERAL]\r\nEffectSearchPaths=.\\reshade-shaders\\Shaders\\**\r\nTextureSearchPaths=.\\reshade-shaders\\Textures\\**\r\n" +
        "IntermediateCachePath=\r\nNoDebugInfo=1\r\nNoEffectCache=0\r\nNoReloadOnInit=0\r\nPerformanceMode=0\r\nPreprocessorDefinitions=\r\n" +
        "PresetPath=.\\ReShadePreset.ini\r\nPresetShortcutKeys=\r\nPresetShortcutPaths=\r\nPresetTransitionDuration=1000\r\n" +
        "SkipLoadingDisabledEffects=0\r\nStartupPresetPath=\r\n\r\n" +
        "[INPUT]\r\nForceShortcutModifiers=1\r\nInputProcessing=2\r\nKeyEffects=0,0,0,0\r\nKeyOverlay=36,0,0,0\r\nKeyReload=0,0,0,0\r\nKeyScreenshot=0,0,0,0\r\n\r\n" +
        "[OVERLAY]\r\nTutorialProgress=4\r\n";

    public const string FeedCfg =
        "enabled=1\r\nmode=2\r\nhdr=-1\r\ndepth_inverted=-1\r\nflags=-1\r\nreset_every=0\r\nrebuild=0\r\nlog_frames=3\r\nhold_strength=0.00\r\n";

    public const string DxvkConf =
        "# WoW DLSS 5 - DXVK for WoW 3.3.5a.\r\n" +
        "# allowFse = False is the one setting the confirmed WoW configuration needed (DLSS5-Feeder issue #15).\r\n" +
        "dxvk.allowFse = False\r\n";

    // Техника провайдера векторов движения идёт в списке выше DLSS5_Feed.
    static string Preset(int provider)
    {
        var mv = provider switch
        {
            4 => "Lumenite_QuantMotion@lumenite_QuantMotion.fx",
            2 => "vort_MotionEffects@vort_Motion.fx",
            _ => "Lumenite_Kernel@lumenite_Kernel.fx",
        };
        return "Techniques=" + mv + ",DLSS5_Feed@DLSS5_Feed.fx\r\n" +
               "TechniqueSorting=" + mv + ",DLSS5_Feed@DLSS5_Feed.fx,DLSS5_Feed_Debug@DLSS5_Feed.fx\r\n" +
               "\r\n[DLSS5_Feed.fx]\r\nDEBUG_VIEW=0\r\nMV_SCALE=1.000000\r\nMV_SIGN=1.000000,1.000000\r\nPreprocessorDefinitions=DLSS5_MV_PROVIDER=" + provider + "\r\n";
    }

    /// <summary>Провайдер, который реально есть в папке: без скачанного Lumenite — встроенный VORT.</summary>
    public static int EffectiveProvider(string wow, int wanted) =>
        wanted == 2 || !Lumenite.InstalledIn(wow) ? 2 : wanted == 4 ? 4 : 3;

    public static void WriteTemplates(string wow, RenderMode mode)
    {
        File.WriteAllText(Path.Combine(wow, "ReShade.ini"), ReShadeIni);
        File.WriteAllText(Path.Combine(wow, "ReShadePreset.ini"), Preset(EffectiveProvider(wow, 3)));
        File.WriteAllText(Path.Combine(wow, "dlss5-feed.cfg"), FeedCfg);
        File.WriteAllText(Path.Combine(wow, "host64", "ReShade.ini"), HostReShadeIni);
        if (mode == RenderMode.Vulkan) File.WriteAllText(Path.Combine(wow, "dxvk.conf"), DxvkConf);
    }

    /// <summary>Записать настройки в установленный стек. Возвращает список изменённых файлов.</summary>
    public static List<string> Apply(string wow, RenderMode mode, AppSettings s)
    {
        var changed = new List<string>();

        var feed = IniFile.Load(Path.Combine(wow, "dlss5-feed.cfg"));
        feed.Set(null, "enabled", s.FeedEnabled ? "1" : "0");
        feed.Set(null, "mode", "2");
        feed.Set(null, "hold_strength", F(s.HoldStrength));
        feed.Save();
        changed.Add("dlss5-feed.cfg");

        // Векторы движения: техника провайдера выше DLSS5_Feed + номер провайдера для шейдера.
        File.WriteAllText(Path.Combine(wow, "ReShadePreset.ini"), Preset(EffectiveProvider(wow, s.MvProvider)));
        changed.Add("ReShadePreset.ini");

        var rs = IniFile.Load(Path.Combine(wow, "ReShade.ini"));
        rs.Set("INPUT", "KeyOverlay", s.OverlayKey + ",0,0,0");
        rs.Set("INPUT", "KeyEffects", s.EffectsKey + ",0,0,0");
        rs.Save();
        changed.Add("ReShade.ini");

        // Вид DLSS 5 (RenoDX). Без «вручную» ключи убираются — модель берёт свои значения.
        var hostIni = Path.Combine(wow, "host64", "ReShade.ini");
        var host = IniFile.Load(hostIni);
        const string sec = "RenoDX.DLSS5";
        bool c = s.CustomLook;
        host.Set(sec, "NRStyle", c ? s.NrStyle.ToString() : null);
        host.Set(sec, "NRPreset", c ? s.NrPreset.ToString() : null);
        host.Set(sec, "NRIntensity", c ? F(s.NrIntensity) : null);
        host.Set(sec, "NRLocalTone", c ? F(s.NrLocalTone) : null);
        host.Set(sec, "NRLocalStructure", c ? F(s.NrStructure) : null);
        host.Set(sec, "NRSkinStructure", c ? (s.NrSkin < 0 ? "-1" : F(s.NrSkin)) : null);
        host.Set(sec, "NRUICorrection", c ? (s.NrUiCorrection ? "1" : "0") : null);
        host.Save();
        changed.Add(@"host64\ReShade.ini");

        if (mode == RenderMode.Vulkan)
        {
            var dx = IniFile.Load(Path.Combine(wow, "dxvk.conf"));
            dx.Set(null, "dxvk.allowFse", "False");
            dx.Set(null, "d3d9.maxFrameRate", s.FpsLimit > 0 ? s.FpsLimit.ToString() : null);
            dx.Save();
            changed.Add("dxvk.conf");
        }
        else
        {
            var dg = IniFile.Load(Path.Combine(wow, "dgVoodoo.conf"));
            dg.Set("DirectX", "dgVoodooWatermark", s.DgvWatermark ? "true" : "false");
            dg.Set("GeneralExt", "FPSLimit", Math.Max(0, s.FpsLimit).ToString());
            dg.Save();
            changed.Add("dgVoodoo.conf");
        }
        return changed;
    }
}
