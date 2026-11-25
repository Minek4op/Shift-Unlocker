using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.MbrBoot;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI.Views;

public class MbrBootView : UserControl
{
    private readonly ComboBox _cbDrives;
    private readonly RoundedButton _btnReadMbr;
    private readonly RoundedButton _btnBackupMbr;
    private readonly RoundedButton _btnRestoreMbr;
    private readonly TextBox _txtHexDump;
    private readonly ListBox _lbAnomalies;
    private readonly ListView _lvPartitions;

    private readonly RoundedButton _btnBootsect;
    private readonly RoundedButton _btnBcdboot;
    private readonly RoundedButton _btnSfc;
    private readonly RoundedButton _btnDism;
    private readonly RoundedButton _btnDiskCleanup;

    private readonly MbrBootService _mbrService = new();
    private List<PhysicalDriveInfo> _drives = new();
    private MbrSectorInfo? _currentMbr;

    public Func<ITargetContext>? GetContext { get; set; }
    public Func<BackupManager>? GetBackupManager { get; set; }

    public MbrBootView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var topPanel = new RoundedFlowPanel { Dock = DockStyle.Top };

        var lblDrive = new Label { Text = "Физический накопитель:", AutoSize = true, Margin = new Padding(2, 7, 4, 4) };
        _cbDrives = new ComboBox { Size = new Size(180, 26), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2, 4, 6, 4) };
        _btnReadMbr = new RoundedButton { Text = "Прочитать сектор 0 (MBR)", Size = new Size(190, 30), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };

        _btnBackupMbr = new RoundedButton { Text = "Бэкап MBR в файл", Size = new Size(150, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnRestoreMbr = new RoundedButton { Text = "Восстановить MBR...", Size = new Size(160, 30), Margin = new Padding(2, 2, 6, 4) };

        // системные утилиты
        _btnBootsect = new RoundedButton { Text = "bootsect /mbr", Size = new Size(130, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnBcdboot = new RoundedButton { Text = "bcdboot (ESP/MBR)", Size = new Size(150, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnSfc = new RoundedButton { Text = "Offline SFC", Size = new Size(120, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnDism = new RoundedButton { Text = "Offline DISM", Size = new Size(120, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnDiskCleanup = new RoundedButton { Text = "💾 Удаление дисков...", Size = new Size(185, 30), Margin = new Padding(2, 2, 6, 4) };

        topPanel.Controls.AddRange(new Control[]
        {
            lblDrive, _cbDrives, _btnReadMbr, _btnBackupMbr, _btnRestoreMbr,
            _btnBootsect, _btnBcdboot, _btnSfc, _btnDism, _btnDiskCleanup
        });

        // слева hex-дамп, справа разбор разделов
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 450
        };

        var hexPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var lblHex = new Label { Text = "Шестнадцатеричный дамп сектора 0 (512 байт MBR):", Dock = DockStyle.Top, Height = 24 };
        _txtHexDump = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = ThemeManager.MonoFont,
            BorderStyle = BorderStyle.None
        };
        hexPanel.Controls.Add(_txtHexDump);
        hexPanel.Controls.Add(lblHex);

        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var lblAnomalies = new Label { Text = "Анализ аномалий и угроз загрузчика:", Dock = DockStyle.Top, Height = 24 };
        _lbAnomalies = new ListBox { Dock = DockStyle.Top, Height = 130, Font = ThemeManager.BoldFont, BorderStyle = BorderStyle.None };

        var lblParts = new Label { Text = "Таблица разделов MBR / GPT:", Dock = DockStyle.Top, Height = 24 };
        _lvPartitions = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvPartitions);
        _lvPartitions.Columns.Add("№", 40);
        _lvPartitions.Columns.Add("Акт.?", 60);
        _lvPartitions.Columns.Add("Тип", 140);
        _lvPartitions.Columns.Add("Начальный LBA", 110);
        _lvPartitions.Columns.Add("Размер", 100);

        rightPanel.Controls.Add(_lvPartitions);
        rightPanel.Controls.Add(lblParts);
        rightPanel.Controls.Add(_lbAnomalies);
        rightPanel.Controls.Add(lblAnomalies);

        split.Panel1.Controls.Add(hexPanel);
        split.Panel2.Controls.Add(rightPanel);

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        centerPanel.Controls.Add(split);

        Controls.Add(centerPanel);
        Controls.Add(topPanel);

        RefreshDrives();

        _btnReadMbr.Click += (s, e) => ReadCurrentMbr();
        _btnBackupMbr.Click += (s, e) => BackupCurrentMbr();
        _btnRestoreMbr.Click += (s, e) => RestoreMbrWithPrompt();
        _btnBootsect.Click += async (s, e) => await RunBootsect();
        _btnBcdboot.Click += async (s, e) => await RunBcdboot();
        _btnSfc.Click += async (s, e) => await RunSfc();
        _btnDism.Click += async (s, e) => await RunDism();
        _btnDiskCleanup.Click += (s, e) => new DiskCleanupDialog().ShowDialog(this);

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _btnReadMbr.Enabled = isAdmin;
        _btnBackupMbr.Enabled = isAdmin;
        _btnRestoreMbr.Enabled = isAdmin;
        _btnBootsect.Enabled = isAdmin;
        _btnBcdboot.Enabled = isAdmin;
        _btnSfc.Enabled = isAdmin;
        _btnDism.Enabled = isAdmin;
        _btnDiskCleanup.Enabled = isAdmin;
    }

    public void RefreshDrives()
    {
        _cbDrives.Items.Clear();
        _drives = _mbrService.EnumeratePhysicalDrives();
        foreach (var d in _drives)
        {
            _cbDrives.Items.Add(d.FriendlyName);
        }
        if (_cbDrives.Items.Count > 0)
        {
            _cbDrives.SelectedIndex = 0;
        }
    }

    private void ReadCurrentMbr()
    {
        if (_cbDrives.SelectedIndex < 0 || _cbDrives.SelectedIndex >= _drives.Count) return;
        int driveIdx = _drives[_cbDrives.SelectedIndex].DriveIndex;

        var res = _mbrService.ReadMbrSector(driveIdx);
        if (!res.Success || res.Value == null)
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка чтения MBR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _currentMbr = res.Value;
        _txtHexDump.Text = _currentMbr.HexDump;

        _lbAnomalies.Items.Clear();
        if (_currentMbr.Anomalies.Count == 0)
        {
            _lbAnomalies.Items.Add("Аномалий и подозрительных строк не обнаружено.");
            _lbAnomalies.Items.Add($"Сигнатура диска: 0x{_currentMbr.DiskSignature:X8}");
            _lbAnomalies.Items.Add("Загрузочная сигнатура 0x55AA присутствует.");
        }
        else
        {
            foreach (var a in _currentMbr.Anomalies)
            {
                _lbAnomalies.Items.Add(a);
            }
        }

        _lvPartitions.Items.Clear();
        foreach (var p in _currentMbr.Partitions)
        {
            double mb = p.SizeBytes / (1024.0 * 1024.0);
            string szStr = mb >= 1024 ? $"{mb / 1024.0:F1} ГБ" : $"{mb:F0} МБ";

            var item = new ListViewItem(new[]
            {
                p.Index.ToString(),
                p.IsBootable ? "[*] ДА" : "Нет",
                p.PartitionTypeName,
                p.StartLba.ToString(),
                szStr
            });
            _lvPartitions.Items.Add(item);
        }
    }

    private void BackupCurrentMbr()
    {
        if (_cbDrives.SelectedIndex < 0) return;
        int driveIdx = _drives[_cbDrives.SelectedIndex].DriveIndex;
        var bm = GetBackupManager?.Invoke();
        if (bm == null) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "Резервная копия MBR (*.bin)|*.bin",
            FileName = $"MBR_Drive{driveIdx}_{DateTime.Now:yyyyMMdd_HHmmss}.bin"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var res = _mbrService.BackupMbrSector(driveIdx, sfd.FileName, bm);
            if (res.Success)
            {
                MessageBox.Show($"Бэкап успешно сохранен:\n{sfd.FileName}", "Бэкап MBR", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(res.ErrorMessage, "Ошибка бэкапа", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RestoreMbrWithPrompt()
    {
        if (_cbDrives.SelectedIndex < 0) return;
        int driveIdx = _drives[_cbDrives.SelectedIndex].DriveIndex;
        var bm = GetBackupManager?.Invoke();
        if (bm == null) return;

        var confirm1 = MessageBox.Show(
            $"ВНИМАНИЕ! Перезапись MBR физического диска {driveIdx} может сделать систему незагружаемой в случае неверного файла!\n\nВы уверены, что хотите продолжить?",
            "ОПАСНАЯ ОПЕРАЦИЯ: Двойное подтверждение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm1 != DialogResult.Yes) return;

        using var ofd = new OpenFileDialog
        {
            Filter = "Файл бэкапа MBR (*.bin)|*.bin",
            Title = "Выберите файл бэкапа для восстановления MBR"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var res = _mbrService.RestoreMbrFromFile(driveIdx, ofd.FileName, bm);
            if (res.Success)
            {
                MessageBox.Show("MBR успешно восстановлен из файла. Сделайте повторное чтение для проверки.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ReadCurrentMbr();
            }
            else
            {
                MessageBox.Show(res.ErrorMessage, "Ошибка восстановления MBR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task RunBootsect()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        var res = await _mbrService.RunBootsectAsync(ctx.TargetRootPath);
        MessageBox.Show(res.Success ? "Команда bootsect /mbr успешно выполнена." : res.ErrorMessage, "bootsect", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private async Task RunBcdboot()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        var res = await _mbrService.RunBcdbootAsync(ctx.WindowsDirectory, null);
        MessageBox.Show(res.Success ? "Команда bcdboot успешно выполнена." : res.ErrorMessage, "bcdboot", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private async Task RunSfc()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        var res = await _mbrService.RunSfcOfflineAsync(ctx.WindowsDirectory, ctx.TargetRootPath);
        MessageBox.Show(res.Success ? "SFC офлайн сканирование завершено успешно." : res.ErrorMessage, "SFC", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private async Task RunDism()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        var res = await _mbrService.RunDismOfflineAsync(ctx.WindowsDirectory);
        MessageBox.Show(res.Success ? "DISM восстановление завершено успешно." : res.ErrorMessage, "DISM", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }
}
