using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Extra;
using ShiftUnlocker.Core.Modules.FileManager;
using ShiftUnlocker.Core.Modules.MbrBoot;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI;

public class BcdEditorDialog : Form
{
    private readonly BcdService _bcd = new();

    public BcdEditorDialog(ITargetContext ctx)
    {
        Text = "Редактор BCD / Безопасный режим / WinRE";
        Size = new Size(760, 500);
        StartPosition = FormStartPosition.CenterParent;

        var top = new RoundedFlowPanel { Dock = DockStyle.Top };
        var btnEnum = new RoundedButton { Text = "Обновить BCD", Size = new Size(125, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnSafeMin = new RoundedButton { Text = "Вкл. SafeMode", Size = new Size(125, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnSafeNet = new RoundedButton { Text = "Вкл. SafeMode+Net", Size = new Size(155, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnNormal = new RoundedButton { Text = "Обычная загрузка", Size = new Size(145, 30), IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        var btnIgnoreFail = new RoundedButton { Text = "IgnoreAllFailures", Size = new Size(135, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnWinRe = new RoundedButton { Text = "Перезагрузка в WinRE", Size = new Size(175, 30), Margin = new Padding(2, 2, 6, 4) };
        top.Controls.AddRange(new Control[] { btnEnum, btnSafeMin, btnSafeNet, btnNormal, btnIgnoreFail, btnWinRe });

        var txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Font = ThemeManager.MonoFont };
        Controls.Add(txt);
        Controls.Add(top);

        void RefreshBcd() => txt.Text = _bcd.EnumerateBcd(ctx);

        btnEnum.Click += (s, e) => RefreshBcd();
        btnSafeMin.Click += (s, e) => { var r = _bcd.SetSafeBoot(ctx, true, false); MessageBox.Show(r.Success ? "SafeBoot Minimal включен." : r.ErrorMessage); RefreshBcd(); };
        btnSafeNet.Click += (s, e) => { var r = _bcd.SetSafeBoot(ctx, true, true); MessageBox.Show(r.Success ? "SafeBoot Network включен." : r.ErrorMessage); RefreshBcd(); };
        btnNormal.Click += (s, e) => { var r = _bcd.SetSafeBoot(ctx, false); MessageBox.Show(r.Success ? "Обычная загрузка восстановлена." : r.ErrorMessage); RefreshBcd(); };
        btnIgnoreFail.Click += (s, e) => { var r = _bcd.SetBootStatusPolicyIgnoreAll(ctx); MessageBox.Show(r.Success ? "bootstatuspolicy=ignoreallfailures установлен." : r.ErrorMessage); RefreshBcd(); };
        btnWinRe.Click += (s, e) =>
        {
            if (MessageBox.Show("Перезагрузить компьютер в среду восстановления WinRE?", "WinRE", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                _bcd.RebootToWinRE();
        };

        RefreshBcd();
        ThemeManager.ApplyTheme(this);
    }
}

public class RansomwareDialog : Form
{
    private readonly RansomwareHelperService _svc = new();

    public RansomwareDialog(ITargetContext ctx)
    {
        Text = "Помощник по шифровальщикам";
        Size = new Size(760, 480);
        StartPosition = FormStartPosition.CenterParent;

        var top = new RoundedFlowPanel { Dock = DockStyle.Top };
        var btnScan = new RoundedButton { Text = "Сканировать записки и расширения", Size = new Size(255, 30), IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        top.Controls.Add(btnScan);

        var txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = ThemeManager.MonoFont };
        Controls.Add(txt);
        Controls.Add(top);

        void Run()
        {
            var rep = _svc.AnalyzeUserFiles(ctx);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ПРЕДПОЛАГАЕМЫЕ СЕМЕЙСТВА ===");
            sb.AppendLine(rep.DetectedFamilies.Count == 0 ? "(Не выявлено)" : string.Join(", ", rep.DetectedFamilies));
            sb.AppendLine("\r\n=== СТАТИСТИКА РАСШИРЕНИЙ ===");
            foreach (var kv in rep.SuspiciousExtensionCounts) sb.AppendLine($"{kv.Key}: {kv.Value} файлов");
            sb.AppendLine("\r\n=== НАЙДЕННЫЕ ЗАПИСКИ О ВЫКУПЕ ===");
            foreach (var n in rep.RansomNotesFound) sb.AppendLine(n);
            sb.AppendLine("\r\n=== ТЕНЕВЫЕ КОПИИ (VSS) ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(rep.ShadowCopiesOutput) ? "(Недоступно / пусто)" : rep.ShadowCopiesOutput);
            txt.Text = sb.ToString();
        }

        btnScan.Click += (s, e) => Run();
        Run();
        ThemeManager.ApplyTheme(this);
    }
}

public class DiagnosticsCleanupDialog : Form
{
    private readonly DiagnosticsCleanupService _svc = new();

    public DiagnosticsCleanupDialog(ITargetContext ctx)
    {
        Text = "Журналы событий (.evtx), Сеть и Очистка кэшей";
        Size = new Size(820, 520);
        StartPosition = FormStartPosition.CenterParent;

        var top = new RoundedFlowPanel { Dock = DockStyle.Top };
        var btnEvtx = new RoundedButton { Text = "Открыть .evtx...", Size = new Size(135, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnNetDiag = new RoundedButton { Text = "DNS / ARP / Маршруты", Size = new Size(175, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnNetReset = new RoundedButton { Text = "Сброс Winsock / TCP-IP", Size = new Size(185, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnPreviewCache = new RoundedButton { Text = "Оценить Temp/Prefetch", Size = new Size(180, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnCleanCache = new RoundedButton { Text = "Очистить кэши", Size = new Size(135, 30), IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        top.Controls.AddRange(new Control[] { btnEvtx, btnNetDiag, btnNetReset, btnPreviewCache, btnCleanCache });

        var txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Font = ThemeManager.MonoFont };
        Controls.Add(txt);
        Controls.Add(top);

        btnEvtx.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Event Log (*.evtx)|*.evtx", InitialDirectory = Path.Combine(ctx.System32Directory, @"winevt\Logs") };
            if (ofd.ShowDialog() == DialogResult.OK) txt.Text = _svc.ReadOfflineEvtx(ofd.FileName);
        };
        btnNetDiag.Click += (s, e) => txt.Text = _svc.GetNetworkDiagnostics();
        btnNetReset.Click += (s, e) =>
        {
            var r = _svc.ResetNetworkStack();
            MessageBox.Show(r.Success ? "Сетевой стек (DNS, Winsock, TCP/IP) сброшен." : r.ErrorMessage);
        };
        btnPreviewCache.Click += (s, e) =>
        {
            var items = _svc.GetTempCachePreview(ctx);
            txt.Text = string.Join("\r\n", items.Select(i => $"{i.Name,-24} | {i.FileCount,5} файлов | {i.SizeBytes / (1024.0 * 1024.0):F2} МБ | {i.FolderPath}"));
        };
        btnCleanCache.Click += (s, e) =>
        {
            var items = _svc.GetTempCachePreview(ctx);
            var (del, freed) = _svc.CleanTempCaches(items);
            MessageBox.Show($"Удалено файлов: {del}, освобождено: {freed / (1024.0 * 1024.0):F2} МБ.");
            btnPreviewCache.PerformClick();
        };

        btnPreviewCache.PerformClick();
        ThemeManager.ApplyTheme(this);
    }
}

public class ProfilesAndMftDialog : Form
{
    public ProfilesAndMftDialog(ITargetContext ctx, BackupManager bm, QuarantineManager qm, Action? onChanged)
    {
        Text = "Профили лечения и MFT (Read-Only)";
        Size = new Size(760, 480);
        StartPosition = FormStartPosition.CenterParent;

        var profSvc = new TreatmentProfileService();
        var mftSvc = new MftReaderService();

        var top = new RoundedFlowPanel { Dock = DockStyle.Top };
        var cbProf = new ComboBox { Size = new Size(210, 24), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2, 5, 6, 4) };
        var profiles = profSvc.GetBuiltInProfiles();
        foreach (var p in profiles) cbProf.Items.Add(p.Name);
        if (cbProf.Items.Count > 0) cbProf.SelectedIndex = 0;

        var btnApplyProf = new RoundedButton { Text = "Применить профиль", Size = new Size(155, 30), IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        var btnExportProf = new RoundedButton { Text = "Экспорт JSON...", Size = new Size(130, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnImportProf = new RoundedButton { Text = "Импорт JSON...", Size = new Size(130, 30), Margin = new Padding(2, 2, 6, 4) };
        var btnMft = new RoundedButton { Text = "MFT удалённые (C:)", Size = new Size(155, 30), Margin = new Padding(2, 2, 6, 4) };
        top.Controls.AddRange(new Control[] { cbProf, btnApplyProf, btnExportProf, btnImportProf, btnMft });

        var txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = ThemeManager.MonoFont };
        Controls.Add(txt);
        Controls.Add(top);

        void ShowProf()
        {
            if (cbProf.SelectedIndex >= 0 && cbProf.SelectedIndex < profiles.Count)
            {
                var p = profiles[cbProf.SelectedIndex];
                txt.Text = $"Профиль: {p.Name}\r\n{p.Description}\r\n\r\nСкрипт:\r\n{p.SuScript}";
            }
        }
        cbProf.SelectedIndexChanged += (s, e) => ShowProf();

        btnApplyProf.Click += (s, e) =>
        {
            if (cbProf.SelectedIndex < 0) return;
            var p = profiles[cbProf.SelectedIndex];
            var res = new SuScriptService().ExecuteScript(p.SuScript, ctx, bm, qm, dryRun: false);
            txt.Text = string.Join("\r\n", res.LogLines);
            onChanged?.Invoke();
        };
        btnExportProf.Click += (s, e) =>
        {
            if (cbProf.SelectedIndex < 0) return;
            using var sfd = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "profile.json" };
            if (sfd.ShowDialog() == DialogResult.OK) profSvc.ExportProfile(profiles[cbProf.SelectedIndex], sfd.FileName);
        };
        btnImportProf.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var p = profSvc.ImportProfile(ofd.FileName);
                if (p != null) { profiles.Add(p); cbProf.Items.Add(p.Name); cbProf.SelectedIndex = cbProf.Items.Count - 1; }
            }
        };
        btnMft.Click += (s, e) =>
        {
            string drv = Path.GetPathRoot(ctx.WindowsDirectory)?.TrimEnd('\\') ?? "C:";
            var (st, entries) = mftSvc.ScanDeletedMftEntries(drv);
            if (!st.Success) { txt.Text = st.ErrorMessage; return; }
            txt.Text = $"Найдено удалённых записей MFT на {drv}: {entries.Count}\r\n" +
                       string.Join("\r\n", entries.Select(x => $"#{x.RecordNumber} {(x.IsDirectory ? "[DIR]" : "[FILE]")} {x.FileName}"));
        };

        ShowProf();
        ThemeManager.ApplyTheme(this);
    }
}
