using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.App.UI.Views;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI;

public class MainForm : Form
{
    private ITargetContext _targetContext;
    private readonly BackupManager _backupManager = new();
    private readonly QuarantineManager _quarantineManager = new();

    private readonly Panel _pnlMenu;
    private readonly Panel _pnlContent;

    private readonly RoundedNavButton[] _menuButtons = new RoundedNavButton[9];
    private readonly UserControl[] _views = new UserControl[9];
    private int _currentViewIndex = 0;

    private readonly RoundedButton _btnElevate;
    private readonly string _processName;

    public MainForm()
    {
        _processName = Process.GetCurrentProcess().ProcessName;
        Text = _processName;
        Size = new Size(1180, 740);
        MinimumSize = new Size(940, 620);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        try
        {
            using var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("ShiftUnlocker.App.app.ico");
            if (iconStream != null)
            {
                Icon = new Icon(iconStream);
            }
            else if (File.Exists("app.ico"))
            {
                Icon = new Icon("app.ico");
            }
        }
        catch { }

        // в WinPE сразу ищем офлайн-систему
        bool inWinPE = WindowsInstallationDetector.IsRunningInWinPE();
        var detected = WindowsInstallationDetector.DetectInstallations();
        var offlineTarget = detected.FirstOrDefault(d => !d.IsCurrentOS);

        if (inWinPE && offlineTarget != null)
        {
            _targetContext = new OfflineTargetContext(offlineTarget.WindowsDirectory);
        }
        else
        {
            _targetContext = new OnlineTargetContext();
        }

        // верхняя панель
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = ThemeManager.SurfaceColor
        };

        Image? avatarImg = null;
        try
        {
            using var imgStream = typeof(MainForm).Assembly.GetManifestResourceStream("ShiftUnlocker.App.app.png");
            if (imgStream != null)
            {
                avatarImg = Image.FromStream(imgStream);
            }
            else if (File.Exists("app.png"))
            {
                avatarImg = Image.FromFile("app.png");
            }
        }
        catch { }

        var picAvatar = new PictureBox
        {
            Size = new Size(32, 32),
            Location = new Point(12, 11),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = avatarImg,
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = "SHIFT UNLOCKER",
            Font = ThemeManager.LargeBoldFont,
            Location = new Point(50, 16),
            AutoSize = true
        };

        var btnTarget = new RoundedButton
        {
            Text = LangManager.T("btn_target"),
            Size = new Size(145, 32),
            Location = new Point(230, 11),
            Font = ThemeManager.StandardFont
        };

        var btnUndo = new RoundedButton
        {
            Text = LangManager.T("btn_undo"),
            Size = new Size(120, 32),
            Location = new Point(383, 11),
            Font = ThemeManager.StandardFont
        };

        var btnPalette = new RoundedButton
        {
            Text = LangManager.T("btn_palette"),
            Size = new Size(135, 32),
            Location = new Point(511, 11),
            Font = ThemeManager.StandardFont
        };
        btnPalette.Click += (s, e) => ShowCommandPalette();

        var btnHelp = new RoundedButton
        {
            Text = LangManager.T("btn_help"),
            Size = new Size(105, 32),
            Location = new Point(654, 11),
            Font = ThemeManager.StandardFont
        };
        btnHelp.Click += (s, e) => ShowContextHelp();

        var btnLang = new RoundedButton
        {
            Text = "RU / EN",
            Size = new Size(72, 32),
            Location = new Point(767, 11),
            Font = ThemeManager.BoldFont
        };
        btnLang.Click += (s, e) => LangManager.ToggleLanguage();

        _btnElevate = new RoundedButton
        {
            Text = LangManager.T("btn_elevate"),
            Size = new Size(175, 32),
            Location = new Point(847, 11),
            Font = ThemeManager.BoldFont,
            IsPrimary = true
        };
        _btnElevate.Click += (s, e) => ElevateApp();

        bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        _btnElevate.Visible = !isAdmin;
        btnUndo.Enabled = isAdmin;

        topBar.Controls.AddRange(new Control[] { picAvatar, lblTitle, btnTarget, btnUndo, btnPalette, btnHelp, btnLang, _btnElevate });

