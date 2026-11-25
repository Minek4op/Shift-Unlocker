using System.Diagnostics;
using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Processes;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI.Views;

public class ProcessView : UserControl
{
    private readonly RoundedTabControl _tabControl;
    private readonly RoundedTabPage _tabProcesses;
    private readonly RoundedTabPage _tabServices;
    private readonly RoundedTabPage _tabNetwork;

    private readonly ListView _lvProcesses;
    private readonly ListView _lvServices;
    private readonly ListView _lvNetwork;

    private readonly RoundedButton _btnKill;
    private readonly RoundedButton _btnKillTree;
    private readonly RoundedButton _btnSuspend;
    private readonly RoundedButton _btnResume;
    private readonly RoundedButton _btnWatchdog;
    private readonly RoundedButton _btnNewTask;
    private readonly RoundedButton _btnRefresh;

    private readonly RoundedButton _btnSvcAuto;
    private readonly RoundedButton _btnSvcManual;
    private readonly RoundedButton _btnSvcDisable;

    private readonly ProcessService _processService = new();

    public Func<ITargetContext>? GetContext { get; set; }
    public Func<BackupManager>? GetBackupManager { get; set; }

    public ProcessView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        _tabControl = new RoundedTabControl { Dock = DockStyle.Fill };
        _tabProcesses = new RoundedTabPage("Процессы") { Padding = new Padding(2) };
        _tabServices = new RoundedTabPage("Службы и драйверы") { Padding = new Padding(2) };
        _tabNetwork = new RoundedTabPage("Сетевые соединения") { Padding = new Padding(2) };

        _tabControl.TabPages.AddRange(new[] { _tabProcesses, _tabServices, _tabNetwork });

