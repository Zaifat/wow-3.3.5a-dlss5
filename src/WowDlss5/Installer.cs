using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WowDlss5;

enum LogKind { Info, Ok, Warn, Fail, Title }

interface IReport
{
    void Log(LogKind kind, string text);
    void Progress(int percent);
}

/// <summary>
/// _DLSS5\state.json в папке игры: что было до установки, что добавлено, что ушло в бэкап.
/// Формат совместим с первой (PowerShell) версией утилиты — её установку тоже можно удалить.
/// </summary>
sealed class InstallState
{
    public string Mode { get; set; }
    public string InstalledAt { get; set; }
    public string Backup { get; set; }
    public List<string> BackedUp { get; set; } = new();
    public List<string> Before { get; set; } = new();
    public List<string> Added { get; set; } = new();
    public Dictionary<string, string> WtfOriginal { get; set; } = new();
    public bool Complete { get; set; }
    public bool LayerCreated { get; set; }
    public string AppVersion { get; set; }

    [JsonIgnore] public RenderMode RenderMode => Mode == "DX11" ? RenderMode.DX11 : RenderMode.Vulkan;

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string PathFor(string wow) => System.IO.Path.Combine(wow, "_DLSS5", "state.json");

    public static InstallState Load(string wow)
    {
        var p = PathFor(wow);
        if (!File.Exists(p)) return null;
        try { return JsonSerializer.Deserialize<InstallState>(File.ReadAllText(p)); }
        catch { return null; }
    }

    public void Save(string wow)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathFor(wow)));
        File.WriteAllText(PathFor(wow), JsonSerializer.Serialize(this, Json), new UTF8Encoding(false));
    }
}

static class Wow
{
    public static string Exe(string wow) => Path.Combine(wow, "Wow.exe");
    public static string ConfigWtf(string wow) => Path.Combine(wow, "WTF", "Config.wtf");

    public static bool IsRunning(string wow)
    {
        var exe = Path.GetFullPath(Exe(wow));
        foreach (var p in Process.GetProcessesByName("Wow"))
        {
            try { if (string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase)) return true; }
            catch { return true; } // нет доступа к процессу — считаем, что это он
        }
        return false;
    }

    /// <summary>Клиент 3.3.5a: версия файла, разрядность, флаг Large Address Aware.</summary>
    public static (string Version, bool Is32, bool Laa) Info(string wow)
    {
        var exe = Exe(wow);
        var ver = FileVersionInfo.GetVersionInfo(exe).FileVersion?.Replace(" ", "") ?? "";
        using var fs = File.OpenRead(exe);
        using var br = new BinaryReader(fs);
        fs.Position = 0x3C;
        int pe = br.ReadInt32();
        fs.Position = pe + 4;
        ushort machine = br.ReadUInt16();
        fs.Position = pe + 22;
        ushort chars = br.ReadUInt16();
        return (ver, machine == 0x14C, (chars & 0x20) != 0);
    }
}

/// <summary>
/// Машинный Vulkan-слой ReShade: C:\ProgramData\ReShade, реестр ImplicitLayers и ReShadeApps.ini.
/// Так же, как это делает Install-DLSS5Feeder: слой общий, но включается только для exe из Apps=.
/// Запись требует админа — выполняется в копии программы, запущенной через UAC.
/// </summary>
static class VulkanLayer
{
    static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ReShade");
    static string AppsIni => Path.Combine(Root, "ReShadeApps.ini");
    const string KeyPath = @"SOFTWARE\Khronos\Vulkan\ImplicitLayers";
    static readonly Version MinVersion = new(6, 8, 0);

    static string Json(int bits) => Path.Combine(Root, $"ReShade{bits}.json");
    static string Dll(int bits) => Path.Combine(Root, $"ReShade{bits}.dll");

