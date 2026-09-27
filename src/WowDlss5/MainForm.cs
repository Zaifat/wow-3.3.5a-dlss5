using System.Diagnostics;

namespace WowDlss5;

static class Theme
{
    public static readonly Color Bg = Color.FromArgb(0x17, 0x18, 0x1C);
    public static readonly Color Side = Color.FromArgb(0x1F, 0x21, 0x27);
    public static readonly Color Card = Color.FromArgb(0x25, 0x27, 0x2E);
    public static readonly Color Field = Color.FromArgb(0x2E, 0x31, 0x39);
    public static readonly Color Border = Color.FromArgb(0x3A, 0x3D, 0x47);
    public static readonly Color Text = Color.FromArgb(0xE8, 0xE8, 0xEA);
    public static readonly Color Muted = Color.FromArgb(0x9B, 0xA1, 0xAB);
    public static readonly Color Accent = Color.FromArgb(0x76, 0xB9, 0x00);
    public static readonly Color AccentHover = Color.FromArgb(0x8A, 0xD0, 0x10);
    public static readonly Color Warn = Color.FromArgb(0xE5, 0xB5, 0x3A);
    public static readonly Color Fail = Color.FromArgb(0xEF, 0x5B, 0x55);

    public static readonly Font Base = new("Segoe UI", 10f);
    public static readonly Font Small = new("Segoe UI", 9f);
    public static readonly Font Bold = new("Segoe UI Semibold", 10.5f);
    public static readonly Font H1 = new("Segoe UI Semibold", 17f);
    public static readonly Font H2 = new("Segoe UI Semibold", 12f);
    public static readonly Font Mono = new("Cascadia Mono", 9.5f);
}

/// <summary>Журнал с цветными строками; безопасен для вызова из фоновых задач.</summary>
sealed class LogBox : RichTextBox, IReport
{
    public event Action<int> ProgressChanged;

    public LogBox()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = FontFamily.Families.Any(f => f.Name == "Cascadia Mono") ? Theme.Mono : new Font("Consolas", 9.5f);
        DetectUrls = false;
        HideSelection = false;
    }

    public void Log(LogKind kind, string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Log(kind, text)); return; }
        var (prefix, color) = kind switch
        {
            LogKind.Ok => ("  ✓  ", Theme.Accent),
            LogKind.Warn => ("  !  ", Theme.Warn),
            LogKind.Fail => ("  ✕  ", Theme.Fail),
            LogKind.Title => (TextLength > 0 ? "\n" : "", Theme.Text),
            _ => ("     ", Theme.Muted),
        };
        SelectionStart = TextLength;
        SelectionLength = 0;
        SelectionColor = color;
        SelectionFont = kind == LogKind.Title ? new Font(Font, FontStyle.Bold) : Font;
        AppendText(prefix + text + "\n");
        SelectionColor = ForeColor;
        ScrollToCaret();
    }

    public void Progress(int percent)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Progress(percent)); return; }
        ProgressChanged?.Invoke(percent);
    }
}

sealed class FlatBtn : Button
{
    public FlatBtn(string text, bool accent = false)
    {
        Text = text;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = accent ? 0 : 1;
        FlatAppearance.BorderColor = Theme.Border;
        BackColor = accent ? Theme.Accent : Theme.Field;
        ForeColor = accent ? Color.Black : Theme.Text;
        FlatAppearance.MouseOverBackColor = accent ? Theme.AccentHover : Theme.Border;
        Font = accent ? Theme.Bold : Theme.Base;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14, 6, 14, 6);
        Margin = new Padding(0, 0, 10, 0);
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
    }
}

sealed class MainForm : Form
{
    AppSettings _s = AppSettings.Load();
    bool _busy, _loading;

    readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(28, 22, 28, 22) };
    readonly List<(Button Nav, Control Page)> _pages = new();
    readonly Label _sideStatus = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 64, ForeColor = Theme.Muted, Font = Theme.Small, Padding = new Padding(18, 0, 12, 8) };

    // Установка
    readonly TextBox _wowDir = new();
    readonly Label _wowInfo = new();
    readonly RadioButton _rbVulkan = new(), _rbDx11 = new();
    readonly Label _status = new(), _statusSub = new();
    readonly FlatBtn _btnInstall = new("Установить DLSS 5", true), _btnUninstall = new("Удалить"), _btnLaunch = new("Запустить WoW");
    readonly ProgressBar _progress = new() { Height = 6, Dock = DockStyle.Top, Style = ProgressBarStyle.Continuous, Visible = false };
    readonly LogBox _log = new() { Dock = DockStyle.Fill };

    // Настройки
    readonly CheckBox _cbFeed = new(), _cbCustom = new(), _cbUi = new(), _cbWatermark = new();
    readonly ComboBox _cmbStyle = new(), _cmbPreset = new(), _cmbMv = new(), _cmbOverlayKey = new(), _cmbEffectsKey = new();
    readonly (TrackBar Bar, Label Value)[] _sliders = new (TrackBar, Label)[5];
    readonly NumericUpDown _numFps = new();
    readonly List<Control> _lookControls = new();
    readonly Label _settingsNote = new();

    // Диагностика
    readonly LogBox _diag = new() { Dock = DockStyle.Fill };
    readonly List<Button> _actionButtons = new();

    static readonly (string Name, int Vk)[] OverlayKeys =
        { ("Home", 36), ("End", 35), ("Insert", 45), ("F10", 121), ("F11", 122), ("F12", 123), ("Pause", 19), ("Scroll Lock", 145) };
    static readonly (string Name, int Vk)[] EffectsKeys =
        { ("Pause", 19), ("Scroll Lock", 145), ("End", 35), ("Insert", 45), ("F9", 120), ("F10", 121), ("F11", 122), ("F12", 123), ("нет", 0) };

    public MainForm(int startPage = 0)
    {
        Text = "WoW 3.3.5a DLSS 5";
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Base;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1060, 720);
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(_content);
        Controls.Add(BuildSidebar());

        AddPage("Установка", BuildInstallPage());
        AddPage("Настройки", BuildSettingsPage());
        AddPage("Диагностика", BuildDiagPage());
        ShowPage(Math.Clamp(startPage, 0, _pages.Count - 1));

        LoadUi();
        RefreshStatus();
        _log.ProgressChanged += p => _progress.Value = Math.Clamp(p, 0, 100);
        _diag.ProgressChanged += p => _progress.Value = Math.Clamp(p, 0, 100);

        Shown += (_, _) =>
        {
            if (!Payload.Available) _log.Log(LogKind.Fail, "В программе нет встроенных файлов DLSS — используй собранный WoW-3.3.5a-DLSS5.exe.");
            else _log.Log(LogKind.Info, "Всё для DLSS 5 уже внутри программы. Только LumeniteFX скачивается при установке с GitHub автора; без интернета — встроенный VORT.");
            RunTask(_log, r => Diagnostics.SystemCheck(_s.WowDir, r), quiet: true);
        };
    }

    // ---------------------------------------------------------------- каркас

    Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Theme.Side };
        var nav = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        var head = new Panel { Dock = DockStyle.Top, Height = 124 };
        head.Controls.Add(new Label { Text = "Neural Rendering", ForeColor = Theme.Muted, Font = Theme.Small, AutoSize = true, Location = new Point(20, 90) });
        head.Controls.Add(new Label { Text = "WoW 3.3.5a\nDLSS 5", ForeColor = Theme.Accent, Font = Theme.H1, AutoSize = true, Location = new Point(16, 16) });
        side.Controls.Add(nav);
        side.Controls.Add(_sideStatus);
        side.Controls.Add(head);
        side.Tag = nav;
        return side;
    }

    void AddPage(string title, Control page)
    {
        var nav = (FlowLayoutPanel)Controls.OfType<Panel>().First(p => p.Tag is FlowLayoutPanel).Tag;
        var b = new Button
        {
            Text = "   " + title, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat,
            Width = 220, Height = 44, Margin = Padding.Empty, ForeColor = Theme.Text, BackColor = Theme.Side,
            Font = Theme.Bold, Cursor = Cursors.Hand, UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.Card;
        int index = _pages.Count;
        b.Click += (_, _) => ShowPage(index);
        nav.Controls.Add(b);
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _content.Controls.Add(page);
        _pages.Add((b, page));
    }

    void ShowPage(int index)
    {
        for (int i = 0; i < _pages.Count; i++)
        {
            _pages[i].Page.Visible = i == index;
            _pages[i].Nav.BackColor = i == index ? Theme.Card : Theme.Side;
            _pages[i].Nav.ForeColor = i == index ? Theme.Accent : Theme.Text;
        }
    }

    static Label Title(string text) => new() { Text = text, Font = Theme.H1, ForeColor = Theme.Text, AutoSize = false, Height = 50, TextAlign = ContentAlignment.TopLeft, Margin = new Padding(0, 0, 0, 14) };

    static Label Hint(string text) => new() { Text = text, ForeColor = Theme.Muted, Font = Theme.Small, AutoSize = true, Margin = new Padding(0, 2, 0, 8) };

    /// <summary>Карточка: заголовок + вертикальный список строк, растягивается по ширине.</summary>
    static Panel CardOf(string heading, params Control[] rows)
    {
        var card = new Panel { BackColor = Theme.Card, Padding = new Padding(18, 14, 18, 14), Margin = new Padding(0, 0, 0, 14), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };
        var t = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Card };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (heading != null) t.Controls.Add(new Label { Text = heading, Font = Theme.H2, ForeColor = Theme.Text, AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
        foreach (var r in rows) { r.Anchor |= AnchorStyles.Left | AnchorStyles.Right; t.Controls.Add(r); }
        card.Controls.Add(t);
        return card;
    }

    /// <summary>Страница: вертикальный стек карточек сверху, последний элемент заполняет остаток.</summary>
    static Control Page(Control fill, params Control[] top)
    {
        var page = new Panel { BackColor = Theme.Bg };
        page.Controls.Add(fill);
        fill.Dock = DockStyle.Fill;
        for (int i = top.Length - 1; i >= 0; i--)
        {
            if (i > 0 && top[i] is not Label) page.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Theme.Bg });
            top[i].Dock = DockStyle.Top;
            page.Controls.Add(top[i]);
        }
        return page;
    }

    // ---------------------------------------------------------------- Установка

    Control BuildInstallPage()
    {
        StyleField(_wowDir);
        _wowDir.Dock = DockStyle.Fill;
        _wowDir.Leave += (_, _) => { _s.WowDir = _wowDir.Text.Trim(); SaveSettings(); RefreshStatus(); };
        var browse = new FlatBtn("Обзор…") { Dock = DockStyle.Right, AutoSize = false, Width = 120, Padding = Padding.Empty };
        browse.Click += (_, _) => Browse();
        var dirRow = new Panel { Height = 36, BackColor = Theme.Card, Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 4) };
        dirRow.Controls.Add(_wowDir);
        dirRow.Controls.Add(new Panel { Width = 10, Dock = DockStyle.Right, BackColor = Theme.Card });
        dirRow.Controls.Add(browse);
        _wowInfo.AutoSize = true;
        _wowInfo.Font = Theme.Small;
        _wowInfo.Margin = new Padding(0, 6, 0, 0);

        StyleRadio(_rbVulkan, "Vulkan через DXVK — альтернатива");
        StyleRadio(_rbDx11, "DirectX 11 через dgVoodoo2 — рекомендуется");
        _rbVulkan.CheckedChanged += (_, _) => { if (!_loading && _rbVulkan.Checked) { _s.Mode = RenderMode.Vulkan; SaveSettings(); RefreshStatus(); } };
        _rbDx11.CheckedChanged += (_, _) => { if (!_loading && _rbDx11.Checked) { _s.Mode = RenderMode.DX11; SaveSettings(); RefreshStatus(); } };

        _status.Font = Theme.H2;
        _status.AutoSize = true;
        _statusSub.AutoSize = true;
        _statusSub.ForeColor = Theme.Muted;
        _statusSub.Font = Theme.Small;
        _statusSub.Margin = new Padding(0, 2, 0, 12);

        _btnInstall.Click += (_, _) => InstallClicked();
        _btnUninstall.Click += (_, _) => UninstallClicked();
        _btnLaunch.Click += (_, _) => Launch();
        _actionButtons.AddRange(new Button[] { _btnInstall, _btnUninstall, _btnLaunch, browse });
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Margin = Padding.Empty };
        buttons.Controls.AddRange(new Control[] { _btnInstall, _btnUninstall, _btnLaunch });

        var logCard = new Panel { BackColor = Theme.Card, Padding = new Padding(14, 10, 8, 10) };
        logCard.Controls.Add(_log);

        return Page(logCard,
            Title("Установка"),
            CardOf("Папка клиента WoW 3.3.5a", dirRow, _wowInfo),
            CardOf("Как подключить DLSS 5",
                _rbDx11, Hint("DirectX 9 → DirectX 11. Проверено на WoW 3.3.5a, права администратора не нужны."),
                _rbVulkan, Hint("DirectX 9 → Vulkan через DXVK. Один раз попросит права администратора — для слоя ReShade.")),
            CardOf(null, _status, _statusSub, buttons),
            _progress);
    }

    void Browse()
    {
        using var d = new FolderBrowserDialog { Description = "Папка, где лежит Wow.exe", UseDescriptionForTitle = true, SelectedPath = Directory.Exists(_s.WowDir) ? _s.WowDir : "" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        _wowDir.Text = d.SelectedPath;
        _s.WowDir = d.SelectedPath;
        SaveSettings();
        RefreshStatus();
    }

    void RefreshStatus()
    {
        var wow = _s.WowDir;
        bool exe = File.Exists(Wow.Exe(wow));
        if (!exe) { _wowInfo.Text = "Wow.exe здесь не найден"; _wowInfo.ForeColor = Theme.Fail; }
        else
        {
            try
            {
                var (ver, _, laa) = Wow.Info(wow);
                bool ok = ver is "3,3,5,12340" or "3.3.5.12340";
                _wowInfo.Text = (ok ? "✓ Wow.exe 3.3.5a (12340)" : "Wow.exe версии " + ver) + (laa ? " · патч 4 ГБ стоит" : " · нет патча 4 ГБ");
                _wowInfo.ForeColor = ok ? Theme.Accent : Theme.Warn;
            }
            catch { _wowInfo.Text = "Wow.exe не читается"; _wowInfo.ForeColor = Theme.Warn; }
        }

        var st = exe ? InstallState.Load(wow) : null;
        var selected = _s.Mode == RenderMode.Vulkan ? "Vulkan" : "DX11";
        if (st == null)
        {
            _status.Text = "DLSS 5 не установлен";
            _status.ForeColor = Theme.Text;
            _statusSub.Text = "Все файлы уже внутри программы: DXVK, dgVoodoo2, ReShade, DLSS5-Feeder, RenoDX, модели NVIDIA DLSS.";
            _btnInstall.Text = "Установить DLSS 5";
            _sideStatus.Text = "Не установлен";
        }
        else if (!st.Complete)
        {
            _status.Text = "Установка не завершена";
            _status.ForeColor = Theme.Warn;
            _statusSub.Text = $"Режим {st.Mode}, {st.InstalledAt}. Нажми «Удалить», потом установи заново.";
            _btnInstall.Text = "Переустановить";
            _sideStatus.Text = "Установка не завершена";
        }
        else
        {
            _status.Text = $"DLSS 5 установлен — {(st.RenderMode == RenderMode.Vulkan ? "Vulkan (DXVK)" : "DirectX 11 (dgVoodoo2)")}";
            _status.ForeColor = Theme.Accent;
            _statusSub.Text = $"С {st.InstalledAt}. В игре: {Installer.KeyName(_s.EffectsKey)} — DLSS 5 вкл/выкл для сравнения, Home — меню ReShade.";
            _btnInstall.Text = st.Mode == selected ? "Переустановить" : $"Переустановить в режиме {selected}";
            _sideStatus.Text = $"Установлен · {st.Mode}";
        }
        _sideStatus.Text += "\nверсия " + Installer.Version;
        _btnUninstall.Enabled = !_busy && st != null;
        _btnInstall.Enabled = !_busy && exe;
        _btnLaunch.Enabled = !_busy && exe;
        _settingsNote.Text = st?.Complete == true
            ? "Применяется сразу. Если игра запущена — перезапусти её."
            : "DLSS 5 не установлен: применится при установке.";
    }

    void InstallClicked()
    {
        var st = InstallState.Load(_s.WowDir);
        if (st != null && MessageBox.Show(this, "DLSS 5 уже установлен. Удалить текущую установку и поставить заново?", "WoW 3.3.5a DLSS 5",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        ReadSettingsUi();
        var s = _s.Clone();
        RunTask(_log, r =>
        {
            if (!Diagnostics.SystemCheck(s.WowDir, r)) return;
            if (InstallState.Load(s.WowDir) != null) Installer.Uninstall(s.WowDir, r);
            Installer.Install(s.WowDir, s, r);
        });
    }

    void UninstallClicked()
    {
        if (MessageBox.Show(this, "Удалить DLSS 5 и вернуть клиент в исходное состояние?", "WoW 3.3.5a DLSS 5",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var wow = _s.WowDir;
        RunTask(_log, r => Installer.Uninstall(wow, r));
    }

    void Launch()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Wow.Exe(_s.WowDir)) { WorkingDirectory = _s.WowDir, UseShellExecute = true });
            _log.Log(LogKind.Ok, "WoW запущен. В игре: Home — меню ReShade.");
        }
        catch (Exception ex) { _log.Log(LogKind.Fail, ex.Message); }
    }

    // ---------------------------------------------------------------- Настройки

    Control BuildSettingsPage()
    {
        StyleCheck(_cbFeed, "Включить DLSS 5 (нейро-рендеринг)");
        StyleCheck(_cbCustom, "Настроить вид вручную (иначе — стандартные значения модели)");
        _cbCustom.CheckedChanged += (_, _) => UpdateLookEnabled();
        StyleCheck(_cbUi, "Коррекция интерфейса — включай, только если HUD выглядит странно");
        StyleCheck(_cbWatermark, "Водяной знак dgVoodoo — чтобы убедиться, что dgVoodoo2 работает");

        StyleCombo(_cmbStyle, "Стандартный", "Natural — естественный", "Cinematic — кинематографичный");
        StyleCombo(_cmbPreset, "Стандартный", "Пресет 1", "Пресет 2", "Пресет 3");
        StyleCombo(_cmbMv, "LumeniteFX Kernel — рекомендуется", "LumeniteFX QuantMotion", "VORT — встроенный");
        StyleCombo(_cmbOverlayKey, OverlayKeys.Select(k => k.Name).ToArray());
        StyleCombo(_cmbEffectsKey, EffectsKeys.Select(k => k.Name).ToArray());

        _numFps.Minimum = 0;
        _numFps.Maximum = 500;
        _numFps.Width = 90;
        _numFps.BackColor = Theme.Field;
        _numFps.ForeColor = Theme.Text;
        _numFps.BorderStyle = BorderStyle.FixedSingle;

        string[] names = { "Сила эффекта", "Локальный тон — свет и тени по областям", "Детализация поверхностей", "Кожа и лица персонажей (влево до упора — авто)", "Стабилизатор картинки (0 — выкл.)" };
        for (int i = 0; i < _sliders.Length; i++) _sliders[i] = MakeSlider(i == 4 ? 100 : 200, i == 3 ? -25 : 0);

        var look = new Control[]
        {
            Row("Стиль", _cmbStyle), Row("Пресет модели", _cmbPreset),
            SliderRow(names[0], 0), SliderRow(names[1], 1), SliderRow(names[2], 2), SliderRow(names[3], 3), _cbUi,
        };
        _lookControls.AddRange(look);

        var apply = new FlatBtn("Применить", true);
        apply.Click += (_, _) => ApplySettings();
        var reset = new FlatBtn("Стандартные значения");
        reset.Click += (_, _) => { _s = _s.Defaults(); LoadUi(); ApplySettings(); };
        _actionButtons.AddRange(new Button[] { apply, reset });
        _settingsNote.AutoSize = true;
        _settingsNote.ForeColor = Theme.Muted;
        _settingsNote.Font = Theme.Small;
        _settingsNote.Margin = new Padding(6, 8, 0, 0);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Bg, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
        bar.Controls.AddRange(new Control[] { apply, reset, _settingsNote });

        var stack = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, BackColor = Theme.Bg };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control c) { c.Dock = DockStyle.Fill; stack.Controls.Add(c); }
        Add(Title("Настройки"));
        Add(CardOf("DLSS 5",
            _cbFeed,
            Hint("Выключить, не удаляя: игра пойдёт без нейро-рендеринга, FPS вернётся."),
            _cbCustom));
        Add(CardOf("Вид нейро-рендеринга (RenoDX DLSS 5)", look.Concat(new Control[]
            { Hint("1.00 — стандарт модели; больше — сильнее меняет картинку. Эти же ползунки есть в игре: Home → Add-ons → DLSS 5 Feed.") }).ToArray()));
        Add(CardOf("Картинка в движении",
            SliderRow(names[4], 4),
            Hint("Убирает «дыхание» неподвижных участков, когда камера стоит. Эксперимент DLSS5-Feeder: при медленной анимации возможна задержка."),
            Row("Векторы движения", _cmbMv),
            Hint("LumeniteFX скачивается при установке с GitHub автора (его лицензия не разрешает вшивать). Без интернета используется встроенный VORT.")));
        Add(CardOf("Сравнение в игре",
            Row("DLSS 5 вкл/выкл на лету", _cmbEffectsKey),
            Hint("Нажимай в игре — картинка мгновенно переключается между DLSS 5 и оригиналом. То же галочкой: Home → вкладка Home → DLSS5_Feed."),
            Row("Меню ReShade", _cmbOverlayKey),
            Hint("Не назначай клавиши, которыми печатаешь в чате: ReShade ловит их и там.")));
        Add(CardOf("Производительность и режимы", Row("Ограничение FPS (0 — нет)", _numFps), _cbWatermark));

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg, Padding = new Padding(0, 0, 12, 0) };
        scroll.Controls.Add(stack);
        scroll.Resize += (_, _) => stack.Width = scroll.ClientSize.Width - scroll.Padding.Horizontal;

        var page = new Panel { BackColor = Theme.Bg };
        page.Controls.Add(scroll);
        page.Controls.Add(bar);
        return page;
    }

    // min < 0 — у ползунка есть положение «авто» (значение модели -1).
    (TrackBar, Label) MakeSlider(int max, int min = 0)
    {
        var bar = new TrackBar { Minimum = min, Maximum = max, TickFrequency = 25, SmallChange = 5, LargeChange = 25, BackColor = Theme.Card, Dock = DockStyle.Fill, AutoSize = false, Height = 30 };
        var val = new Label { Width = 48, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Text, Dock = DockStyle.Right };
        bar.ValueChanged += (_, _) => val.Text = SliderText(bar.Value);
        return (bar, val);
    }

    Control SliderRow(string label, int i)
    {
        var (bar, val) = _sliders[i];
        var p = new TableLayoutPanel { ColumnCount = 3, Height = 34, BackColor = Theme.Card, Margin = new Padding(0, 2, 0, 2) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        p.Controls.Add(new Label { Text = label, ForeColor = Theme.Text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        p.Controls.Add(bar, 1, 0);
        p.Controls.Add(val, 2, 0);
        return p;
    }

    static Control Row(string label, Control field)
    {
        var p = new TableLayoutPanel { ColumnCount = 2, Height = 36, BackColor = Theme.Card, Margin = new Padding(0, 2, 0, 2) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.Controls.Add(new Label { Text = label, ForeColor = Theme.Text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        field.Anchor = AnchorStyles.Left;
        p.Controls.Add(field, 1, 0);
        return p;
    }

    void UpdateLookEnabled()
    {
        foreach (var c in _lookControls) c.Enabled = _cbCustom.Checked;
    }

    void LoadUi()
    {
        _loading = true;
        _wowDir.Text = _s.WowDir;
        _rbVulkan.Checked = _s.Mode == RenderMode.Vulkan;
        _rbDx11.Checked = _s.Mode == RenderMode.DX11;
        _cbFeed.Checked = _s.FeedEnabled;
        _cbCustom.Checked = _s.CustomLook;
        _cmbStyle.SelectedIndex = Math.Clamp(_s.NrStyle, 0, 2);
        _cmbPreset.SelectedIndex = Math.Clamp(_s.NrPreset, 0, 3);
        SetSlider(0, _s.NrIntensity);
        SetSlider(1, _s.NrLocalTone);
        SetSlider(2, _s.NrStructure);
        SetSlider(3, _s.NrSkin);
        SetSlider(4, _s.HoldStrength);
        _cbUi.Checked = _s.NrUiCorrection;
        _cmbMv.SelectedIndex = _s.MvProvider switch { 4 => 1, 2 => 2, _ => 0 };
        _cmbOverlayKey.SelectedIndex = Math.Max(0, Array.FindIndex(OverlayKeys, k => k.Vk == _s.OverlayKey));
        _cmbEffectsKey.SelectedIndex = Math.Max(0, Array.FindIndex(EffectsKeys, k => k.Vk == _s.EffectsKey));
        _numFps.Value = Math.Clamp(_s.FpsLimit, 0, 500);
        _cbWatermark.Checked = _s.DgvWatermark;
        UpdateLookEnabled();
        _loading = false;
    }

    static string SliderText(int value) => value < 0 ? "авто" : (value / 100.0).ToString("0.00");

    void SetSlider(int i, double v)
    {
        var bar = _sliders[i].Bar;
        bar.Value = Math.Clamp(v < 0 ? bar.Minimum : (int)Math.Round(v * 100), bar.Minimum, bar.Maximum);
        _sliders[i].Value.Text = SliderText(bar.Value);
    }

    double Slider(int i) => _sliders[i].Bar.Value < 0 ? -1 : _sliders[i].Bar.Value / 100.0;

    void ReadSettingsUi()
    {
        _s.WowDir = _wowDir.Text.Trim();
        _s.Mode = _rbDx11.Checked ? RenderMode.DX11 : RenderMode.Vulkan;
        _s.FeedEnabled = _cbFeed.Checked;
        _s.CustomLook = _cbCustom.Checked;
        _s.NrStyle = _cmbStyle.SelectedIndex;
        _s.NrPreset = _cmbPreset.SelectedIndex;
        _s.NrIntensity = Slider(0);
        _s.NrLocalTone = Slider(1);
        _s.NrStructure = Slider(2);
        _s.NrSkin = Slider(3);
        _s.HoldStrength = Slider(4);
        _s.NrUiCorrection = _cbUi.Checked;
        _s.MvProvider = _cmbMv.SelectedIndex switch { 1 => 4, 2 => 2, _ => 3 };
        _s.OverlayKey = OverlayKeys[Math.Max(0, _cmbOverlayKey.SelectedIndex)].Vk;
        _s.EffectsKey = EffectsKeys[Math.Max(0, _cmbEffectsKey.SelectedIndex)].Vk;
        _s.FpsLimit = (int)_numFps.Value;
        _s.DgvWatermark = _cbWatermark.Checked;
        SaveSettings();
    }

    void ApplySettings()
    {
        ReadSettingsUi();
        var st = InstallState.Load(_s.WowDir);
        if (st?.Complete != true)
        {
            MessageBox.Show(this, "Настройки сохранены. DLSS 5 ещё не установлен — они применятся при установке.", "WoW 3.3.5a DLSS 5", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var changed = StackConfig.Apply(_s.WowDir, st.RenderMode, _s);
            var msg = "Сохранено: " + string.Join(", ", changed) + ".";
            if (Wow.IsRunning(_s.WowDir)) msg += "\n\nWoW запущен — перезапусти игру, чтобы применилось всё (вкл/выкл DLSS 5 и стабилизатор подхватятся сразу).";
            MessageBox.Show(this, msg, "WoW 3.3.5a DLSS 5", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "WoW 3.3.5a DLSS 5", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void SaveSettings()
    {
        try { _s.Save(); } catch { }
    }

    // ---------------------------------------------------------------- Диагностика

    Control BuildDiagPage()
    {
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Bg, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(0, 0, 0, 12) };
        void B(string text, Action<IReport> action, bool accent = false)
        {
            var b = new FlatBtn(text, accent) { Margin = new Padding(0, 0, 10, 8) };
            b.Click += (_, _) => { _diag.Clear(); RunTask(_diag, action); };
            buttons.Controls.Add(b);
            _actionButtons.Add(b);
        }
        B("Проверить систему", r => Diagnostics.SystemCheck(_s.WowDir, r), true);
        B("Разобрать логи игры", r => Diagnostics.Logs(_s.WowDir, r));
        B("Самотест DLSS 5", r => Diagnostics.SelfTest(_s.WowDir, r));
        B("Полная проверка", r => Diagnostics.Verify(_s.WowDir, r));
        var open = new FlatBtn("Открыть папку игры") { Margin = new Padding(0, 0, 10, 8) };
        open.Click += (_, _) => { if (Directory.Exists(_s.WowDir)) Process.Start("explorer.exe", _s.WowDir); };
        buttons.Controls.Add(open);

        var logCard = new Panel { BackColor = Theme.Card, Padding = new Padding(14, 10, 8, 10) };
        logCard.Controls.Add(_diag);
        return Page(logCard,
            Title("Диагностика"),
            new Label { Text = "«Разобрать логи» — после того, как зашёл в мир с DLSS 5. «Самотест» гоняет DLSS 5 без игры, ~30 секунд.", ForeColor = Theme.Muted, Font = Theme.Small, AutoSize = false, Height = 30 },
            buttons);
    }

    // ---------------------------------------------------------------- общее

    void RunTask(LogBox box, Action<IReport> work, bool quiet = false)
    {
        if (_busy) return;
        _busy = true;
        foreach (var b in _actionButtons) b.Enabled = false;
        _progress.Value = 0;
        _progress.Visible = !quiet;
        UseWaitCursor = !quiet;
        Task.Run(() =>
        {
            try { work(box); }
            catch (Exception ex) { box.Log(LogKind.Fail, ex.Message); }
        }).ContinueWith(_ => BeginInvoke(() =>
        {
            _busy = false;
            UseWaitCursor = false;
            _progress.Visible = false;
            foreach (var b in _actionButtons) b.Enabled = true;
            RefreshStatus();
        }));
    }

    static void StyleField(TextBox t)
    {
        t.BackColor = Theme.Field;
        t.ForeColor = Theme.Text;
        t.BorderStyle = BorderStyle.FixedSingle;
        t.Font = Theme.Base;
    }

    static void StyleRadio(RadioButton r, string text)
    {
        r.Text = text;
        r.AutoSize = true;
        r.Font = Theme.Bold;
        r.ForeColor = Theme.Text;
        r.FlatStyle = FlatStyle.Flat;
        r.Margin = new Padding(0, 4, 0, 0);
        r.Cursor = Cursors.Hand;
    }

    static void StyleCheck(CheckBox c, string text)
    {
        c.Text = text;
        c.AutoSize = true;
        c.ForeColor = Theme.Text;
        c.FlatStyle = FlatStyle.Flat;
        c.Margin = new Padding(0, 6, 0, 4);
        c.Cursor = Cursors.Hand;
    }

    static void StyleCombo(ComboBox c, params string[] items)
    {
        c.DropDownStyle = ComboBoxStyle.DropDownList;
        c.FlatStyle = FlatStyle.Flat;
        c.BackColor = Theme.Field;
        c.ForeColor = Theme.Text;
        c.Width = 320;
        c.Items.AddRange(items);
    }
}
