using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WowDlss5;

static class Diagnostics
{
    // Нейронная модель — с 616.56; RenoDX DLSS 5 v6+ проверен на 617.14 (README DLSS5-Feeder).
    static readonly Version DriverMin = new(616, 56), DriverRecommended = new(617, 14);

    public record Gpu(string Name, Version Driver);

    public static List<Gpu> Gpus()
    {
        // Класс видеоадаптеров: 0000, 0001, … — DriverDesc и DriverVersion (как в диспетчере устройств).
        // Там же остаются записи давно вынутых карт, поэтому берём только те, что сейчас в системе.
        var present = PresentAdapters();
        var list = new List<Gpu>();
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            foreach (var sub in cls?.GetSubKeyNames() ?? Array.Empty<string>())
            {
                if (!Regex.IsMatch(sub, @"^\d{4}$")) continue;
                using var k = cls.OpenSubKey(sub);
                var name = k?.GetValue("DriverDesc") as string ?? "";
                if (!name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || !present.Contains(name)) continue;
                var gpu = new Gpu(name, NvidiaVersion(k.GetValue("DriverVersion") as string));
                if (!list.Contains(gpu)) list.Add(gpu);
            }
        }
        catch { }
        return list;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplayDevices(string device, uint index, ref DISPLAY_DEVICE dd, uint flags);

