using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Browsers;
using ShiftUnlocker.Core.Modules.Extra;
using ShiftUnlocker.Core.Modules.FileManager;
using ShiftUnlocker.Core.Modules.Processes;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI;

public class SuScriptDialog : Form
{
    private readonly TextBox _txtScript;
    private readonly TextBox _txtLog;
    private readonly SuScriptService _service = new();

    public SuScriptDialog(ITargetContext ctx, BackupManager bm, QuarantineManager qm, Action? onChanged)
    {
        Text = "SU Script (Fixlist)";
        Size = new Size(760, 520);
        StartPosition = FormStartPosition.CenterParent;

        var top = new RoundedFlowPanel { Dock = DockStyle.Top };
        var btnDry = new RoundedButton { Text = "Dry-Run (Тест)", Size = new Size(135, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnRun = new RoundedButton { Text = "Выполнить скрипт", Size = new Size(155, 30), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        var lblHelp = new Label
        {
            Text = "Команды: RegDelete, RegSet, FileQuarantine, FileDelete, ServiceDisable, TaskDisable, ProcessKill, HostsReset, ProxyReset",
            AutoSize = true,
            ForeColor = ThemeManager.TextMutedColor,
            Margin = new Padding(4, 7, 4, 4)
        };
        top.Controls.AddRange(new Control[] { btnDry, btnRun, lblHelp });

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 240 };
        _txtScript = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = ThemeManager.MonoFont,
            Text = "# Пример скрипта SU Script:\r\n# HostsReset:\r\n# ProxyReset:\r\n# RegDelete: HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run | BadEntry\r\n"
        };
        _txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = ThemeManager.MonoFont
        };
        split.Panel1.Controls.Add(_txtScript);
        split.Panel2.Controls.Add(_txtLog);

        Controls.Add(split);
        Controls.Add(top);

        btnDry.Click += (s, e) =>
        {
            var res = _service.ExecuteScript(_txtScript.Text, ctx, bm, qm, dryRun: true);
            _txtLog.Text = $"[DRY-RUN] Команд: {res.TotalCommands}, Успешно: {res.Succeeded}, Ошибок: {res.Failed}\r\n" + string.Join("\r\n", res.LogLines);
        };

        btnRun.Click += (s, e) =>
        {
            var res = _service.ExecuteScript(_txtScript.Text, ctx, bm, qm, dryRun: false);
            _txtLog.Text = $"[ВЫПОЛНЕНО] Команд: {res.TotalCommands}, Успешно: {res.Succeeded}, Ошибок: {res.Failed}\r\n" + string.Join("\r\n", res.LogLines);
            onChanged?.Invoke();
        };

        ThemeManager.ApplyTheme(this);
    }
}

public class BrowserAuditDialog : Form
{
    private readonly ListView _lv;
    private readonly BrowserAuditService _svc = new();

