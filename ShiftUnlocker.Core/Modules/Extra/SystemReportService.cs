using System.Text;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.QuickScan;
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
}
