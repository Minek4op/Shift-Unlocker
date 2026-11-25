using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Modules.SystemRecovery;

namespace ShiftUnlocker.App.UI;

public class DiskCleanupDialog : Form
{
    private readonly ListView _lvVolumes;
    private readonly RoundedButton _btnDeleteVol;
    private readonly RoundedButton _btnCleanDisk;
    private readonly RoundedButton _btnOpenDiskmgmt;
    private readonly RoundedButton _btnRefresh;
    private readonly RoundedButton _btnClose;

    private readonly DiskCleanupService _diskService = new();

    public DiskCleanupDialog()
    {
        Text = "Управление и удаление дисков / томов (DiskPart)";
        Size = new Size(820, 480);
        MinimumSize = new Size(720, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;

        var pnlWarning = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(12, 10, 12, 6),
            BackColor = Color.FromArgb(255, 240, 240)
        };
        var lblWarn = new Label
        {
            Dock = DockStyle.Fill,
            Text = "ВНИМАНИЕ: Операции удаления томов и очистки дисков безвозвратно уничтожают данные! Будьте осторожны.",
            ForeColor = Color.FromArgb(180, 20, 20),
            Font = ThemeManager.BoldFont
        };
        pnlWarning.Controls.Add(lblWarn);

        _lvVolumes = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvVolumes);

        _lvVolumes.Columns.Add("Том", 80);
        _lvVolumes.Columns.Add("Имя / Буква", 100);
        _lvVolumes.Columns.Add("Метка тома", 130);
        _lvVolumes.Columns.Add("ФС", 75);
        _lvVolumes.Columns.Add("Тип", 100);
        _lvVolumes.Columns.Add("Размер", 95);
        _lvVolumes.Columns.Add("Состояние", 95);
        _lvVolumes.Columns.Add("Сведения", 120);

        var bottomPanel = new RoundedFlowPanel
        {
            Dock = DockStyle.Bottom,
            Padding = new Padding(8)
        };

        _btnDeleteVol = new RoundedButton { Text = "Удалить выбранный том", Size = new Size(185, 30), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnCleanDisk = new RoundedButton { Text = "Очистить диск (Clean)", Size = new Size(170, 30), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnOpenDiskmgmt = new RoundedButton { Text = "Управление дисками (GUI)", Size = new Size(180, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnRefresh = new RoundedButton { Text = "Обновить", Size = new Size(95, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnClose = new RoundedButton { Text = "Закрыть", Size = new Size(90, 30), DialogResult = DialogResult.OK, IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };

        bottomPanel.Controls.AddRange(new Control[]
        {
            _btnDeleteVol, _btnCleanDisk, _btnOpenDiskmgmt, _btnRefresh, _btnClose
        });

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        centerPanel.Controls.Add(_lvVolumes);

        Controls.Add(centerPanel);
        Controls.Add(pnlWarning);
        Controls.Add(bottomPanel);

        AcceptButton = _btnClose;

        _btnRefresh.Click += (s, e) => LoadVolumes();
        _btnOpenDiskmgmt.Click += (s, e) => DiskCleanupService.OpenDiskManagement();
        _btnDeleteVol.Click += (s, e) => DeleteSelectedVolume();
        _btnCleanDisk.Click += (s, e) => CleanPhysicalDisk();

        LoadVolumes();
        ThemeManager.ApplyTheme(this);
    }

    private void LoadVolumes()
    {
        _lvVolumes.Items.Clear();
        var vols = _diskService.GetVolumes();
        foreach (var v in vols)
        {
            var lvi = new ListViewItem(new[]
            {
                v.VolumeIndex,
                v.DriveLetter,
                v.Label,
                v.FileSystem,
                v.Type,
                v.Size,
                v.Status,
                v.Info
            })
            {
                Tag = v
            };
            _lvVolumes.Items.Add(lvi);
        }
    }

    private void DeleteSelectedVolume()
    {
        if (_lvVolumes.SelectedItems.Count == 0 || _lvVolumes.SelectedItems[0].Tag is not DiskVolumeItem v)
        {
            MessageBox.Show("Выберите том для удаления.", "Удаление тома", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string target = !string.IsNullOrWhiteSpace(v.DriveLetter) && v.DriveLetter != "-" ? v.DriveLetter : v.VolumeIndex;
        string confirm = PromptDialog("УДАЛЕНИЕ ТОМА", $"ВНИМАНИЕ! Вы собираетесь безвозвратно удалить том:\n{v.VolumeIndex} ({v.DriveLetter} {v.Label} {v.Size})\n\nВсе данные на этом разделе будут стёрты!\nДля подтверждения введите слово УДАЛИТЬ:");

        if (!string.Equals(confirm, "УДАЛИТЬ", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Удаление отменено: проверочное слово не совпадает.", "Отмена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var res = _diskService.DeleteVolume(target);
        if (res.Success)
        {
            MessageBox.Show($"Том {target} успешно удален через DiskPart.", "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadVolumes();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка DiskPart", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CleanPhysicalDisk()
    {
        string diskStr = PromptDialog("ОЧИСТКА ДИСКА (DISKPART CLEAN)", "Введите номер физического диска для полной очистки разделов (например, 0, 1 или 2):\n\nВНИМАНИЕ: Все разделы и данные на данном накопителе будут удалены!");
        if (string.IsNullOrWhiteSpace(diskStr) || !int.TryParse(diskStr.Trim(), out int diskNum))
        {
            return;
        }

        string confirm = PromptDialog("ПОДТВЕРЖДЕНИЕ ОЧИСТКИ ДИСКА", $"Вы уверены, что хотите полностью стереть разметку Диска {diskNum}?\n\nДля подтверждения введите ОЧИСТИТЬ:");
        if (!string.Equals(confirm, "ОЧИСТИТЬ", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Очистка диска отменена.", "Отмена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var res = _diskService.CleanDisk(diskNum);
        if (res.Success)
        {
            MessageBox.Show($"Диск {diskNum} успешно очищен (таблица разделов удалена).", "Очистка", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadVolumes();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка DiskPart", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string PromptDialog(string title, string text)
    {
        using var prompt = new Form
        {
            Width = 460,
            Height = 180,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var lbl = new Label { Left = 20, Top = 15, Width = 400, Height = 60, Text = text };
        var textBox = new TextBox { Left = 20, Top = 80, Width = 400 };
        var btnOk = new RoundedButton { Text = "OK", Left = 220, Width = 90, Top = 110, DialogResult = DialogResult.OK, IsPrimary = true };
        var btnCancel = new RoundedButton { Text = "Отмена", Left = 320, Width = 90, Top = 110, DialogResult = DialogResult.Cancel };
        prompt.Controls.AddRange(new Control[] { lbl, textBox, btnOk, btnCancel });
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;
        ThemeManager.ApplyTheme(prompt);

        return prompt.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : "";
    }
}
