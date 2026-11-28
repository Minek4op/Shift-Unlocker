using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Autorun;
using ShiftUnlocker.Core.Modules.Processes;
using ShiftUnlocker.Core.Modules.QuickScan;
using ShiftUnlocker.Core.Modules.Restrictions;
using ShiftUnlocker.Core.Modules.SystemRecovery;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Extra;

public class SystemReportService
{
    public string GenerateTextReport(ITargetContext ctx, BackupManager bm, IEnumerable<ScanFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=================================================");
        sb.AppendLine("           ОТЧЕТ SHIFT UNLOCKER                  ");
        sb.AppendLine("=================================================");
        sb.AppendLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Режим: {(ctx.IsOnline ? "ONLINE (Текущая система)" : "OFFLINE (Чужой диск)")}");
        sb.AppendLine($"Среда: {(ctx.IsWinPE ? "Windows PE / Recovery Environment" : "Windows Desktop")}");
        sb.AppendLine($"Целевой корень: {ctx.TargetRootPath}");
        sb.AppendLine($"Папка Windows: {ctx.WindowsDirectory}");
        sb.AppendLine($"Профилей пользователей: {ctx.UserProfiles.Count}");
        sb.AppendLine(new string('-', 50));
        sb.AppendLine("НАЙДЕННЫЕ ПРОБЛЕМЫ И ОГРАНИЧЕНИЯ:");

        int count = 0;
        foreach (var f in findings)
        {
            count++;
            string sev = f.Severity switch
            {
                FindingSeverity.Critical => "[КРИТИЧНО]",
                FindingSeverity.High => "[ВЫСОКИЙ]",
                FindingSeverity.Medium => "[СРЕДНИЙ]",
                FindingSeverity.Low => "[НИЗКИЙ]",
                _ => "[ИНФО]"
            };
            sb.AppendLine($"{count}. {sev} [{f.Category}] {f.Title}");
            sb.AppendLine($"   Описание: {f.Description}");
        }

        if (count == 0)
        {
            sb.AppendLine("   (Проблем не обнаружено)");
        }

        sb.AppendLine(new string('-', 50));
        sb.AppendLine("ЖУРНАЛ ДЕЙСТВИЙ:");
        foreach (var log in bm.ActionLogs)
        {
            string status = log.Success ? "Успешно" : "Ошибка";
            sb.AppendLine($"[{log.Timestamp:HH:mm:ss}] {status} [{log.Category}] {log.Description}");
        }

        return sb.ToString();
    }