    static RegistryKey OpenKey(int bits, bool write)
    {
        var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, bits == 32 ? RegistryView.Registry32 : RegistryView.Registry64);
        return write ? hklm.CreateSubKey(KeyPath, true) : hklm.OpenSubKey(KeyPath);
    }

    public static bool Registered(int bits)
    {
        using var k = OpenKey(bits, false);
        return k != null && k.GetValueNames().Any(n => n.Equals(Json(bits), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Сборка ReShade с поддержкой аддонов экспортирует ReShadeRegisterAddon — обычная нет.</summary>
    public static bool DllOk(int bits)
    {
        var p = Dll(bits);
        if (!File.Exists(p)) return false;
        var v = FileVersionInfo.GetVersionInfo(p);
        if (new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart) < MinVersion) return false;
        var bytes = File.ReadAllBytes(p);
        return IndexOf(bytes, Encoding.ASCII.GetBytes("ReShadeRegisterAddon")) >= 0;
    }

    static int IndexOf(byte[] hay, byte[] needle)
    {
        for (int i = 0; i <= hay.Length - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }

    static List<string> ReadApps(out string text, out Match m)
    {
        text = File.Exists(AppsIni) ? File.ReadAllText(AppsIni).TrimStart('\uFEFF') : "";
        m = Regex.Match(text, @"(?im)^\s*Apps\s*=\s*(.*)$");
        return m.Success
            ? m.Groups[1].Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList()
            : new List<string>();
    }

    public static bool AppListed(string exe) =>
        ReadApps(out _, out _).Any(a => a.TrimEnd('\\').Equals(exe, StringComparison.OrdinalIgnoreCase));

    public static bool Exists => File.Exists(Json(32)) && Registered(32);

    public static bool ReadyFor(string exe) => DllOk(32) && File.Exists(Json(32)) && Registered(32) && AppListed(exe);

    static void WriteApps(List<string> list, string text, Match m)
    {
        var line = "Apps=" + string.Join(",", list);
        text = m.Success ? text.Substring(0, m.Index) + line + text.Substring(m.Index + m.Length) : (text.TrimEnd() + "\r\n" + line).TrimStart();
        if (File.Exists(AppsIni)) File.Copy(AppsIni, AppsIni + ".bak", true);
        File.WriteAllText(AppsIni, text, new UTF8Encoding(true));
    }

    // ---- выполняется с правами администратора ----

    public static void Install(string exe)
    {
        Directory.CreateDirectory(Root);
        foreach (var bits in new[] { 32, 64 })
        {
            if (!DllOk(bits)) Payload.ExtractFile($"layer/ReShade{bits}.dll", Dll(bits));
            if (!File.Exists(Json(bits))) Payload.ExtractFile($"layer/ReShade{bits}.json", Json(bits));
            using var k = OpenKey(bits, true);
            k.SetValue(Json(bits), 0, RegistryValueKind.DWord);
        }
        // Для UWP-процессов (ALL APPLICATION PACKAGES), как у установщика ReShade.
        RunQuiet("icacls.exe", $"\"{Root}\" /grant *S-1-15-2-1:(OI)(CI)RX");

        var list = ReadApps(out var text, out var m);
        // Старые записи того же exe, которых больше нет на диске, убираются.
        list.RemoveAll(a => Path.GetFileName(a).Equals(Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase) && !File.Exists(a));
        if (!list.Any(a => a.Equals(exe, StringComparison.OrdinalIgnoreCase))) list.Add(exe);
        WriteApps(list, text, m);
    }

    public static void Uninstall(string exe, bool removeLayer)
    {
        var list = ReadApps(out var text, out var m);
        if (list.RemoveAll(a => a.TrimEnd('\\').Equals(exe, StringComparison.OrdinalIgnoreCase)) > 0) WriteApps(list, text, m);
        if (!removeLayer || list.Count > 0) return;

        foreach (var bits in new[] { 32, 64 })
        {
            using (var k = OpenKey(bits, true)) k.DeleteValue(Json(bits), false);
            foreach (var f in new[] { Json(bits), Dll(bits) }) if (File.Exists(f)) File.Delete(f);
        }
        foreach (var f in new[] { AppsIni, AppsIni + ".bak" }) if (File.Exists(f)) File.Delete(f);
        try { Directory.Delete(Root); } catch { }
    }

    static void RunQuiet(string file, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(file, args) { CreateNoWindow = true, UseShellExecute = false });
        p?.WaitForExit(15000);
    }

    /// <summary>Запустить эту же программу через UAC. true — успешно, false — отказ в UAC или ошибка.</summary>
    public static bool RunElevated(string args, out string error)
    {
        error = null;
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath, args) { UseShellExecute = true, Verb = "runas" });
            p.WaitForExit();
            if (p.ExitCode != 0) error = "код " + p.ExitCode;
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            error = "запрос прав администратора отклонён";
            return false;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    /// <summary>Точка входа повышенной копии: WoW-DLSS5.exe --layer install|uninstall "exe" [--remove-layer]</summary>
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length < 3) return 2;
            if (args[1] == "install") Install(args[2]);
            else if (args[1] == "uninstall") Uninstall(args[2], args.Contains("--remove-layer"));
            else return 2;
            return 0;
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "WoW-DLSS5-layer.log"), ex.ToString()); } catch { }
            return 1;
        }
    }
}

