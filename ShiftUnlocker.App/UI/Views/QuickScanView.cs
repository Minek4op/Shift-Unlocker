using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.QuickScan;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI.Views;

public class QuickScanView : UserControl
{
    private readonly ListView _lvFindings;
    private readonly RoundedButton _btnScan;
    private readonly RoundedButton _btnFixSelected;
    private readonly RoundedButton _btnFixAll;
    private readonly Label _lblStatus;
    private readonly ProgressBar _progressBar;

    private readonly QuickScanService _scanService = new();
    private List<ScanFinding> _currentFindings = new();
    private bool _hasScannedOnce;

    public Func<ITargetContext>? GetContext { get; set; }
    public Func<BackupManager>? GetBackupManager { get; set; }
    public Action? OnStateChanged { get; set; }

    public QuickScanView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var topPanel = new RoundedFlowPanel { Dock = DockStyle.Top };

        _btnScan = new RoundedButton
        {
            Text = "Быстрое сканирование",
            Size = new Size(190, 32),
            Font = ThemeManager.BoldFont,
            IsPrimary = true,
            Margin = new Padding(2, 2, 6, 4)
        };

        _btnFixSelected = new RoundedButton
        {
            Text = "Исправить выбранное",
            Size = new Size(180, 32),
            Margin = new Padding(2, 2, 6, 4)
        };

        _btnFixAll = new RoundedButton
        {
            Text = "Исправить ВСЕ (Рекомендуется)",
            Size = new Size(230, 32),
            Font = ThemeManager.BoldFont,
            Margin = new Padding(2, 2, 6, 4)
        };

        _progressBar = new ProgressBar
        {
            Size = new Size(120, 24),
            Style = ProgressBarStyle.Marquee,
            Visible = false,
            Margin = new Padding(2, 6, 6, 4)
        };

        _lblStatus = new Label
        {
            Text = "Нажмите «Быстрое сканирование» для проверки системы на вирусы, блокировки и подмены.",
            AutoSize = true,
            ForeColor = ThemeManager.TextMutedColor,
            Margin = new Padding(2, 6, 2, 4)
        };

        topPanel.Controls.AddRange(new Control[] { _btnScan, _btnFixSelected, _btnFixAll, _progressBar, _lblStatus });

        _lvFindings = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvFindings);

        _lvFindings.Columns.Add("Риск", 100);
        _lvFindings.Columns.Add("Категория", 140);
        _lvFindings.Columns.Add("Проблема", 230);
        _lvFindings.Columns.Add("Описание и последствия", 460);

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        centerPanel.Controls.Add(_lvFindings);

        Controls.Add(centerPanel);
        Controls.Add(topPanel);

        _btnScan.Click += async (s, e) => await RunScanAsync();
        _btnFixSelected.Click += (s, e) => FixSelected();
        _btnFixAll.Click += (s, e) => FixAll();

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _btnFixSelected.Enabled = isAdmin;
        _btnFixAll.Enabled = isAdmin;
    }

    public void ResetScanState()
    {
        _hasScannedOnce = false;
    }

    public async Task EnsureScannedOnceAsync()
    {
        if (_hasScannedOnce && _currentFindings.Count > 0) return;
        if (_hasScannedOnce) return;
        _hasScannedOnce = true;
        await RunScanAsync();
    }

    public async Task RunScanAsync()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        _hasScannedOnce = true;

        _btnScan.Enabled = false;
        _progressBar.Visible = true;
        _lblStatus.Text = "Сканирование системы (реестр, IFEO, оболочка, ассоциации, диспетчер задач)...";
        _lvFindings.Items.Clear();

        try
        {
            _currentFindings = await _scanService.RunQuickScanAsync(ctx);
            _lblStatus.Text = _currentFindings.Count == 0
                ? "Угроз и блокировок не обнаружено. Система в норме."
                : $"[!] Обнаружено проблем: {_currentFindings.Count}. Рекомендуется исправить их.";

            foreach (var f in _currentFindings)
            {
                string sevStr = f.Severity switch
                {
                    FindingSeverity.Critical => "[X] КРИТИЧНО",
                    FindingSeverity.High => "[!] ВЫСОКИЙ",
                    FindingSeverity.Medium => "[?] СРЕДНИЙ",
                    _ => "[ ] ИНФО"
                };

                var item = new ListViewItem(new[] { sevStr, f.Category, f.Title, f.Description })
                {
                    Tag = f,
                    Font = f.Severity == FindingSeverity.Critical ? ThemeManager.BoldFont : ThemeManager.StandardFont
                };
                _lvFindings.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Ошибка сканирования: {ex.Message}";
        }
        finally
        {
            _btnScan.Enabled = true;
            _progressBar.Visible = false;
        }
    }

    private void FixSelected()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        if (_lvFindings.SelectedItems.Count == 0)
        {
            MessageBox.Show("Выберите хотя бы один пункт в списке.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int fixedCount = 0;
        foreach (ListViewItem item in _lvFindings.SelectedItems)
        {
            if (item.Tag is ScanFinding f)
            {
                var res = f.FixAction(ctx, bm);
                if (res.Success) fixedCount++;
            }
        }

        OnStateChanged?.Invoke();
        _ = RunScanAsync();
        MessageBox.Show($"Успешно исправлено элементов: {fixedCount}.\nВсе изменения зафиксированы в резервной копии.", "Восстановление", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void FixAll()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        if (_currentFindings.Count == 0)
        {
            MessageBox.Show("Нет обнаруженных проблем для исправления.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show($"Исправить все обнаруженные проблемы ({_currentFindings.Count} шт.)?\nБудет создана резервная копия перед изменением.", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        int fixedCount = 0;
        foreach (var f in _currentFindings)
        {
            var res = f.FixAction(ctx, bm);
            if (res.Success) fixedCount++;
        }

        OnStateChanged?.Invoke();
        _ = RunScanAsync();
        MessageBox.Show($"Успешно исправлено элементов: {fixedCount}.\nВсе ограничения и подмены сняты.", "Восстановление", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
