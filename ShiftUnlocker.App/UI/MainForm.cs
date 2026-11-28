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
            Text = "Выбор целевой ОС",
            Size = new Size(160, 32),
            Location = new Point(320, 11),
            Font = ThemeManager.StandardFont
        };

        var btnUndo = new RoundedButton
        {
            Text = "Откатить (Undo)",
            Size = new Size(130, 32),
            Location = new Point(490, 11),
            Font = ThemeManager.StandardFont
        };

        _btnElevate = new RoundedButton
        {
            Text = "Права Администратора",
            Size = new Size(180, 32),
            Location = new Point(630, 11),
            Font = ThemeManager.BoldFont,
            IsPrimary = true
        };
        _btnElevate.Click += (s, e) => ElevateApp();

        bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        _btnElevate.Visible = !isAdmin;
        btnUndo.Enabled = isAdmin;

        topBar.Controls.AddRange(new Control[] { picAvatar, lblTitle, btnTarget, btnUndo, _btnElevate });

        // левое меню
        _pnlMenu = new Panel
        {
            Dock = DockStyle.Left,
            Width = 210,
            Padding = new Padding(8, 10, 8, 10),
            BackColor = ThemeManager.BackgroundColor
        };

        string[] menuTitles =
        [
            "[Alt+1]  Обзор / Скан",
            "[Alt+2]  Автозагрузка",
            "[Alt+3]  Диспетчер задач",
            "[Alt+4]  MBR / Загрузчик",
            "[Alt+5]  Разблокировка",
            "[Alt+6]  Утилиты",
            "[Alt+7]  Редактор реестра",
            "[Alt+8]  Проводник",
            "[Alt+9]  Мастер и Журнал"
        ];

        for (int i = 0; i < 9; i++)
        {
            int index = i;
            var btn = new RoundedNavButton
            {
                Text = menuTitles[i],
                Height = 44,
                Font = ThemeManager.StandardFont
            };
            btn.Click += (s, e) => SwitchView(index);
            _menuButtons[i] = btn;
        }

        for (int i = 8; i >= 0; i--)
        {
            _pnlMenu.Controls.Add(_menuButtons[i]);
        }

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
            case Keys.Alt | Keys.U:
                _backupManager.UndoLast();
                UpdateStatusBar();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        _targetContext.Dispose();
    }
}