static class Installer
{
    public static string Version => typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    // Ключи Config.wtf, которые нужны стеку: D3D9 (его перехватывают DXVK / dgVoodoo2) и без MSAA
    // (с мультисэмплингом ReShade не видит буфер глубины, а без глубины DLSS не работает).
    static readonly Dictionary<string, string> WtfSettings = new() { ["gxApi"] = "d3d9", ["gxMultisample"] = "1" };

    // Файлы, которые генерируются, а не берутся из встроенных.
    static readonly string[] Generated = { "ReShade.ini", "ReShadePreset.ini", "dlss5-feed.cfg" };

    // Что игра и стек создают уже после установки: логи, второй ini ReShade, кэши.
    static readonly Regex RuntimeFiles = new(@"^(reshade|dgvoodoo|dxvk|dlss5|wow_d3d9|wow_dxgi|renodx)", RegexOptions.IgnoreCase);

    public static string KeyName(int vk) => vk switch
    {
        19 => "Pause", 145 => "Scroll Lock", 35 => "End", 45 => "Insert", 36 => "Home",
        >= 112 and <= 123 => "F" + (vk - 111), 0 => "(клавиша не назначена)", _ => "клавиша " + vk,
    };

    static string ModeDir(RenderMode m) => m == RenderMode.Vulkan ? "vulkan/" : "dx11/";

    static List<string> RootEntries(string wow) =>
        Directory.EnumerateFileSystemEntries(wow).Select(Path.GetFileName)
                 .Where(n => !n.Equals("_DLSS5", StringComparison.OrdinalIgnoreCase)).ToList();

