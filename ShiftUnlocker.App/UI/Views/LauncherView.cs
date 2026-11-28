using System.Diagnostics;
using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Modules.Launcher;

namespace ShiftUnlocker.App.UI.Views;

public class LauncherView : UserControl
{
    private readonly ListView _lvUtilities;
    private readonly ListViewGroup _grpBuiltIn;
    private readonly ListViewGroup _grpTools;
    private readonly ListViewGroup _grpCustom;
    private readonly RoundedButton _btnLaunch;
    private readonly RoundedButton _btnAddExe;
    private readonly RoundedButton _btnRemoveExe;
    private readonly RoundedButton _btnOpenToolsFolder;
    private readonly RoundedButton _btnRefresh;
    private readonly CheckBox _chkRandomName;

    private readonly RoundedButton _btnSfc;
    private readonly RoundedButton _btnDiskCleanup;
    private readonly RoundedButton _btnLogoff;

    private readonly LauncherService _launcherService = new(resourceAssembly: typeof(LauncherView).Assembly);
    private List<UtilityItem> _items = new();

    public Func<ShiftUnlocker.Core.Context.ITargetContext>? GetContext { get; set; }

    public LauncherView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var topPanel = new RoundedFlowPanel { Dock = DockStyle.Top };

        _btnLaunch = new RoundedButton { Text = "Запустить утилиту", Size = new Size(160, 32), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(2, 2, 8, 4) };
        _chkRandomName = new CheckBox { Text = "Запуск под случайным именем", AutoSize = true, Checked = true, Margin = new Padding(4, 7, 8, 4) };
        _btnAddExe = new RoundedButton { Text = "Добавить .exe...", Size = new Size(130, 32), Margin = new Padding(2, 2, 8, 4) };
        _btnRemoveExe = new RoundedButton { Text = "Удалить добавленную", Size = new Size(165, 32), Margin = new Padding(2, 2, 8, 4) };
        _btnOpenToolsFolder = new RoundedButton { Text = "Папка Tools", Size = new Size(110, 32), Margin = new Padding(2, 2, 8, 4) };
        _btnRefresh = new RoundedButton { Text = "Обновить", Size = new Size(95, 32), Margin = new Padding(2, 2, 8, 4) };

        _btnSfc = new RoundedButton { Text = "sfc /scannow", Size = new Size(125, 32), Margin = new Padding(2, 2, 8, 4) };
        _btnDiskCleanup = new RoundedButton { Text = "Удаление дисков...", Size = new Size(155, 32), Margin = new Padding(2, 2, 8, 4) };
        _btnLogoff = new RoundedButton { Text = "Выйти из пользователя", Size = new Size(170, 32), Margin = new Padding(2, 2, 8, 4) };

        topPanel.Controls.AddRange(new Control[]
        {
            _btnLaunch, _chkRandomName, _btnAddExe, _btnRemoveExe, _btnOpenToolsFolder, _btnRefresh,
            _btnSfc, _btnDiskCleanup, _btnLogoff
        });

        _grpBuiltIn = new ListViewGroup("Встроенные системные утилиты", HorizontalAlignment.Left);
        _grpTools = new ListViewGroup("Обнаруженные утилиты (папка Tools)", HorizontalAlignment.Left);
        _grpCustom = new ListViewGroup("Добавленные пользователем", HorizontalAlignment.Left);