    public string GenerateHtmlReport(ITargetContext ctx, BackupManager bm, IEnumerable<ScanFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Отчет Shift Unlocker</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { background: #000; color: #eee; font-family: Consolas, monospace; padding: 25px; }");
        sb.AppendLine("h1, h2 { color: #fff; border-bottom: 1px solid #444; padding-bottom: 5px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 15px; }");
        sb.AppendLine("th, td { border: 1px solid #444; padding: 8px; text-align: left; }");
        sb.AppendLine("th { background: #222; }");
        sb.AppendLine(".critical { color: #ff4444; font-weight: bold; }");
        sb.AppendLine(".high { color: #ff8800; font-weight: bold; }");
        sb.AppendLine(".medium { color: #ffee55; }");
        sb.AppendLine(".ok { color: #44ff44; }");
        sb.AppendLine(".fail { color: #ff4444; }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<h1>Отчет Shift Unlocker</h1>");
        sb.AppendLine($"<p><b>Дата:</b> {DateTime.Now:yyyy-MM-dd HH:mm:ss}<br>");
        sb.AppendLine($"<b>Режим:</b> {(ctx.IsOnline ? "ONLINE" : "OFFLINE")}<br>");
        sb.AppendLine($"<b>Целевая система:</b> {ctx.WindowsDirectory}<br>");
        sb.AppendLine($"<b>Среда:</b> {(ctx.IsWinPE ? "WinPE" : "Windows Desktop")}</p>");

        sb.AppendLine("<h2>Обнаруженные ограничения и аномалии</h2>");
        sb.AppendLine("<table><tr><th>Уровень</th><th>Категория</th><th>Название</th><th>Описание</th></tr>");
        foreach (var f in findings)
        {
            string cls = f.Severity switch
            {
                FindingSeverity.Critical => "critical",
                FindingSeverity.High => "high",
                FindingSeverity.Medium => "medium",
                _ => ""
            };
            sb.AppendLine($"<tr><td class='{cls}'>[{f.Severity}]</td><td>{f.Category}</td><td>{f.Title}</td><td>{f.Description}</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Журнал выполненных действий</h2>");
        sb.AppendLine("<table><tr><th>Время</th><th>Статус</th><th>Категория</th><th>Действие</th></tr>");
        foreach (var l in bm.ActionLogs)
        {
            string cls = l.Success ? "ok" : "fail";
            sb.AppendLine($"<tr><td>{l.Timestamp:HH:mm:ss}</td><td class='{cls}'>{(l.Success ? "Успешно" : "Ошибка")}</td><td>{l.Category}</td><td>{l.Description}</td></tr>");
        }
        sb.AppendLine("</table></body></html>");

        return sb.ToString();
    }

    public async Task<string> GenerateFrstStyleReportAsync(ITargetContext ctx, BackupManager bm, bool sanitizeUsernames = false)
    {
        var sb = new StringBuilder();
        string user = sanitizeUsernames ? "<USER>" : Environment.UserName;
        string machine = sanitizeUsernames ? "<PC>" : Environment.MachineName;

        string Sanitize(string input)
        {
            if (!sanitizeUsernames || string.IsNullOrEmpty(input)) return input;
            return input.Replace(Environment.UserName, "<USER>", StringComparison.OrdinalIgnoreCase);
        }

        sb.AppendLine("==================== Shift Unlocker Diagnostic Report (FRST/AVZ Style) ====================");
        sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Mode: {(ctx.IsOnline ? "ONLINE" : "OFFLINE")} | WinPE: {ctx.IsWinPE} | Target: {ctx.WindowsDirectory}");
        sb.AppendLine($"Computer: {machine} | User: {user} | OS: {Environment.OSVersion}");
        sb.AppendLine();

        sb.AppendLine("==================== Processes ====================");
        if (ctx.IsOnline)
        {
            var procs = await new ProcessService().GetProcessesAsync();
            foreach (var p in procs.OrderBy(x => x.Name))
            {
                string sus = p.IsSuspicious ? " [SUSPICIOUS]" : "";
                sb.AppendLine($"({p.Id}) {p.Name} => {Sanitize(p.FilePath)}{sus}");
            }
        }
        else
        {
            sb.AppendLine("(Offline target - process enumeration skipped)");
        }
        sb.AppendLine();

        sb.AppendLine("==================== Autorun & Persistence Points ====================");
        var autoruns = await new AutorunService().ScanAllAutorunsAsync(ctx);
        foreach (var a in autoruns)
        {
            string flag = a.IsSuspicious ? " [ALERT]" : "";
            string state = a.IsEnabled ? "" : " [DISABLED]";
            sb.AppendLine($"[{a.LocationType}]{state}{flag} {a.Name} => {Sanitize(a.Command)} ({a.Hive}\\{Sanitize(a.KeyOrDirectoryPath)})");
        }
        sb.AppendLine();

        sb.AppendLine("==================== Services (Non-Microsoft / Custom) ====================");
        foreach (var svc in ctx.GetSubKeyNames("HKLM", @"SYSTEM\CurrentControlSet\Services"))
        {
            string sk = $@"SYSTEM\CurrentControlSet\Services\{svc}";
            string img = ctx.GetValue("HKLM", sk, "ImagePath")?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(img)) continue;
            if (img.Contains(@"system32\svchost.exe", StringComparison.OrdinalIgnoreCase) ||
                img.Contains(@"system32\drivers\", StringComparison.OrdinalIgnoreCase)) continue;
            string start = ctx.GetValue("HKLM", sk, "Start")?.ToString() ?? "?";
            sb.AppendLine($"S{start} {svc}; {Sanitize(img)}");
        }
        sb.AppendLine();

        sb.AppendLine("==================== Policies & Restrictions ====================");
        var restrictions = new RestrictionsService().CheckAll(ctx);
        foreach (var r in restrictions.Where(x => x.CurrentStatus == RestrictionStatus.Restricted))
        {
            sb.AppendLine($"[RESTRICTED] {r.Category}: {r.Title} = {r.CurrentRawValue} ({r.Hive}\\{r.SubKey})");
        }
        sb.AppendLine();

        sb.AppendLine("==================== Hosts & Proxy ====================");
        string hostsPath = Path.Combine(ctx.System32Directory, @"drivers\etc\hosts");
        if (File.Exists(hostsPath))
        {
            try
            {
                foreach (var line in File.ReadAllLines(hostsPath).Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#')))
                {
                    sb.AppendLine($"HOSTS: {line}");
                }
            }
            catch { }
        }
        var proxyEnable = ctx.GetValue("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxyEnable");
        var proxyServer = ctx.GetValue("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxyServer");
        var autoConfig = ctx.GetValue("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", "AutoConfigURL");
        sb.AppendLine($"ProxyEnable={proxyEnable} | ProxyServer={proxyServer} | AutoConfigURL={autoConfig}");
        sb.AppendLine();

        sb.AppendLine("==================== Installed Programs ====================");
        foreach (var uninstKey in new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        })
        {
            foreach (var sub in ctx.GetSubKeyNames("HKLM", uninstKey).Take(120))
            {
                string disp = ctx.GetValue("HKLM", $@"{uninstKey}\{sub}", "DisplayName")?.ToString() ?? "";
                string ver = ctx.GetValue("HKLM", $@"{uninstKey}\{sub}", "DisplayVersion")?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(disp))
                    sb.AppendLine($"{disp} ({ver})");
            }
        }
        sb.AppendLine();

        sb.AppendLine("==================== Firewall Rules (Sample) ====================");
        const string fwKey = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules";
        foreach (var vn in ctx.GetValueNames("HKLM", fwKey).Take(40))
        {
            string val = ctx.GetValue("HKLM", fwKey, vn)?.ToString() ?? "";
            if (val.Contains("App=", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"{vn} => {Sanitize(val)}");
        }
        sb.AppendLine();

        sb.AppendLine("==================== Local Accounts & Restore Points ====================");
        if (ctx.IsOnline)
        {
            foreach (var u in new UserManagerService().GetUsers())
                sb.AppendLine($"User: {Sanitize(u.Username)} | Admin={u.IsAdmin} | Active={u.IsActive}");

            sb.AppendLine(RunSnippet("vssadmin.exe", "list shadows /for=C:", 4000));
        }
        sb.AppendLine();

        sb.AppendLine("==================== Network & DNS ====================");
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
            {
                var ipProps = ni.GetIPProperties();
                string dns = string.Join(", ", ipProps.DnsAddresses.Select(d => d.ToString()));
                sb.AppendLine($"{ni.Name} ({ni.NetworkInterfaceType}) | DNS: {dns}");
            }
        }
        catch { }
        sb.AppendLine();

        sb.AppendLine("==================== Recent Event Log Errors ====================");
        if (ctx.IsOnline)
        {
            sb.AppendLine(RunSnippet("wevtutil.exe", "qe System /q:\"*[System[(Level=1 or Level=2)]]\" /c:10 /rd:true /f:text", 5000));
        }

        sb.AppendLine("==================== End of Report ====================");
        return sb.ToString();
    }

    private static string RunSnippet(string exe, string args, int timeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return "";
            string outStr = p.StandardOutput.ReadToEnd();
            p.WaitForExit(timeoutMs);
            return outStr.Trim();
        }
        catch
        {
            return "";
        }
    }
}