    public static void Install(string wow, AppSettings s, IReport r)
    {
        var mode = s.Mode;
        var exe = Path.GetFullPath(Wow.Exe(wow));
        if (!File.Exists(exe)) throw new Exception("Не найден " + exe);
        if (Wow.IsRunning(wow)) throw new Exception("WoW запущен — закрой игру.");
        if (InstallState.Load(wow) != null) throw new Exception("DLSS 5 уже установлен — сначала удали его.");
        if (!Payload.Available) throw new Exception("В программе нет встроенных файлов DLSS — exe собран без них.");

        r.Log(LogKind.Title, $"Установка DLSS 5 — режим {(mode == RenderMode.Vulkan ? "Vulkan (DXVK)" : "DirectX 11 (dgVoodoo2)")}");

        // 1. Снимок папки и бэкап всего, что будет заменено
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backup = Path.Combine(wow, "_DLSS5", "backup-" + stamp);
        Directory.CreateDirectory(backup);

        var state = new InstallState
        {
            Mode = mode.ToString(),
            InstalledAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            Backup = backup,
            Before = RootEntries(wow),
            AppVersion = Version,
        };
        foreach (var (k, _) in WtfSettings) state.WtfOriginal[k] = WtfConfig.Get(Wow.ConfigWtf(wow), k);

        // Состояние пишется до первых изменений: если установка упадёт на середине,
        // удаление найдёт бэкап и откатит то, что успело появиться.
        state.Save(wow);

        var ours = Payload.TopLevel("common/").Concat(Payload.TopLevel(ModeDir(mode))).Concat(Generated);
        if (mode == RenderMode.Vulkan) ours = ours.Append("dxvk.conf");
        foreach (var name in ours.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var p = Path.Combine(wow, name);
            if (File.Exists(p)) File.Move(p, Path.Combine(backup, name));
            else if (Directory.Exists(p)) Directory.Move(p, Path.Combine(backup, name));
            else continue;
            state.BackedUp.Add(name);
            r.Log(LogKind.Info, "В бэкап: " + name);
        }
        state.Save(wow);
        r.Log(LogKind.Ok, "Бэкап: " + backup);

        try
        {
            // 2. Файлы
            long total = Payload.Size("common/") + Payload.Size(ModeDir(mode)), done = 0;
            void Tick(long n) { done += n; r.Progress((int)(5 + 80 * done / Math.Max(1, total))); }

            r.Log(LogKind.Title, "Файлы");
            Payload.ExtractTo(ModeDir(mode), wow, Tick);
            r.Log(LogKind.Ok, mode == RenderMode.Vulkan
                ? "DXVK 3.0.2 (d3d9.dll): Direct3D 9 → Vulkan"
                : "dgVoodoo2 2.87.5 (D3D9.dll) + ReShade 6.8.0 (dxgi.dll): Direct3D 9 → DirectX 11");
            Payload.ExtractTo("common/", wow, Tick);
            r.Log(LogKind.Ok, "DLSS5-Feeder 1.17.0 (+ исправление вылета Vulkan): dlss5-feed.addon32 + host64\\dlss5-feed-host64.exe");
            r.Log(LogKind.Ok, "host64: ReShade 6.8.0 x64, RenoDX DLSS 5 8.5.0-rc10, NGX DLSS 310.9.1, DLSS 5 NR 310.8");
            r.Log(LogKind.Ok, "Шейдеры: DLSS5_Feed + VORT (встроенные векторы движения)");
            Lumenite.Install(wow, r);

            StackConfig.WriteTemplates(wow, mode);
            StackConfig.Apply(wow, mode, s);
            r.Log(LogKind.Ok, "Конфиги ReShade, DLSS5-Feeder" + (mode == RenderMode.Vulkan ? ", DXVK" : ", dgVoodoo2") + " и твои настройки");
            r.Progress(88);

            // 3. Config.wtf
            WtfConfig.Set(Wow.ConfigWtf(wow), WtfSettings);
            r.Log(LogKind.Ok, "Config.wtf: gxApi = d3d9, сглаживание MSAA выключено (нужен буфер глубины)");

            // 4. Vulkan-слой ReShade
            if (mode == RenderMode.Vulkan)
            {
                r.Log(LogKind.Title, "Vulkan-слой ReShade");
                if (VulkanLayer.ReadyFor(exe)) r.Log(LogKind.Ok, "Уже установлен и включён для Wow.exe");
                else
                {
                    state.LayerCreated = !VulkanLayer.Exists;
                    state.Save(wow);
                    r.Log(LogKind.Info, "Нужны права администратора — подтверди запрос Windows (UAC)");
                    if (!VulkanLayer.RunElevated($"--layer install \"{exe}\"", out var err))
                        throw new Exception("Vulkan-слой ReShade не установлен: " + err + ". Без него режим Vulkan не работает — поставь ещё раз или выбери DirectX 11.");
                    if (!VulkanLayer.ReadyFor(exe)) throw new Exception("Vulkan-слой ReShade не прошёл проверку после установки.");
                    r.Log(LogKind.Ok, "Установлен в C:\\ProgramData\\ReShade и включён только для Wow.exe");
                }
            }
            r.Progress(96);
        }
        finally
        {
            state.Added = RootEntries(wow).Where(n => !state.Before.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
            state.Save(wow);
        }

        state.Complete = true;
        state.Save(wow);
        r.Progress(100);
        r.Log(LogKind.Title, "Готово");
        r.Log(LogKind.Ok, "DLSS 5 установлен. Запускай WoW как обычно — через Manager_WOW или кнопкой «Запустить WoW».");
        r.Log(LogKind.Info, $"В игре: {KeyName(s.EffectsKey)} — DLSS 5 вкл/выкл для сравнения, Home — меню ReShade.");
    }

    public static void Uninstall(string wow, IReport r)
    {
        var state = InstallState.Load(wow) ?? throw new Exception("DLSS 5 не установлен — удалять нечего.");
        if (Wow.IsRunning(wow)) throw new Exception("WoW запущен — закрой игру.");
        var exe = Path.GetFullPath(Wow.Exe(wow));

        r.Log(LogKind.Title, $"Удаление DLSS 5 (режим {state.Mode})");

        if (state.RenderMode == RenderMode.Vulkan && (VulkanLayer.AppListed(exe) || state.LayerCreated))
        {
            r.Log(LogKind.Info, "Убираю Wow.exe из Vulkan-слоя ReShade — подтверди запрос Windows (UAC)");
            var args = $"--layer uninstall \"{exe}\"" + (state.LayerCreated ? " --remove-layer" : "");
            if (VulkanLayer.RunElevated(args, out var err))
                r.Log(LogKind.Ok, state.LayerCreated && !VulkanLayer.Exists ? "Vulkan-слой ReShade удалён" : "Wow.exe убран из ReShadeApps.ini");
            else
                r.Log(LogKind.Warn, "Vulkan-слой не тронут (" + err + "). На игру он не влияет: без DXVK WoW не использует Vulkan.");
        }
        r.Progress(20);

        foreach (var name in state.Added)
        {
            var p = Path.Combine(wow, name);
            if (File.Exists(p)) File.Delete(p);
            else if (Directory.Exists(p)) Directory.Delete(p, true);
            else continue;
            r.Log(LogKind.Ok, "Удалено: " + name);
        }
        foreach (var f in Directory.EnumerateFiles(wow).Select(Path.GetFileName).ToList())
        {
            if (state.Before.Contains(f, StringComparer.OrdinalIgnoreCase) || !RuntimeFiles.IsMatch(f)) continue;
            File.Delete(Path.Combine(wow, f));
            r.Log(LogKind.Ok, "Удалено: " + f);
        }
        r.Progress(60);

        foreach (var name in state.BackedUp)
        {
            var src = Path.Combine(state.Backup, name);
            var dst = Path.Combine(wow, name);
            if (!File.Exists(src) && !Directory.Exists(src)) continue;
            // На месте оригинала сейчас лежит наша версия (она была в снимке «до», поэтому не в Added).
            if (File.Exists(dst)) File.Delete(dst);
            else if (Directory.Exists(dst)) Directory.Delete(dst, true);
            if (File.Exists(src)) File.Move(src, dst);
            else Directory.Move(src, dst);
            r.Log(LogKind.Ok, "Восстановлено: " + name);
        }

        WtfConfig.Set(Wow.ConfigWtf(wow), state.WtfOriginal);
        r.Log(LogKind.Ok, "Config.wtf: gxApi и сглаживание возвращены как были");

        Directory.Delete(Path.Combine(wow, "_DLSS5"), true);
        r.Progress(100);
        r.Log(LogKind.Title, "Готово");
        r.Log(LogKind.Ok, "Клиент возвращён в исходное состояние.");
    }
}