        _lvUtilities = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            ShowGroups = true
        };
        ThemeManager.EnableDoubleBuffering(_lvUtilities);
        _lvUtilities.Groups.AddRange(new[] { _grpBuiltIn, _grpTools, _grpCustom });

        _lvUtilities.Columns.Add("Статус", 90);
        _lvUtilities.Columns.Add("Название утилиты", 200);
        _lvUtilities.Columns.Add("Категория", 110);
        _lvUtilities.Columns.Add("Описание", 280);
        _lvUtilities.Columns.Add("Путь к исполняемому файлу", 350);

        var ctxMenu = new ContextMenuStrip();
        ctxMenu.Items.Add("Запустить утилиту", null, (s, e) => LaunchSelected());
        ctxMenu.Items.Add("Удалить добавленную утилиту", null, (s, e) => RemoveSelectedCustomExe());
        _lvUtilities.ContextMenuStrip = ctxMenu;

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        centerPanel.Controls.Add(_lvUtilities);

        Controls.Add(centerPanel);
        Controls.Add(topPanel);

        RefreshList();

        _btnRefresh.Click += (s, e) => RefreshList();
        _btnLaunch.Click += (s, e) => LaunchSelected();
        _btnAddExe.Click += (s, e) => AddCustomExe();
        _btnRemoveExe.Click += (s, e) => RemoveSelectedCustomExe();
        _btnOpenToolsFolder.Click += (s, e) => OpenToolsFolder();
        _lvUtilities.DoubleClick += (s, e) => LaunchSelected();

        _btnSfc.Click += (s, e) => RunSfc();
        _btnDiskCleanup.Click += (s, e) => new DiskCleanupDialog().ShowDialog(this);
        _btnLogoff.Click += (s, e) => RunLogoff();

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _btnSfc.Enabled = isAdmin;
        _btnDiskCleanup.Enabled = isAdmin;
    }

    private void RunSfc()
    {
        var ctx = GetContext?.Invoke();
        string args = (ctx != null && !ctx.IsOnline)
            ? $"/k sfc /scannow /offbootdir=\"{ctx.TargetRootPath}\" /offwindir=\"{ctx.WindowsDirectory}\""
            : "/k sfc /scannow";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка запуска SFC: {ex.Message}", "SFC", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RunLogoff()
    {
        if (MessageBox.Show("Завершить текущий сеанс пользователя Windows (Logoff)?", "Выход из системы", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "shutdown.exe", Arguments = "/l /f", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public void RefreshList()
    {
        _lvUtilities.Items.Clear();
        _items = _launcherService.GetAllUtilities();

        foreach (var u in _items)
        {
            string status = u.ExistsOnDisk ? "Готов" : "Не найден";
            var grp = u.IsBuiltIn ? _grpBuiltIn : (u.IsUserAdded ? _grpCustom : _grpTools);

            var item = new ListViewItem(new[]
            {
                status,
                u.Name,
                u.Category,
                u.Description,
                u.ExecutablePath
            })
            {
                Tag = u,
                Group = grp,
                Font = u.ExistsOnDisk ? ThemeManager.BoldFont : ThemeManager.StandardFont
            };

            _lvUtilities.Items.Add(item);
        }
    }

    private void LaunchSelected()
    {
        if (_lvUtilities.SelectedItems.Count == 0) return;
        if (_lvUtilities.SelectedItems[0].Tag is UtilityItem item)
        {
            if (!item.ExistsOnDisk)
            {
                MessageBox.Show($"Файл не найден на диске:\n{item.ExecutablePath}\n\nПоложите исполняемый файл в папку Tools\\ рядом с программой.", "Файл отсутствует", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var res = _launcherService.LaunchUtility(item, _chkRandomName.Checked);
            if (!res.Success)
            {
                MessageBox.Show(res.ErrorMessage, "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void AddCustomExe()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Исполняемые файлы (*.exe;*.com;*.bat)|*.exe;*.com;*.bat|Все файлы (*.*)|*.*",
            Title = "Выберите утилиту для добавления в лаунчер"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            var newItem = new UtilityItem(
                Guid.NewGuid().ToString("N"),
                name,
                "Пользовательские",
                "Пользовательская утилита",
                ofd.FileName,
                "",
                true,
                true,
                true,
                false,
                true);

            _items.Add(newItem);
            _launcherService.SaveConfigUtilities(_items);
            RefreshList();
        }
    }

    private void RemoveSelectedCustomExe()
    {
        if (_lvUtilities.SelectedItems.Count == 0 || _lvUtilities.SelectedItems[0].Tag is not UtilityItem item)
        {
            MessageBox.Show("Выберите утилиту для удаления.", "Удаление утилиты", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!item.IsUserAdded)
        {
            MessageBox.Show("Встроенные системные и автоматически обнаруженные в Tools\\ утилиты нельзя удалить из этого списка.", "Встроенная утилита", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show($"Удалить добавленную утилиту «{item.Name}» из списка?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        var res = _launcherService.RemoveCustomUtility(item.Id);
        if (res.Success)
        {
            RefreshList();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenToolsFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _launcherService.ToolsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть папку: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