        // левое меню с группировкой по смыслу
        _pnlMenu = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            Padding = new Padding(8, 6, 8, 6),
            AutoScroll = true,
            BackColor = ThemeManager.BackgroundColor
        };

        string[] menuKeys =
        [
            "nav_scan", "nav_autorun", "nav_proc", "nav_mbr",
            "nav_restrict", "nav_tools", "nav_reg", "nav_files", "nav_wizard"
        ];

        for (int i = 0; i < 9; i++)
        {
            int index = i;
            var btn = new RoundedNavButton
            {
                Text = LangManager.T(menuKeys[i]),
                Height = 36,
                Font = ThemeManager.StandardFont
            };
            btn.Click += (s, e) => SwitchView(index);
            _menuButtons[i] = btn;
        }

        Label MakeGroupHeader(string key) => new Label
        {
            Text = LangManager.T(key),
            Dock = DockStyle.Top,
            Height = 22,
            Padding = new Padding(6, 6, 0, 0),
            Font = new Font(ThemeManager.BoldFont.FontFamily, 7.5f, FontStyle.Bold),
            ForeColor = ThemeManager.TextMutedColor,
            Tag = key
        };

        var grpDiag = MakeGroupHeader("menu_grp_diag");
        var grpHeal = MakeGroupHeader("menu_grp_heal");
        var grpSys = MakeGroupHeader("menu_grp_sys");
        var grpDisk = MakeGroupHeader("menu_grp_disk");
        var grpTools = MakeGroupHeader("menu_grp_tools");

        // Dock=Top добавляется в обратном порядке (снизу вверх)
        Control[] menuOrderBottomToTop =
        [
            _menuButtons[7], // Проводник
            _menuButtons[5], // Утилиты
            grpTools,
            _menuButtons[3], // MBR / Загрузчик
            grpDisk,
            _menuButtons[6], // Редактор реестра
            _menuButtons[2], // Диспетчер задач
            grpSys,
            _menuButtons[8], // Мастер и Журнал
            _menuButtons[4], // Разблокировка
            grpHeal,
            _menuButtons[1], // Автозагрузка
            _menuButtons[0], // Обзор / Скан
            grpDiag
        ];

        foreach (var ctrl in menuOrderBottomToTop)
        {
            _pnlMenu.Controls.Add(ctrl);
        }

        LangManager.LanguageChanged += () =>
        {
            btnTarget.Text = LangManager.T("btn_target");
            btnUndo.Text = LangManager.T("btn_undo");
            btnPalette.Text = LangManager.T("btn_palette");
            btnHelp.Text = LangManager.T("btn_help");
            _btnElevate.Text = LangManager.T("btn_elevate");
            for (int i = 0; i < 9; i++) _menuButtons[i].Text = LangManager.T(menuKeys[i]);
            foreach (Control c in _pnlMenu.Controls)
            {
                if (c is Label lbl && lbl.Tag is string k) lbl.Text = LangManager.T(k);
            }
        };

        // основная область
        _pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            BackColor = ThemeManager.BackgroundColor
        };

        Controls.Add(_pnlContent);
        Controls.Add(_pnlMenu);
        Controls.Add(topBar);

        // инициализация вкладок
        InitViews();

        btnTarget.Click += (s, e) => ShowTargetDialog();
        btnUndo.Click += (s, e) =>
        {
            _backupManager.UndoLast();
            UpdateStatusBar();
            if (_views[8] is ToolsLogView tlv) tlv.RefreshLogs();
        };

        UpdateStatusBar();
        SwitchView(0);
        ThemeManager.ApplyTheme(this);
    }

    private void ElevateApp()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось повысить привилегии: {ex.Message}", "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void InitViews()
    {
        var v1 = new QuickScanView
        {
            GetContext = () => _targetContext,
            GetBackupManager = () => _backupManager,
            OnStateChanged = UpdateStatusBar
        };

        var v2 = new AutorunView
        {
            GetContext = () => _targetContext,
            GetBackupManager = () => _backupManager,
            GetQuarantineManager = () => _quarantineManager,
            OnStateChanged = UpdateStatusBar
        };

        var v3 = new ProcessView
        {
            GetContext = () => _targetContext,
            GetBackupManager = () => _backupManager
        };

        var v4 = new MbrBootView
        {
            GetContext = () => _targetContext,
            GetBackupManager = () => _backupManager
        };

        var v5 = new RestrictionsView
        {
            GetContext = () => _targetContext,
            GetBackupManager = () => _backupManager,
            OnStateChanged = UpdateStatusBar
        };

        var v6 = new LauncherView
        {
            GetContext = () => _targetContext
        };

        var v7 = new RegEditorView
        {
            GetContext = () => _targetContext,
            GetBackupManager = () => _backupManager,
            OnStateChanged = UpdateStatusBar
        };

        var v8 = new FileManagerView
        {
            GetContext = () => _targetContext,
            GetBackupManager = () => _backupManager,
            GetQuarantineManager = () => _quarantineManager,
            OnStateChanged = UpdateStatusBar
        };

        var v9 = new ToolsLogView
        {
            GetContext = () => _targetContext,
            GetBackupManager = () => _backupManager,
            OnStateChanged = UpdateStatusBar
        };

        _views[0] = v1;
        _views[1] = v2;
        _views[2] = v3;
        _views[3] = v4;
        _views[4] = v5;
        _views[5] = v6;
        _views[6] = v7;
        _views[7] = v8;
        _views[8] = v9;

        v1.OnScenarioSelected = scenario =>
        {
            switch (scenario)
            {
                case "tools_blocked":
                    SwitchView(4);
                    v5.SelectCategory("Инструменты");
                    break;
                case "usb_shortcuts":
                    SwitchView(4);
                    v5.SelectCategory("Накопители");
                    break;
                case "banner":
                    SwitchView(1);
                    v2.SetSuspiciousFilter();
                    break;
                case "encrypted":
                    SwitchView(7);
                    break;
                case "noboot":
                    SwitchView(3);
                    break;
            }
        };
    }

    public void SwitchView(int index)
    {
        if (index < 0 || index >= _views.Length) return;
        _currentViewIndex = index;

        for (int i = 0; i < 9; i++)
        {
            _menuButtons[i].IsActive = (i == index);
            _menuButtons[i].Invalidate();
        }

        _pnlContent.Controls.Clear();
        var view = _views[index];
        view.Dock = DockStyle.Fill;
        _pnlContent.Controls.Add(view);

        // обновляем данные при переключении вкладки
        if (view is QuickScanView qv) { _ = qv.EnsureScannedOnceAsync(); }
        else if (view is AutorunView av) { _ = av.EnsureLoadedOnceAsync(); }
        else if (view is RestrictionsView rv) rv.RefreshRestrictions();
        else if (view is ProcessView pv) { _ = pv.RefreshProcessesAsync(); }
        else if (view is ToolsLogView tlv) tlv.RefreshLogs();
    }

    private void UpdateStatusBar()
    {
        Text = _processName;

        bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        _btnElevate.Visible = !isAdmin;
    }

    private void ShowTargetDialog()
    {
        using var dlg = new TargetSelectDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedInstallation != null)
        {
            var inst = dlg.SelectedInstallation;
            _targetContext.Dispose(); // Unload old offline hives

            if (inst.IsCurrentOS)
            {
                _targetContext = new OnlineTargetContext();
            }
            else
            {
                _targetContext = new OfflineTargetContext(inst.WindowsDirectory);
            }

            if (_views[0] is QuickScanView qv) qv.ResetScanState();
            if (_views[1] is AutorunView av) av.ResetLoadState();

            UpdateStatusBar();
            SwitchView(_currentViewIndex);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Alt | Keys.D1: SwitchView(0); return true;
            case Keys.Alt | Keys.D2: SwitchView(1); return true;
            case Keys.Alt | Keys.D3: SwitchView(2); return true;
            case Keys.Alt | Keys.D4: SwitchView(3); return true;
            case Keys.Alt | Keys.D5: SwitchView(4); return true;
            case Keys.Alt | Keys.D6: SwitchView(5); return true;
            case Keys.Alt | Keys.D7: SwitchView(6); return true;
            case Keys.Alt | Keys.D8: SwitchView(7); return true;
            case Keys.Alt | Keys.D9: SwitchView(8); return true;
            case Keys.Alt | Keys.T: ShowTargetDialog(); return true;
            case Keys.Control | Keys.K: ShowCommandPalette(); return true;
            case Keys.F1: ShowContextHelp(); return true;
            case Keys.Alt | Keys.U:
                _backupManager.UndoLast();
                UpdateStatusBar();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ShowContextHelp()
    {
        var (title, body) = LangManager.GetHelpForView(_currentViewIndex);
        MessageBox.Show(this, body, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowCommandPalette()
    {
        var commands = new List<PaletteCommand>
        {
            new(LangManager.T("nav_scan"), LangManager.T("menu_grp_diag"), () => SwitchView(0)),
            new(LangManager.T("nav_autorun"), LangManager.T("menu_grp_diag"), () => SwitchView(1)),
            new(LangManager.T("nav_proc"), LangManager.T("menu_grp_sys"), () => SwitchView(2)),
            new(LangManager.T("nav_mbr"), LangManager.T("menu_grp_disk"), () => SwitchView(3)),
            new(LangManager.T("nav_restrict"), LangManager.T("menu_grp_heal"), () => SwitchView(4)),
            new(LangManager.T("nav_tools"), LangManager.T("menu_grp_tools"), () => SwitchView(5)),
            new(LangManager.T("nav_reg"), LangManager.T("menu_grp_sys"), () => SwitchView(6)),
            new(LangManager.T("nav_files"), LangManager.T("menu_grp_tools"), () => SwitchView(7)),
            new(LangManager.T("nav_wizard"), LangManager.T("menu_grp_heal"), () => SwitchView(8)),
            new(LangManager.T("btn_target"), "Система", ShowTargetDialog),
            new(LangManager.T("btn_undo"), "Безопасность", () => { _backupManager.UndoLast(); UpdateStatusBar(); }),
            new(LangManager.T("btn_help"), "Помощь", ShowContextHelp),
            new("Переключить язык (RU / EN)", "Настройки", LangManager.ToggleLanguage),
            new("Удаление и очистка дисков (DiskPart)", "Диски", () => new DiskCleanupDialog().ShowDialog(this))
        };

        using var dlg = new CommandPaletteDialog(commands);
        dlg.ShowDialog(this);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        _targetContext.Dispose();
    }
}
