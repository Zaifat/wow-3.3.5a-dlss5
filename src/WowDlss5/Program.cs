using System.Runtime.InteropServices;

namespace WowDlss5;

static class Program
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    [STAThread]
    static int Main(string[] args)
    {
        // Повышенная копия для Vulkan-слоя ReShade (запускается через UAC).
        if (args.Length > 0 && args[0] == "--layer") return VulkanLayer.Run(args);

        // Консольный режим: WoW-3.3.5a-DLSS5.exe --cli install|uninstall|apply|check|logs|selftest|verify [--wow <папка>] [--mode vulkan|dx11]
        if (args.Length > 0 && args[0] == "--cli") return Cli(args);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        int page = 0;
        if (args.Length > 1 && args[0] == "--page") int.TryParse(args[1], out page);
        Application.Run(new MainForm(page));
        return 0;
    }

    sealed class ConsoleReport : IReport
    {
        public int Fails;
        public void Log(LogKind kind, string text)
        {
            if (kind == LogKind.Fail) Fails++;
            Console.WriteLine(kind switch
            {
                LogKind.Title => "\n== " + text + " ==",
                LogKind.Ok => "  [ OK ] " + text,
                LogKind.Warn => "  [WARN] " + text,
                LogKind.Fail => "  [FAIL] " + text,
                _ => "         " + text,
            });
        }
        public void Progress(int percent) { }
    }

    static int Cli(string[] args)
    {
        AttachConsole(-1);
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var s = AppSettings.Load();
        for (int i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == "--wow") s.WowDir = args[i + 1];
            if (args[i] == "--mode") s.Mode = args[i + 1].Equals("dx11", StringComparison.OrdinalIgnoreCase) ? RenderMode.DX11 : RenderMode.Vulkan;
        }
        var r = new ConsoleReport();
        try
        {
            switch (args.Length > 1 ? args[1] : "")
            {
                case "install": if (Diagnostics.SystemCheck(s.WowDir, r)) Installer.Install(s.WowDir, s, r); break;
                case "uninstall": Installer.Uninstall(s.WowDir, r); break;
                case "check": Diagnostics.SystemCheck(s.WowDir, r); break;
                case "logs": Diagnostics.Logs(s.WowDir, r); break;
                case "apply":
                    var st = InstallState.Load(s.WowDir) ?? throw new Exception("DLSS 5 не установлен.");
                    r.Log(LogKind.Ok, "Записано: " + string.Join(", ", StackConfig.Apply(s.WowDir, st.RenderMode, s)));
                    break;
                case "selftest": Diagnostics.SelfTest(s.WowDir, r); break;
                case "verify": Diagnostics.Verify(s.WowDir, r); break;
                default: Console.WriteLine("install | uninstall | apply | check | logs | selftest | verify  [--wow <папка>] [--mode vulkan|dx11]"); return 2;
            }
        }
        catch (Exception ex) { r.Log(LogKind.Fail, ex.Message); }
        return r.Fails > 0 ? 1 : 0;
    }
}