        // вкладка процессов
        var procTop = new RoundedFlowPanel { Dock = DockStyle.Top };
        _btnRefresh = new RoundedButton { Text = "Обновить", Size = new Size(95, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnKill = new RoundedButton { Text = "Завершить", Size = new Size(100, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnKillTree = new RoundedButton { Text = "Завершить дерево", Size = new Size(140, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnSuspend = new RoundedButton { Text = "Пауза", Size = new Size(80, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnResume = new RoundedButton { Text = "Возобновить", Size = new Size(110, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnWatchdog = new RoundedButton { Text = "Киллер по имени (Watchdog)", Size = new Size(205, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnNewTask = new RoundedButton { Text = "Новая задача", Size = new Size(110, 30), Margin = new Padding(2, 2, 6, 4) };

        procTop.Controls.AddRange(new Control[]
        {
            _btnRefresh, _btnKill, _btnKillTree, _btnSuspend, _btnResume, _btnWatchdog, _btnNewTask
        });

        _lvProcesses = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvProcesses);
        _lvProcesses.Columns.Add("PID", 65);
        _lvProcesses.Columns.Add("Имя процесса", 140);
        _lvProcesses.Columns.Add("Память (МБ)", 90);
        _lvProcesses.Columns.Add("Потоков", 70);
        _lvProcesses.Columns.Add("Дескрипторов", 95);
        _lvProcesses.Columns.Add("Подозрительность", 200);
        _lvProcesses.Columns.Add("Путь к исполняемому файлу", 350);

        var procCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        procCenter.Controls.Add(_lvProcesses);

        _tabProcesses.Controls.Add(procCenter);
        _tabProcesses.Controls.Add(procTop);

        // вкладка служб
        var svcTop = new RoundedFlowPanel { Dock = DockStyle.Top };
        var btnSvcRefresh = new RoundedButton { Text = "Обновить службы", Size = new Size(140, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnSvcAuto = new RoundedButton { Text = "Запуск: Авто", Size = new Size(120, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnSvcManual = new RoundedButton { Text = "Запуск: Вручную", Size = new Size(130, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnSvcDisable = new RoundedButton { Text = "Отключить службу", Size = new Size(140, 30), Margin = new Padding(2, 2, 6, 4) };

        svcTop.Controls.AddRange(new Control[] { btnSvcRefresh, _btnSvcAuto, _btnSvcManual, _btnSvcDisable });

        _lvServices = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvServices);
        _lvServices.Columns.Add("Имя службы", 150);
        _lvServices.Columns.Add("Тип запуска", 150);
        _lvServices.Columns.Add("Не-Microsoft?", 110);
        _lvServices.Columns.Add("Отображаемое имя", 220);
        _lvServices.Columns.Add("Путь к образу (ImagePath)", 380);

        var svcCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        svcCenter.Controls.Add(_lvServices);

        _tabServices.Controls.Add(svcCenter);
        _tabServices.Controls.Add(svcTop);

        // вкладка сети
        var netTop = new RoundedFlowPanel { Dock = DockStyle.Top };
        var btnNetRefresh = new RoundedButton { Text = "Обновить соединения", Size = new Size(170, 30), Margin = new Padding(2, 2, 6, 4) };
        netTop.Controls.Add(btnNetRefresh);

        _lvNetwork = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvNetwork);
        _lvNetwork.Columns.Add("Протокол", 80);
        _lvNetwork.Columns.Add("Локальный адрес:порт", 180);
        _lvNetwork.Columns.Add("Внешний адрес:порт", 180);
        _lvNetwork.Columns.Add("Состояние", 120);
        _lvNetwork.Columns.Add("PID", 65);
        _lvNetwork.Columns.Add("Имя процесса", 180);

        var netCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        netCenter.Controls.Add(_lvNetwork);

        _tabNetwork.Controls.Add(netCenter);
        _tabNetwork.Controls.Add(netTop);

        Controls.Add(_tabControl);

        // обработчики
        _btnRefresh.Click += async (s, e) => await RefreshProcessesAsync();
        _btnKill.Click += (s, e) => KillSelected(false);
        _btnKillTree.Click += (s, e) => KillSelected(true);
        _btnSuspend.Click += (s, e) => SuspendSelected();
        _btnResume.Click += (s, e) => ResumeSelected();
        _btnWatchdog.Click += (s, e) => ToggleWatchdog();
        _btnNewTask.Click += (s, e) => PromptNewTask();

        btnSvcRefresh.Click += (s, e) => RefreshServices();
        _btnSvcAuto.Click += (s, e) => ChangeService(2);
        _btnSvcManual.Click += (s, e) => ChangeService(3);
        _btnSvcDisable.Click += (s, e) => ChangeService(4);

        btnNetRefresh.Click += (s, e) => RefreshNetwork();

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _btnKill.Enabled = isAdmin;
        _btnKillTree.Enabled = isAdmin;
        _btnSuspend.Enabled = isAdmin;
        _btnResume.Enabled = isAdmin;
        _btnWatchdog.Enabled = isAdmin;
        _btnSvcAuto.Enabled = isAdmin;
        _btnSvcManual.Enabled = isAdmin;
        _btnSvcDisable.Enabled = isAdmin;
    }

    public async Task RefreshProcessesAsync()
    {
        _lvProcesses.Items.Clear();
        try
        {
            var procs = await _processService.GetProcessesAsync();
            foreach (var p in procs.OrderBy(x => x.Name))
            {
                double mb = p.WorkingSetBytes / (1024.0 * 1024.0);
                var item = new ListViewItem(new[]
                {
                    p.Id.ToString(),
                    p.Name,
                    $"{mb:F1} МБ",
                    p.ThreadCount.ToString(),
                    p.HandleCount.ToString(),
                    p.IsSuspicious ? $"[!] {p.SuspiciousReason}" : "OK",
                    p.FilePath
                })
                {
                    Tag = p,
                    Font = p.IsSuspicious ? ThemeManager.BoldFont : ThemeManager.StandardFont
                };
                _lvProcesses.Items.Add(item);
            }
        }
        catch { }
    }

    private void KillSelected(bool tree)
    {
        if (_lvProcesses.SelectedItems.Count == 0) return;
        foreach (ListViewItem it in _lvProcesses.SelectedItems)
        {
            if (it.Tag is ProcessInfoItem p)
            {
                if (tree) _processService.KillProcessTree(p.Id);
                else _processService.KillProcess(p.Id);
            }
        }
        _ = RefreshProcessesAsync();
    }

    private void SuspendSelected()
    {
        if (_lvProcesses.SelectedItems.Count == 0) return;
        foreach (ListViewItem it in _lvProcesses.SelectedItems)
        {
            if (it.Tag is ProcessInfoItem p)
            {
                _processService.SuspendProcess(p.Id);
            }
        }
    }

    private void ResumeSelected()
    {
        if (_lvProcesses.SelectedItems.Count == 0) return;
        foreach (ListViewItem it in _lvProcesses.SelectedItems)
        {
            if (it.Tag is ProcessInfoItem p)
            {
                _processService.ResumeProcess(p.Id);
            }
        }
    }

    private void ToggleWatchdog()
    {
        if (_processService.IsWatchdogRunning)
        {
            _processService.StopWatchdog();
            _btnWatchdog.Text = "Киллер по имени (Watchdog)";
            MessageBox.Show("Циклический киллер остановлен.", "Watchdog", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            string defaultName = _lvProcesses.SelectedItems.Count > 0 && _lvProcesses.SelectedItems[0].Tag is ProcessInfoItem p ? p.Name : "";
            string input = PromptInput("Введите имя процесса для циклического завершения (через запятую):", defaultName);
            if (!string.IsNullOrWhiteSpace(input))
            {
                var names = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                _processService.StartWatchdog(names, 500);
                _btnWatchdog.Text = "ОСТАНОВИТЬ Watchdog";
                MessageBox.Show($"Киллер запущен для: {string.Join(", ", names)}.\nПроцессы будут уничтожаться сразу при их появлении.", "Watchdog", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    private static string PromptInput(string text, string defaultValue = "")
    {
        using var prompt = new Form
        {
            Width = 450,
            Height = 160,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Ввод данных",
            StartPosition = FormStartPosition.CenterParent
        };
        var lbl = new Label { Left = 20, Top = 15, Text = text, AutoSize = true };
        var textBox = new TextBox { Left = 20, Top = 40, Width = 390, Text = defaultValue };
        var btnOk = new RoundedButton { Text = "OK", Left = 210, Width = 85, Top = 80, DialogResult = DialogResult.OK, IsPrimary = true };
        var btnCancel = new RoundedButton { Text = "Отмена", Left = 305, Width = 85, Top = 80, DialogResult = DialogResult.Cancel };
        prompt.Controls.AddRange(new Control[] { lbl, textBox, btnOk, btnCancel });
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;
        ThemeManager.ApplyTheme(prompt);

        return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
    }

    private void PromptNewTask()
    {
        string cmd = PromptInput("Введите команду или путь к файлу для запуска:");
        if (!string.IsNullOrWhiteSpace(cmd))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = cmd,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public void RefreshServices()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        _lvServices.Items.Clear();
        var svcs = _processService.GetServicesFromRegistry(ctx);
        foreach (var s in svcs.OrderBy(x => x.DisplayName))
        {
            var item = new ListViewItem(new[]
            {
                s.ServiceName,
                s.StartTypeDescription,
                s.IsNonMicrosoft ? "[!] Да" : "Microsoft",
                s.DisplayName,
                s.ImagePath
            })
            {
                Tag = s,
                Font = s.IsNonMicrosoft ? ThemeManager.BoldFont : ThemeManager.StandardFont
            };
            _lvServices.Items.Add(item);
        }
    }

    private void ChangeService(uint newStart)
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null || _lvServices.SelectedItems.Count == 0) return;

        foreach (ListViewItem it in _lvServices.SelectedItems)
        {
            if (it.Tag is ServiceInfoItem s)
            {
                _processService.ChangeServiceStartType(ctx, bm, s.ServiceName, newStart);
            }
        }
        RefreshServices();
    }

    public void RefreshNetwork()
    {
        _lvNetwork.Items.Clear();
        var map = Process.GetProcesses().ToDictionary(p => p.Id, p => p.ProcessName);
        var conns = NetworkConnectionHelper.GetConnections(map);

        foreach (var c in conns)
        {
            var item = new ListViewItem(new[]
            {
                c.Protocol,
                $"{c.LocalAddress}:{c.LocalPort}",
                $"{c.RemoteAddress}:{c.RemotePort}",
                c.State,
                c.OwningPid.ToString(),
                c.ProcessName
            });
            _lvNetwork.Items.Add(item);
        }
    }
}