    public BrowserAuditDialog(ITargetContext ctx, BackupManager bm, Action? onChanged)
    {
        Text = "Аудит браузеров (расширения, политики, ярлыки)";
        Size = new Size(880, 500);
        StartPosition = FormStartPosition.CenterParent;

        var top = new RoundedFlowPanel { Dock = DockStyle.Top };
        var btnScan = new RoundedButton { Text = "Сканировать", Size = new Size(120, 30), IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        var btnFix = new RoundedButton { Text = "Удалить выбранную политику", Size = new Size(210, 30), Margin = new Padding(2, 2, 6, 4) };
        top.Controls.AddRange(new Control[] { btnScan, btnFix });

        _lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BorderStyle = BorderStyle.None };
        ThemeManager.EnableDoubleBuffering(_lv);
        _lv.Columns.Add("Браузер", 140);
        _lv.Columns.Add("Категория", 160);
        _lv.Columns.Add("Объект", 240);
        _lv.Columns.Add("Подробности", 310);

        Controls.Add(_lv);
        Controls.Add(top);

        void LoadItems()
        {
            _lv.Items.Clear();
            foreach (var f in _svc.ScanBrowsers(ctx))
            {
                var lvi = new ListViewItem(new[] { f.Browser, f.Category, f.Title, f.Details })
                {
                    Tag = f,
                    Font = f.IsSuspicious ? ThemeManager.BoldFont : ThemeManager.StandardFont
                };
                _lv.Items.Add(lvi);
            }
        }

        btnScan.Click += (s, e) => LoadItems();
        btnFix.Click += (s, e) =>
        {
            if (_lv.SelectedItems.Count == 0 || _lv.SelectedItems[0].Tag is not BrowserFinding bf || bf.FixAction == null)
            {
                MessageBox.Show("Выберите политику или принудительное расширение с доступным исправлением.", "Браузеры", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var res = bf.FixAction(ctx, bm);
            MessageBox.Show(res.Success ? "Политика удалена (бэкап сохранён)." : res.ErrorMessage, "Браузеры", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            onChanged?.Invoke();
            LoadItems();
        };

        LoadItems();
        ThemeManager.ApplyTheme(this);
    }
}

public class AntiRootkitDialog : Form
{
    private readonly ListView _lv;
    private readonly AntiRootkitService _svc = new();

    public AntiRootkitDialog(ITargetContext ctx)
    {
        Text = "Антируткит (Cross-View процессы, драйверы, модули)";
        Size = new Size(880, 480);
        StartPosition = FormStartPosition.CenterParent;

        var top = new RoundedFlowPanel { Dock = DockStyle.Top };
        var btnScan = new RoundedButton { Text = "Повторить проверку", Size = new Size(165, 30), IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        top.Controls.Add(btnScan);

        _lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BorderStyle = BorderStyle.None };
        ThemeManager.EnableDoubleBuffering(_lv);
        _lv.Columns.Add("Категория", 200);
        _lv.Columns.Add("Объект", 180);
        _lv.Columns.Add("Описание", 320);
        _lv.Columns.Add("Путь / Ключ", 160);

        Controls.Add(_lv);
        Controls.Add(top);

        void Run()
        {
            _lv.Items.Clear();
            foreach (var f in _svc.RunCrossViewScan(ctx))
            {
                _lv.Items.Add(new ListViewItem(new[] { f.Category, f.TargetName, f.Details, f.PathOrKey }) { Tag = f });
            }
            if (_lv.Items.Count == 0)
            {
                _lv.Items.Add(new ListViewItem(new[] { "Норма", "-", "Скрытых процессов, неподписанных драйверов и фантомных модулей не обнаружено", "-" }));
            }
        }

        btnScan.Click += (s, e) => Run();
        Run();
        ThemeManager.ApplyTheme(this);
    }
}

public class DeepSearchDialog : Form
{
    private readonly ListView _lv;
    private readonly DeepSearchService _svc = new();

    public DeepSearchDialog(ITargetContext ctx, Action<string>? onJumpToRegistry)
    {
        Text = "Глубокий поиск по реестру и диску";
        Size = new Size(900, 500);
        StartPosition = FormStartPosition.CenterParent;

        var top = new RoundedFlowPanel { Dock = DockStyle.Top };
        var chkReg = new CheckBox { Text = "Искать в реестре", Checked = true, AutoSize = true, Margin = new Padding(4, 7, 8, 4) };
        var chkDisk = new CheckBox { Text = "Искать на диске (Temp/Startup/Scripts)", Checked = true, AutoSize = true, Margin = new Padding(4, 7, 8, 4) };
        var btnSearch = new RoundedButton { Text = "Начать поиск", Size = new Size(135, 30), IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        var btnJump = new RoundedButton { Text = "Открыть в редакторе реестра", Size = new Size(215, 30), Margin = new Padding(2, 2, 6, 4) };
        top.Controls.AddRange(new Control[] { chkReg, chkDisk, btnSearch, btnJump });

        _lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BorderStyle = BorderStyle.None };
        ThemeManager.EnableDoubleBuffering(_lv);
        _lv.Columns.Add("Источник", 120);
        _lv.Columns.Add("Шаблон", 160);
        _lv.Columns.Add("Расположение", 280);
        _lv.Columns.Add("Совпадение", 300);

        Controls.Add(_lv);
        Controls.Add(top);

        void Search()
        {
            _lv.Items.Clear();
            foreach (var m in _svc.SearchRegistryAndDisk(ctx, chkReg.Checked, chkDisk.Checked))
            {
                _lv.Items.Add(new ListViewItem(new[] { m.SourceType, m.PatternName, m.Location, m.MatchedText }) { Tag = m });
            }
        }

        btnSearch.Click += (s, e) => Search();
        btnJump.Click += (s, e) =>
        {
            if (_lv.SelectedItems.Count > 0 && _lv.SelectedItems[0].Tag is DeepSearchMatch m && m.SourceType == "Реестр")
            {
                onJumpToRegistry?.Invoke(m.Location);
                Close();
            }
        };

        Search();
        ThemeManager.ApplyTheme(this);
    }
}

public class DataRescueDialog : Form
{
    private readonly DataRescueService _svc = new();

    public DataRescueDialog(ITargetContext ctx)
    {
        Text = "Мастер спасения данных";
        Size = new Size(600, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var lblSrc = new Label { Left = 16, Top = 16, Width = 550, Text = "Профиль пользователя (источник):" };
        var cbProfile = new ComboBox { Left = 16, Top = 38, Width = 550, DropDownStyle = ComboBoxStyle.DropDownList };
        string usersRoot = Path.Combine(ctx.TargetRootPath, "Users");
        if (Directory.Exists(usersRoot))
        {
            foreach (var d in Directory.GetDirectories(usersRoot))
            {
                string n = Path.GetFileName(d);
                if (!n.Equals("Public", StringComparison.OrdinalIgnoreCase) && !n.Equals("Default", StringComparison.OrdinalIgnoreCase))
                    cbProfile.Items.Add(d);
            }
        }
        if (cbProfile.Items.Count > 0) cbProfile.SelectedIndex = 0;

        var chkDesk = new CheckBox { Left = 16, Top = 74, Text = "Рабочий стол (Desktop)", Checked = true, AutoSize = true };
        var chkDocs = new CheckBox { Left = 220, Top = 74, Text = "Документы (Documents)", Checked = true, AutoSize = true };
        var chkDown = new CheckBox { Left = 16, Top = 100, Text = "Загрузки (Downloads)", Checked = true, AutoSize = true };
        var chkPic = new CheckBox { Left = 220, Top = 100, Text = "Изображения (Pictures)", Checked = false, AutoSize = true };
        var chkHash = new CheckBox { Left = 16, Top = 130, Text = "Проверять SHA-256 после копирования", Checked = true, AutoSize = true };

        var lblDst = new Label { Left = 16, Top = 165, Width = 550, Text = "Целевой диск / папка для спасения:" };
        var txtDst = new TextBox { Left = 16, Top = 188, Width = 440, Text = Path.GetTempPath() };
        var btnBrowse = new RoundedButton { Left = 466, Top = 186, Size = new Size(100, 28), Text = "Обзор..." };

        var pb = new ProgressBar { Left = 16, Top = 230, Width = 550, Height = 22 };
        var lblStat = new Label { Left = 16, Top = 260, Width = 550, Text = "Готово к копированию." };
        var btnStart = new RoundedButton { Left = 396, Top = 295, Size = new Size(170, 32), Text = "Начать спасение", IsPrimary = true };

        Controls.AddRange(new Control[] { lblSrc, cbProfile, chkDesk, chkDocs, chkDown, chkPic, chkHash, lblDst, txtDst, btnBrowse, pb, lblStat, btnStart });

        btnBrowse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK) txtDst.Text = fbd.SelectedPath;
        };

        btnStart.Click += async (s, e) =>
        {
            if (cbProfile.SelectedItem is not string srcProf || !Directory.Exists(txtDst.Text)) return;
            btnStart.Enabled = false;
            var prog = new Progress<(string File, int Percent)>(p =>
            {
                pb.Value = Math.Clamp(p.Percent, 0, 100);
                lblStat.Text = $"({p.Percent}%) {Path.GetFileName(p.File)}";
            });

            var rep = await _svc.RescueUserFoldersAsync(srcProf, txtDst.Text, chkDesk.Checked, chkDocs.Checked, chkDown.Checked, chkPic.Checked, chkHash.Checked, prog);
            btnStart.Enabled = true;
            MessageBox.Show($"Скопировано файлов: {rep.FilesCopied}\nПроверено SHA-256: {rep.FilesVerified}\nОшибок: {rep.Failed}", "Спасение данных", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        ThemeManager.ApplyTheme(this);
    }
}