    /// <summary>Имена адаптеров, которые сейчас есть в системе (не призраки старых карт из реестра).</summary>
    static HashSet<string> PresentAdapters()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref dd, 0); i++)
        {
            set.Add(dd.DeviceString.Trim());
            dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
        }
        return set;
    }

    // 32.0.16.1714 -> 617.14: последняя цифра третьей части + четвёртая часть.
    static Version NvidiaVersion(string win)
    {
        var p = win?.Split('.');
        if (p == null || p.Length != 4 || !int.TryParse(p[2], out var a) || !int.TryParse(p[3], out var b)) return null;
        int n = (a % 10) * 10000 + b;
        return new Version(n / 100, n % 100);
    }

    static string V(Version v) => $"{v.Major}.{v.Minor:D2}";

    /// <summary>Проверка перед установкой. false — есть блокирующие проблемы.</summary>
    public static bool SystemCheck(string wow, IReport r)
    {
        r.Log(LogKind.Title, "Проверка системы");
        bool ok = true;

        if (!File.Exists(Wow.Exe(wow)))
        {
            r.Log(LogKind.Fail, "Не найден Wow.exe в " + wow + " — укажи папку клиента на вкладке «Установка».");
            return false;
        }
        var (ver, is32, laa) = Wow.Info(wow);
        if (ver == "3,3,5,12340" || ver == "3.3.5.12340") r.Log(LogKind.Ok, "Wow.exe 3.3.5a (build 12340)");
        else r.Log(LogKind.Warn, $"Wow.exe версии {ver} — программа рассчитана на 3.3.5a (12340)");
        if (!is32) { r.Log(LogKind.Fail, "Wow.exe не 32-битный — это не клиент 3.3.5a"); ok = false; }
        if (laa) r.Log(LogKind.Ok, "Патч 4 ГБ (Large Address Aware) стоит");
        else r.Log(LogKind.Warn, "Нет патча 4 ГБ: DXVK + ReShade занимают память 32-битного процесса, без патча возможны вылеты в рейдах. Патч есть в Manager_WOW.");

        if (Wow.IsRunning(wow)) { r.Log(LogKind.Fail, "WoW запущен — закрой его перед установкой или удалением"); ok = false; }
        else r.Log(LogKind.Ok, "WoW не запущен");

        var gpus = Gpus();
        if (gpus.Count == 0) { r.Log(LogKind.Fail, "Видеокарта NVIDIA не найдена — DLSS работает только на NVIDIA RTX"); ok = false; }
        // Блокирует только отсутствие подходящей RTX: вторая карта или встройка рядом не мешают.
        bool usable = false;
        foreach (var g in gpus)
        {
            bool rtx = g.Name.Contains("RTX");
            bool driverOk = g.Driver != null && g.Driver >= DriverMin;
            usable |= rtx && driverOk;

            if (Regex.IsMatch(g.Name, @"RTX\s*50\d\d")) r.Log(LogKind.Ok, g.Name + " — нейронный рендеринг DLSS 5 поддерживается");
            else if (rtx) r.Log(LogKind.Warn, g.Name + " — нейромодель DLSS 5 работает только на RTX 50, здесь будет лишь сглаживание DLAA");
            else { r.Log(LogKind.Info, g.Name + " — без тензорных ядер, для DLSS не подходит"); continue; }

            if (g.Driver == null) r.Log(LogKind.Warn, "Не удалось определить версию драйвера NVIDIA");
            else if (g.Driver < DriverMin) r.Log(LogKind.Fail, $"Драйвер NVIDIA {V(g.Driver)} — нужен {V(DriverMin)}+ (лучше {V(DriverRecommended)}+). Обнови через NVIDIA App.");
            else if (g.Driver < DriverRecommended) r.Log(LogKind.Warn, $"Драйвер NVIDIA {V(g.Driver)} — работает, но рекомендуется {V(DriverRecommended)}+");
            else r.Log(LogKind.Ok, $"Драйвер NVIDIA {V(g.Driver)}");
        }
        if (gpus.Count > 0 && !usable) { r.Log(LogKind.Fail, "Нет видеокарты NVIDIA RTX с подходящим драйвером"); ok = false; }

        var st = InstallState.Load(wow);
        if (st != null)
            r.Log(st.Complete ? LogKind.Ok : LogKind.Warn,
                  st.Complete ? $"DLSS 5 установлен: режим {st.Mode}, {st.InstalledAt}"
                              : $"Установка от {st.InstalledAt} не завершилась — удали и поставь заново");
        r.Log(ok ? LogKind.Ok : LogKind.Fail, ok ? "Можно ставить." : "Есть блокирующие проблемы — исправь их.");
        return ok;
    }

    static string LastMatch(string path, string pattern)
    {
        if (!File.Exists(path)) return null;
        string hit = null;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            var rx = new Regex(pattern, RegexOptions.IgnoreCase);
            string line;
            while ((line = sr.ReadLine()) != null) if (rx.IsMatch(line)) hit = line.Trim();
        }
        catch { }
        return hit;
    }

    /// <summary>Разбор логов после запуска игры: где обрывается цепочка.</summary>
    public static void Logs(string wow, IReport r)
    {
        r.Log(LogKind.Title, "Логи после запуска игры");
        var st = InstallState.Load(wow);
        if (st == null) { r.Log(LogKind.Warn, "DLSS 5 не установлен."); return; }

        string feed = Path.Combine(wow, "dlss5-feed.log"), host = Path.Combine(wow, "host64", "dlss5-feed-host.log"),
               hostRs = Path.Combine(wow, "host64", "ReShade.log"), gameRs = Path.Combine(wow, "ReShade.log"),
               dxvk = Path.Combine(wow, "Wow_d3d9.log");

        if (st.RenderMode == RenderMode.Vulkan)
        {
            if (File.Exists(dxvk)) r.Log(LogKind.Ok, "DXVK работает (Wow_d3d9.log)");
            else r.Log(LogKind.Warn, "Нет Wow_d3d9.log — DXVK не подхватился. Проверь, что игру запускали после установки.");
        }
        if (File.Exists(gameRs)) r.Log(LogKind.Ok, "ReShade загрузился в игру");
        else { r.Log(LogKind.Warn, "Нет ReShade.log — ReShade не загрузился. Запусти игру и зайди в мир, потом проверь снова."); return; }

        if (!File.Exists(feed)) { r.Log(LogKind.Warn, "Нет dlss5-feed.log — аддон DLSS5-Feeder не загрузился в ReShade."); return; }

        var checks = new (string File, string Pattern, string Good)[]
        {
            (feed,   @"shared set ready",               "Передача кадров в host64 настроена"),
            (feed,   @"frame \d+ delivered",             "Кадры уходят в DLSS"),
            (host,   @"feature ready",                   "DLSS DLAA создан"),
            (host,   @"frame \d+ evaluated",             "DLSS обрабатывает кадры"),
            (hostRs, @"feature 18 created",              "Нейромодель DLSS 5 создана"),
            (hostRs, @"feature 18 evaluation succeeded", "Нейро-рендеринг DLSS 5 работает"),
        };
        foreach (var (file, pattern, good) in checks)
        {
            var line = LastMatch(file, pattern);
            if (line != null) r.Log(LogKind.Ok, good + "   · " + Short(line));
            else r.Log(LogKind.Warn, good + " — нет в " + Path.GetFileName(file));
        }

        var bad = new (string File, string Pattern, string Text)[]
        {
            (hostRs, @"0xbad00001", "Нейромодель не создалась (0xbad00001): видеокарта не RTX 50 или старый драйвер."),
            (feed,   @"transport-only", "Feeder работает только транспортом, без DLSS: в dlss5-feed.cfg должно быть mode=2."),
            (feed,   @"interop entry points are missing", "Нет Vulkan-interop в драйвере: обнови драйвер NVIDIA."),
            (gameRs, @"No add-on was registered", "Аддон не зарегистрировался в ReShade."),
            (feed,   @"no depth|depth.*not found", "Не найден буфер глубины: в игре должно быть выключено сглаживание (MSAA)."),
        };
        foreach (var (file, pattern, text) in bad)
        {
            var line = LastMatch(file, pattern);
            if (line != null) r.Log(LogKind.Fail, text + "   · " + Short(line));
        }
    }

    static string Short(string s) => s.Length > 140 ? s.Substring(0, 140) + "…" : s;

    /// <summary>host64\dlss5-feed-host64.exe --test: 300 прогонов DLSS + нейромодели без игры.</summary>
    public static void SelfTest(string wow, IReport r)
    {
        r.Log(LogKind.Title, "Самотест DLSS 5 (без игры, ~30 секунд)");
        var exe = Path.Combine(wow, "host64", "dlss5-feed-host64.exe");
        if (!File.Exists(exe)) { r.Log(LogKind.Fail, "DLSS 5 не установлен (нет host64\\dlss5-feed-host64.exe)."); return; }

        using (var p = Process.Start(new ProcessStartInfo(exe, "--test") { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false, CreateNoWindow = true }))
            if (!p.WaitForExit(180_000)) { try { p.Kill(); } catch { } r.Log(LogKind.Fail, "Самотест завис дольше 3 минут."); return; }

        var hostLog = Path.Combine(wow, "host64", "dlss5-feed-host.log");
        var hostRs = Path.Combine(wow, "host64", "ReShade.log");
        var done = LastMatch(hostLog, @"--test finished");
        var m = done == null ? null : Regex.Match(done, @"(\d+)/(\d+) evaluates succeeded");
        if (m != null && m.Success && m.Groups[1].Value == m.Groups[2].Value) r.Log(LogKind.Ok, $"DLSS: {m.Groups[1].Value}/{m.Groups[2].Value} кадров обработано");
        else r.Log(LogKind.Fail, "DLSS: " + (done ?? "самотест не завершился — смотри host64\\dlss5-feed-host.log"));

        var gpu = LastMatch(hostLog, @"--test: DLSS GPU");
        if (gpu != null) r.Log(LogKind.Info, Short(gpu));

        if (LastMatch(hostRs, @"feature 18 created") != null) r.Log(LogKind.Ok, "Нейромодель DLSS 5 создана");
        else r.Log(LogKind.Fail, "Нейромодель DLSS 5 не создалась" + (LastMatch(hostRs, "0xbad00001") != null ? " (0xbad00001: нужна RTX 50)" : ""));
        var ev = LastMatch(hostRs, @"feature 18 evaluation succeeded");
        if (ev != null) r.Log(LogKind.Ok, "Нейро-рендеринг обрабатывает кадры   · " + Short(ev));
        else r.Log(LogKind.Fail, "Нейро-рендеринг не обработал ни одного кадра");
    }

    /// <summary>Встроенный Verify-DLSS5Feeder.ps1 (только читает файлы).</summary>
    public static void Verify(string wow, IReport r)
    {
        r.Log(LogKind.Title, "Полная проверка DLSS5-Feeder");
        var ps1 = Path.Combine(Path.GetTempPath(), "WoW-DLSS5-Verify.ps1");
        Payload.ExtractFile("tools/Verify-DLSS5Feeder.ps1", ps1);
        var cmd = "[Console]::OutputEncoding=[Text.Encoding]::UTF8; & '" + ps1.Replace("'", "''") + "' -GamePath '" + wow.Replace("'", "''") + "' -Exe '" + Wow.Exe(wow).Replace("'", "''") + "' -NoPause";
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + cmd.Replace("\"", "\\\"") + "\"")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi);
        p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) r.Log(LogKind.Fail, e.Data); };
        p.BeginErrorReadLine();
        string line;
        while ((line = p.StandardOutput.ReadLine()) != null)
        {
            if (Regex.IsMatch(line, @"^\s*[╔║╚]")) continue; // баннер
            var t = line.TrimEnd();
            var kind = t.Contains("[FAIL]") ? LogKind.Fail : t.Contains("[WARN]") ? LogKind.Warn : t.Contains("[ OK ]") ? LogKind.Ok
                     : t.TrimStart().StartsWith("──") ? LogKind.Title : LogKind.Info;
            if (kind == LogKind.Title) t = t.Trim().Trim('─').Trim();
            else t = Regex.Replace(t, @"^\s*\[(FAIL|WARN| OK |DONE| -- | \.\. )\]\s*", "");
            if (t.Length > 0) r.Log(kind, t);
        }
        p.WaitForExit();
        try { File.Delete(ps1); } catch { }
    }
}
