using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;

namespace ShiftUnlocker.App.UI;

public class TargetSelectDialog : Form
{
    private readonly ListView _lvInstallations;
    private readonly ComboBox _cmbUserProfile;
    private readonly Label _lblProfile;
    private readonly RoundedButton _btnSelect;
    private readonly RoundedButton _btnCancel;
    private readonly Label _lblInfo;

    public WindowsInstallationInfo? SelectedInstallation { get; private set; }
    public string? SelectedUserProfile { get; private set; }

    public TargetSelectDialog()
    {
        Text = "Выбор целевой системы Windows (Online / Offline)";
        Size = new Size(720, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _lblInfo = new Label
        {
            Text = "Обнаруженные установки Windows на дисках (выберите целевую для офлайн-обработки или текущую):",
            Dock = DockStyle.Top,
            Height = 35,
            Padding = new Padding(12, 10, 12, 5)
        };

        _lvInstallations = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvInstallations);

        _lvInstallations.Columns.Add("Диск", 60);
        _lvInstallations.Columns.Add("Режим", 90);
        _lvInstallations.Columns.Add("Название ОС", 180);
        _lvInstallations.Columns.Add("Версия", 110);
        _lvInstallations.Columns.Add("Путь к Windows", 240);

        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(12, 10, 12, 10)
        };

        _lblProfile = new Label
        {
            Text = "Профиль HKCU:",
            AutoSize = true,
            Location = new Point(12, 18)
        };
        _cmbUserProfile = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(115, 14),
            Size = new Size(240, 26),
            Enabled = false
        };

        _btnSelect = new RoundedButton
        {
            Text = "Выбрать цель",
            Size = new Size(140, 32),
            Location = new Point(410, 11),
            DialogResult = DialogResult.OK,
            IsPrimary = true
        };
        _btnSelect.Click += (s, e) =>
        {
            if (_lvInstallations.SelectedItems.Count > 0)
            {
                SelectedInstallation = _lvInstallations.SelectedItems[0].Tag as WindowsInstallationInfo;
                SelectedUserProfile = _cmbUserProfile.Enabled ? _cmbUserProfile.SelectedItem?.ToString() : null;
            }
        };

        _btnCancel = new RoundedButton
        {
            Text = "Отмена",
            Size = new Size(110, 32),
            Location = new Point(560, 11),
            DialogResult = DialogResult.Cancel
        };

        bottomPanel.Controls.Add(_lblProfile);
        bottomPanel.Controls.Add(_cmbUserProfile);
        bottomPanel.Controls.Add(_btnSelect);
        bottomPanel.Controls.Add(_btnCancel);

        _lvInstallations.SelectedIndexChanged += (s, e) => UpdateProfileSelector();

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
        centerPanel.Controls.Add(_lvInstallations);

        Controls.Add(centerPanel);
        Controls.Add(bottomPanel);
        Controls.Add(_lblInfo);

        LoadInstallations();
        ThemeManager.ApplyTheme(this);
    }

    private void LoadInstallations()
    {
        _lvInstallations.Items.Clear();
        var detected = WindowsInstallationDetector.DetectInstallations();

        foreach (var inst in detected)
        {
            string mode = inst.IsCurrentOS ? "[ONLINE]" : "[OFFLINE]";
            var item = new ListViewItem(new[]
            {
                inst.DriveLetter,
                mode,
                inst.ProductName,
                inst.VersionString,
                inst.WindowsDirectory
            })
            {
                Tag = inst
            };

            _lvInstallations.Items.Add(item);
        }

        if (_lvInstallations.Items.Count > 0)
        {
            _lvInstallations.Items[0].Selected = true;
        }
        UpdateProfileSelector();
    }

    private void UpdateProfileSelector()
    {
        _cmbUserProfile.Items.Clear();
        var inst = _lvInstallations.SelectedItems.Count > 0
            ? _lvInstallations.SelectedItems[0].Tag as WindowsInstallationInfo
            : null;

        if (inst == null || inst.IsCurrentOS)
        {
            _cmbUserProfile.Items.Add("(Текущий пользователь)");
            _cmbUserProfile.SelectedIndex = 0;
            _cmbUserProfile.Enabled = false;
            return;
        }

        var profiles = OfflineTargetContext.EnumerateUserProfiles(inst.WindowsDirectory);
        if (profiles.Count == 0)
        {
            _cmbUserProfile.Items.Add("(Профили не найдены)");
            _cmbUserProfile.SelectedIndex = 0;
            _cmbUserProfile.Enabled = false;
            return;
        }

        foreach (var p in profiles) _cmbUserProfile.Items.Add(p);
        _cmbUserProfile.SelectedIndex = 0;
        _cmbUserProfile.Enabled = true;
    }
}
