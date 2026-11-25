using System.Diagnostics;
using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Extra;
using ShiftUnlocker.Core.Modules.QuickScan;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI.Views;

public class ToolsLogView : UserControl
{
    private readonly ListView _lvLogs;
    private readonly RoundedButton _btnDiagProfile;
    private readonly RoundedButton _btnSafeProfile;
    private readonly RoundedButton _btnFullProfile;

    private readonly RoundedButton _btnResetHosts;
    private readonly RoundedButton _btnResetPolicies;
    private readonly RoundedButton _btnExportHtml;
    private readonly RoundedButton _btnExportTxt;

    private readonly RoundedButton _btnUndoLast;
    private readonly RoundedButton _btnUndoAll;

    private readonly Label _lblStatus;

    private readonly AutoHealWizardService _wizard = new();
    private readonly HostsService _hostsService = new();
    private readonly PolicyResetService _policyReset = new();
    private readonly SystemReportService _reportService = new();
    private readonly QuickScanService _scanService = new();

    public Func<ITargetContext>? GetContext { get; set; }
    public Func<BackupManager>? GetBackupManager { get; set; }
    public Action? OnStateChanged { get; set; }

    public ToolsLogView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var topPanel = new RoundedFlowPanel { Dock = DockStyle.Top };

        // профили автолечения
        var lblProf = new Label { Text = "Мастер лечения:", AutoSize = true, Font = ThemeManager.BoldFont, Margin = new Padding(2, 7, 4, 4) };
        _btnDiagProfile = new RoundedButton { Text = "1. Диагностика", Size = new Size(130, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnSafeProfile = new RoundedButton { Text = "2. Безопасное лечение", Size = new Size(175, 28), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnFullProfile = new RoundedButton { Text = "3. Полное исправление", Size = new Size(175, 28), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };

        // доп. утилиты и экспорт
        var lblTools = new Label { Text = "Инструменты:", AutoSize = true, Margin = new Padding(6, 7, 4, 4) };
        _btnResetHosts = new RoundedButton { Text = "Сбросить hosts", Size = new Size(125, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnResetPolicies = new RoundedButton { Text = "Сбросить GroupPolicy", Size = new Size(165, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnExportHtml = new RoundedButton { Text = "Отчет HTML", Size = new Size(110, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnExportTxt = new RoundedButton { Text = "Отчет TXT", Size = new Size(105, 28), Margin = new Padding(2, 2, 6, 4) };

        // откат и статус
        _btnUndoLast = new RoundedButton { Text = "Откатить последнее (Undo)", Size = new Size(190, 28), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnUndoAll = new RoundedButton { Text = "Откатить ВСЕ", Size = new Size(120, 28), Margin = new Padding(2, 2, 6, 4) };
        _lblStatus = new Label { Text = "Готов к работе.", AutoSize = true, ForeColor = ThemeManager.TextMutedColor, Margin = new Padding(4, 7, 4, 4) };

        topPanel.Controls.AddRange(new Control[]
        {
            lblProf, _btnDiagProfile, _btnSafeProfile, _btnFullProfile,
            lblTools, _btnResetHosts, _btnResetPolicies, _btnExportHtml, _btnExportTxt,
            _btnUndoLast, _btnUndoAll, _lblStatus
        });

        _lvLogs = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvLogs);

        _lvLogs.Columns.Add("Время", 90);
        _lvLogs.Columns.Add("Категория", 110);
        _lvLogs.Columns.Add("Статус", 70);
        _lvLogs.Columns.Add("Операция / Изменение", 400);

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        centerPanel.Controls.Add(_lvLogs);

        Controls.Add(centerPanel);
        Controls.Add(topPanel);

        _btnDiagProfile.Click += async (s, e) => await RunProfile(HealingProfile.DiagnosticsOnly);
        _btnSafeProfile.Click += async (s, e) => await RunProfile(HealingProfile.SafeHealing);
        _btnFullProfile.Click += async (s, e) => await RunProfile(HealingProfile.FullHealing);

        _btnResetHosts.Click += (s, e) => ResetHosts();
        _btnResetPolicies.Click += (s, e) => ResetPolicies();
        _btnExportHtml.Click += async (s, e) => await ExportReportHtml();
        _btnExportTxt.Click += async (s, e) => await ExportReportTxt();

        _btnUndoLast.Click += (s, e) => UndoLast();
        _btnUndoAll.Click += (s, e) => UndoAll();

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _btnSafeProfile.Enabled = isAdmin;
        _btnFullProfile.Enabled = isAdmin;
        _btnResetHosts.Enabled = isAdmin;
        _btnResetPolicies.Enabled = isAdmin;
        _btnUndoLast.Enabled = isAdmin;
        _btnUndoAll.Enabled = isAdmin;
    }

    public void RefreshLogs()
    {
        var bm = GetBackupManager?.Invoke();
        if (bm == null) return;

        _lvLogs.Items.Clear();
        foreach (var l in bm.ActionLogs.OrderByDescending(x => x.Timestamp))
        {
            var item = new ListViewItem(new[]
            {
                l.Timestamp.ToString("HH:mm:ss"),
                l.Category,
                l.Success ? "Успешно" : "Ошибка",
                l.Description
            });
            _lvLogs.Items.Add(item);
        }

        _btnUndoLast.Text = $"Откатить (Undo: {bm.UndoCount})";
    }

    private async Task RunProfile(HealingProfile profile)
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        _btnDiagProfile.Enabled = false;
        _btnSafeProfile.Enabled = false;
        _btnFullProfile.Enabled = false;

        try
        {
            var res = await _wizard.ExecuteProfileAsync(profile, ctx, bm, msg =>
            {
                if (InvokeRequired) Invoke(() => _lblStatus.Text = msg);
                else _lblStatus.Text = msg;
            });

            MessageBox.Show(res.Success ? "Операция успешно выполнена." : res.ErrorMessage, "Мастер лечения", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            _btnDiagProfile.Enabled = true;
            _btnSafeProfile.Enabled = true;
            _btnFullProfile.Enabled = true;
            OnStateChanged?.Invoke();
            RefreshLogs();
        }
    }

    private void ResetHosts()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _hostsService.ResetHostsToDefault(ctx, bm);
        MessageBox.Show(res.Success ? "Файл hosts успешно сброшен к эталонному состоянию." : res.ErrorMessage, "Hosts", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        OnStateChanged?.Invoke();
        RefreshLogs();
    }

    private void ResetPolicies()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _policyReset.ResetAllPolicies(ctx, bm);
        MessageBox.Show(res.Success ? "Все политики успешно сброшены." : res.ErrorMessage, "Политики", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        OnStateChanged?.Invoke();
        RefreshLogs();
    }

    private async Task ExportReportHtml()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "HTML Отчет (*.html)|*.html",
            FileName = $"ShiftUnlocker_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var findings = await _scanService.RunQuickScanAsync(ctx);
            string html = _reportService.GenerateHtmlReport(ctx, bm, findings);
            File.WriteAllText(sfd.FileName, html);

            Process.Start(new ProcessStartInfo { FileName = sfd.FileName, UseShellExecute = true });
        }
    }

    private async Task ExportReportTxt()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "Текстовый отчет (*.txt)|*.txt",
            FileName = $"ShiftUnlocker_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var findings = await _scanService.RunQuickScanAsync(ctx);
            string txt = _reportService.GenerateTextReport(ctx, bm, findings);
            File.WriteAllText(sfd.FileName, txt);

            MessageBox.Show($"Отчет сохранен:\n{sfd.FileName}", "Отчет TXT", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void UndoLast()
    {
        var bm = GetBackupManager?.Invoke();
        if (bm == null) return;

        var res = bm.UndoLast();
        if (!res.Success)
        {
            MessageBox.Show(res.ErrorMessage, "Undo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        OnStateChanged?.Invoke();
        RefreshLogs();
    }

    private void UndoAll()
    {
        var bm = GetBackupManager?.Invoke();
        if (bm == null) return;

        if (MessageBox.Show("Откатить ВСЕ выполненные изменения в реестре и файлах?", "Откат всех действий", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var res = bm.UndoAll();
        MessageBox.Show(res.Success ? "Все изменения успешно отменены." : res.ErrorMessage, "Откат", MessageBoxButtons.OK, MessageBoxIcon.Information);
        OnStateChanged?.Invoke();
        RefreshLogs();
    }
}
