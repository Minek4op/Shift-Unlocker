using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Autorun;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI.Views;

public class AutorunView : UserControl
{
    private readonly ListView _lvAutoruns;
    private readonly RoundedButton _btnRefresh;
    private readonly RoundedButton _btnDisable;
    private readonly RoundedButton _btnEnable;
    private readonly RoundedButton _btnDelete;
    private readonly RoundedButton _btnQuarantine;
    private readonly RoundedButton _btnExport;
    private readonly ComboBox _cbFilter;
    private readonly TextBox _txtSearch;
    private readonly Label _lblInfo;

    private readonly AutorunService _autorunService = new();
    private List<AutorunEntry> _entries = new();

    public Func<ITargetContext>? GetContext { get; set; }
    public Func<BackupManager>? GetBackupManager { get; set; }
    public Func<QuarantineManager>? GetQuarantineManager { get; set; }
    public Action? OnStateChanged { get; set; }

    public AutorunView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var topPanel = new RoundedFlowPanel { Dock = DockStyle.Top };

        _btnRefresh = new RoundedButton { Text = "Обновить", Size = new Size(95, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnDisable = new RoundedButton { Text = "Отключить", Size = new Size(100, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnEnable = new RoundedButton { Text = "Включить", Size = new Size(95, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnDelete = new RoundedButton { Text = "Удалить", Size = new Size(90, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnQuarantine = new RoundedButton { Text = "В карантин", Size = new Size(105, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnExport = new RoundedButton { Text = "Экспорт TXT", Size = new Size(105, 30), Margin = new Padding(2, 2, 6, 4) };

        var lblFilter = new Label { Text = "Фильтр:", AutoSize = true, Margin = new Padding(4, 7, 2, 4) };
        _cbFilter = new ComboBox { Size = new Size(160, 24), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2, 4, 6, 4) };
        _cbFilter.Items.AddRange(new object[] { "Все записи", "Только подозрительные", "Только отключенные" });
        _cbFilter.SelectedIndex = 0;

        var lblSearch = new Label { Text = "Поиск:", AutoSize = true, Margin = new Padding(6, 7, 2, 4) };
        _txtSearch = new TextBox { Size = new Size(160, 24), Margin = new Padding(2, 4, 6, 4) };

        _lblInfo = new Label
        {
            Text = "Готово к сканированию точек автозапуска.",
            AutoSize = true,
            ForeColor = ThemeManager.TextMutedColor,
            Margin = new Padding(4, 7, 4, 4)
        };

        topPanel.Controls.AddRange(new Control[]
        {
            _btnRefresh, _btnDisable, _btnEnable, _btnDelete, _btnQuarantine, _btnExport,
            lblFilter, _cbFilter, lblSearch, _txtSearch, _lblInfo
        });

        _lvAutoruns = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvAutoruns);

        _lvAutoruns.Columns.Add("Статус", 70);
        _lvAutoruns.Columns.Add("Имя записи", 160);
        _lvAutoruns.Columns.Add("Расположение / Источник", 170);
        _lvAutoruns.Columns.Add("Команда / Файл", 320);
        _lvAutoruns.Columns.Add("Файл есть?", 90);
        _lvAutoruns.Columns.Add("Эвристика / Описание", 260);

        // контекстное меню
        var cms = new ContextMenuStrip();
        var miDisable = cms.Items.Add("Отключить выбранное", null, (s, e) => DisableSelected());
        var miEnable = cms.Items.Add("Включить выбранное", null, (s, e) => EnableSelected());
        var miDelete = cms.Items.Add("Удалить выбранное", null, (s, e) => DeleteSelected());
        cms.Items.Add(new ToolStripSeparator());
        var miQuarantine = cms.Items.Add("Поместить файл в карантин", null, (s, e) => QuarantineSelected());
        var miOpenFile = cms.Items.Add("Открыть расположение файла в проводнике", null, (s, e) => OpenSelectedFileLocation());
        var miCopyCmd = cms.Items.Add("Копировать команду в буфер", null, (s, e) => CopySelectedCommand());

        _lvAutoruns.ContextMenuStrip = cms;

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        centerPanel.Controls.Add(_lvAutoruns);

        Controls.Add(centerPanel);
        Controls.Add(topPanel);

        _btnRefresh.Click += async (s, e) => await RefreshAutorunsAsync();
        _cbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
        _txtSearch.TextChanged += (s, e) => ApplyFilter();
        _btnDisable.Click += (s, e) => DisableSelected();
        _btnEnable.Click += (s, e) => EnableSelected();
        _btnDelete.Click += (s, e) => DeleteSelected();
        _btnQuarantine.Click += (s, e) => QuarantineSelected();
        _btnExport.Click += (s, e) => ExportList();

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _btnDisable.Enabled = isAdmin;
        _btnEnable.Enabled = isAdmin;
        _btnDelete.Enabled = isAdmin;
        _btnQuarantine.Enabled = isAdmin;
    }

    public async Task RefreshAutorunsAsync()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        _btnRefresh.Enabled = false;
        _lblInfo.Text = "Сканирование автозагрузки (реестр, службы, задачи, папки)...";

        try
        {
            _entries = await _autorunService.ScanAllAutorunsAsync(ctx);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _lblInfo.Text = $"Ошибка сканирования: {ex.Message}";
        }
        finally
        {
            _btnRefresh.Enabled = true;
        }
    }

    private void ApplyFilter()
    {
        _lvAutoruns.Items.Clear();
        string search = _txtSearch.Text.Trim();
        int filterMode = _cbFilter.SelectedIndex;

        var filtered = _entries.AsEnumerable();

        if (filterMode == 1) // Only suspicious
        {
            filtered = filtered.Where(x => x.IsSuspicious);
        }
        else if (filterMode == 2) // Only disabled
        {
            filtered = filtered.Where(x => !x.IsEnabled);
        }

        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(x =>
                x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Command.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.ResolvedFilePath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.LocationType.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        foreach (var e in list)
        {
            string statusStr = !e.IsEnabled ? "Отключен" : (e.IsSuspicious ? "Подозрительно" : "Включен");
            string existsStr = e.FileExists ? "Да" : (string.IsNullOrEmpty(e.ResolvedFilePath) ? "-" : "НЕТ");

            var item = new ListViewItem(new[]
            {
                statusStr,
                e.Name,
                e.LocationType,
                e.Command,
                existsStr,
                e.IsSuspicious ? e.SuspiciousReason : (!e.IsEnabled ? "Запись отключена" : "Стандартная запись")
            })
            {
                Tag = e,
                Font = e.IsSuspicious ? ThemeManager.BoldFont : ThemeManager.StandardFont
            };

            _lvAutoruns.Items.Add(item);
        }

        _lblInfo.Text = $"Записей: {list.Count} из {_entries.Count} (Подозрительных: {_entries.Count(x => x.IsSuspicious)})";
    }

    private bool CheckAdminRequired(string actionName)
    {
        bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        var ctx = GetContext?.Invoke();
        if (ctx != null && ctx.IsOnline && !isAdmin)
        {
            var res = MessageBox.Show(
                $"Для операции «{actionName}» над системными ветками HKLM требуются права Администратора.\n\n" +
                "Перезапустить Shift Unlocker от имени Администратора сейчас?",
                "Требуются права Администратора",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
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
                catch { }
            }
            return false;
        }
        return true;
    }

    private void DisableSelected()
    {
        if (_lvAutoruns.SelectedItems.Count == 0)
        {
            MessageBox.Show("Пожалуйста, выберите хотя бы одну запись в списке для отключения.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        int successCount = 0;
        var errors = new List<string>();

        foreach (ListViewItem it in _lvAutoruns.SelectedItems)
        {
            if (it.Tag is AutorunEntry e)
            {
                if (!e.IsEnabled)
                {
                    continue; // Already disabled
                }

                var res = _autorunService.DisableEntry(ctx, bm, e);
                if (res.Success)
                {
                    successCount++;
                }
                else
                {
                    errors.Add($"{e.Name}: {res.Error}");
                }
            }
        }

        OnStateChanged?.Invoke();
        _ = RefreshAutorunsAsync();

        if (errors.Count > 0)
        {
            if (errors.Any(x => x.Contains("Администратора") || x.Contains("Отказано в доступе")))
            {
                CheckAdminRequired("Отключение автозагрузки");
            }
            else
            {
                MessageBox.Show($"Отключено: {successCount}.\nОшибки:\n" + string.Join("\n", errors.Take(5)), "Результат отключения", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else if (successCount > 0)
        {
            _lblInfo.Text = $"Успешно отключено записей: {successCount} (бэкап создан).";
        }
    }

    private void EnableSelected()
    {
        if (_lvAutoruns.SelectedItems.Count == 0)
        {
            MessageBox.Show("Пожалуйста, выберите хотя бы одну запись в списке для включения.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        int successCount = 0;
        var errors = new List<string>();

        foreach (ListViewItem it in _lvAutoruns.SelectedItems)
        {
            if (it.Tag is AutorunEntry e)
            {
                if (e.IsEnabled)
                {
                    continue; // Already enabled
                }

                var res = _autorunService.EnableEntry(ctx, bm, e);
                if (res.Success)
                {
                    successCount++;
                }
                else
                {
                    errors.Add($"{e.Name}: {res.Error}");
                }
            }
        }

        OnStateChanged?.Invoke();
        _ = RefreshAutorunsAsync();

        if (errors.Count > 0)
        {
            if (errors.Any(x => x.Contains("Администратора") || x.Contains("Отказано в доступе")))
            {
                CheckAdminRequired("Включение автозагрузки");
            }
            else
            {
                MessageBox.Show($"Включено: {successCount}.\nОшибки:\n" + string.Join("\n", errors.Take(5)), "Результат включения", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else if (successCount > 0)
        {
            _lblInfo.Text = $"Успешно включено записей: {successCount}.";
        }
    }

    private void DeleteSelected()
    {
        if (_lvAutoruns.SelectedItems.Count == 0)
        {
            MessageBox.Show("Пожалуйста, выберите хотя бы одну запись в списке для удаления.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        if (MessageBox.Show($"Удалить выбранные записи автозагрузки ({_lvAutoruns.SelectedItems.Count} шт.)?\nПеред удалением будет автоматически создана резервная копия.", "Подтверждение удаления", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        int successCount = 0;
        var errors = new List<string>();

        foreach (ListViewItem it in _lvAutoruns.SelectedItems)
        {
            if (it.Tag is AutorunEntry e)
            {
                var res = _autorunService.DeleteEntry(ctx, bm, e);
                if (res.Success)
                {
                    successCount++;
                }
                else
                {
                    errors.Add($"{e.Name}: {res.Error}");
                }
            }
        }

        OnStateChanged?.Invoke();
        _ = RefreshAutorunsAsync();

        if (errors.Count > 0)
        {
            if (errors.Any(x => x.Contains("Администратора") || x.Contains("Отказано в доступе")))
            {
                CheckAdminRequired("Удаление автозагрузки");
            }
            else
            {
                MessageBox.Show($"Удалено: {successCount}.\nОшибки:\n" + string.Join("\n", errors.Take(5)), "Результат удаления", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else if (successCount > 0)
        {
            _lblInfo.Text = $"Успешно удалено записей: {successCount} (бэкап сохранен).";
        }
    }

    private void QuarantineSelected()
    {
        if (_lvAutoruns.SelectedItems.Count == 0)
        {
            MessageBox.Show("Пожалуйста, выберите запись с существующим файлом для перемещения в карантин.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var qm = GetQuarantineManager?.Invoke();
        var bm = GetBackupManager?.Invoke();
        var ctx = GetContext?.Invoke();
        if (qm == null || bm == null || ctx == null) return;

        int quarantinedCount = 0;

        foreach (ListViewItem it in _lvAutoruns.SelectedItems)
        {
            if (it.Tag is AutorunEntry e && File.Exists(e.ResolvedFilePath))
            {
                var qRes = qm.QuarantineFile(e.ResolvedFilePath, $"Автозагрузка: {e.Name} ({e.SuspiciousReason})");
                if (qRes.Success)
                {
                    _autorunService.DisableEntry(ctx, bm, e);
                    quarantinedCount++;
                }
            }
        }

        OnStateChanged?.Invoke();
        _ = RefreshAutorunsAsync();

        if (quarantinedCount > 0)
        {
            _lblInfo.Text = $"Изолировано в карантин: {quarantinedCount} файлов (записи отключены).";
            MessageBox.Show($"Успешно изолировано файлов: {quarantinedCount}.\nФайлы зашифрованы и помещены в папку SU_Quarantine.", "Карантин", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("У выбранных записей файл не найден на диске либо уже изолирован.", "Карантин", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OpenSelectedFileLocation()
    {
        if (_lvAutoruns.SelectedItems.Count == 0) return;
        if (_lvAutoruns.SelectedItems[0].Tag is AutorunEntry e && File.Exists(e.ResolvedFilePath))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{e.ResolvedFilePath}\"");
            }
            catch { }
        }
        else
        {
            MessageBox.Show("Файл не существует или не является путем на локальном диске.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void CopySelectedCommand()
    {
        if (_lvAutoruns.SelectedItems.Count == 0) return;
        if (_lvAutoruns.SelectedItems[0].Tag is AutorunEntry e)
        {
            try
            {
                Clipboard.SetText(e.Command);
                _lblInfo.Text = "Команда скопирована в буфер обмена.";
            }
            catch { }
        }
    }

    private void ExportList()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Текстовый файл (*.txt)|*.txt",
            FileName = $"Autoruns_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Точки автозагрузки (Shift Unlocker) ===");
            sb.AppendLine($"Дата экспорта: {DateTime.Now}");
            sb.AppendLine();
            foreach (var e in _entries)
            {
                string state = e.IsEnabled ? "ACTIVE" : "DISABLED";
                sb.AppendLine($"[{state}] [{e.LocationType}] {e.Name} = {e.Command}");
                if (e.IsSuspicious) sb.AppendLine($"  -> ВНИМАНИЕ: {e.SuspiciousReason}");
                if (e.FileExists) sb.AppendLine($"  -> Файл: {e.ResolvedFilePath} (SHA256: {e.Sha256 ?? "N/A"})");
            }
            File.WriteAllText(sfd.FileName, sb.ToString());
            MessageBox.Show("Список успешно сохранен.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
