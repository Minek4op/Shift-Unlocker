using System.Text;
using Microsoft.Win32;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.RegHandler;

namespace ShiftUnlocker.Core.Safety;

public record ActionLogEntry(
    DateTime Timestamp,
    string Category,
    string Description,
    bool Success,
    string? Details = null);

public class UndoItem
{
    public string Description { get; }
    public DateTime Timestamp { get; }
    public Func<Result> RevertAction { get; }

    public UndoItem(string description, Func<Result> revertAction)
    {
        Description = description;
        Timestamp = DateTime.Now;
        RevertAction = revertAction;
    }
}

public class BackupManager
{
    private readonly string _backupRoot;
    private readonly string _sessionFolder;
    private readonly Stack<UndoItem> _undoStack = new();
    private readonly List<ActionLogEntry> _actionLogs = new();

    public bool DryRun { get; set; } = false;
    public string SessionFolderPath => _sessionFolder;
    public IReadOnlyList<ActionLogEntry> ActionLogs => _actionLogs;
    public int UndoCount => _undoStack.Count;

    public BackupManager(string? baseDir = null)
    {
        string root = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _backupRoot = Path.Combine(root, "SU_Backups");
        _sessionFolder = Path.Combine(_backupRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        try
        {
            Directory.CreateDirectory(_sessionFolder);
        }
        catch
        {
            // если носитель только для чтения — пишем бэкап в Temp
            _backupRoot = Path.Combine(Path.GetTempPath(), "SU_Backups");
            _sessionFolder = Path.Combine(_backupRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(_sessionFolder);
        }
    }

    public void Log(string category, string description, bool success = true, string? details = null)
    {
        _actionLogs.Add(new ActionLogEntry(DateTime.Now, category, description, success, details));
    }

    public void RegisterUndo(string description, Func<Result> revertAction)
    {
        _undoStack.Push(new UndoItem(description, revertAction));
    }

    public Result BackupAndSetRegistryValue(
        ITargetContext ctx,
        string hive,
        string subKey,
        string valueName,
        object newValue,
        RegistryValueKind kind,
        string description)
    {
        if (DryRun)
        {
            Log("DryRun", $"[DRY-RUN] Would set {hive}\\{subKey}\\{valueName} = {newValue}");
            return Result.Ok();
        }

        try
        {
            object? prevValue = ctx.GetValue(hive, subKey, valueName);
            bool existed = prevValue != null;
            RegistryValueKind prevKind = existed ? GetValueKindSafe(ctx, hive, subKey, valueName, prevValue!) : kind;

            SaveRegistrySnippet(hive, subKey, valueName, prevValue);

            RegisterUndo($"Revert {hive}\\{subKey}\\{valueName}", () =>
            {
                if (existed && prevValue != null)
                {
                    ctx.SetValue(hive, subKey, valueName, prevValue, prevKind);
                }
                else
                {
                    ctx.DeleteValue(hive, subKey, valueName);
                }
                return Result.Ok();
            });

            ctx.SetValue(hive, subKey, valueName, newValue, kind);
            Log("Registry", description);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Log("Registry", $"{description} FAILED: {ex.Message}", false);
            return Result.Fail(ex.Message);
        }
    }

    public Result BackupAndDeleteRegistryValue(
        ITargetContext ctx,
        string hive,
        string subKey,
        string valueName,
        string description)
    {
        if (DryRun)
        {
            Log("DryRun", $"[DRY-RUN] Would delete {hive}\\{subKey}\\{valueName}");
            return Result.Ok();
        }

        try
        {
            object? prevValue = ctx.GetValue(hive, subKey, valueName);
            if (prevValue == null)
            {
                return Result.Ok();
            }

            RegistryValueKind prevKind = GetValueKindSafe(ctx, hive, subKey, valueName, prevValue);
            SaveRegistrySnippet(hive, subKey, valueName, prevValue);

            RegisterUndo($"Restore {hive}\\{subKey}\\{valueName}", () =>
            {
                ctx.SetValue(hive, subKey, valueName, prevValue, prevKind);
                return Result.Ok();
            });

            ctx.DeleteValue(hive, subKey, valueName);
            Log("Registry", description);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Log("Registry", $"{description} FAILED: {ex.Message}", false);
            return Result.Fail(ex.Message);
        }
    }

    public Result BackupAndDeleteSubKeyTree(
        ITargetContext ctx,
        string hive,
        string subKey,
        string description)
    {
        if (DryRun)
        {
            Log("DryRun", $"[DRY-RUN] Would delete key tree {hive}\\{subKey}");
            return Result.Ok();
        }

        try
        {
            var snapshot = new List<(string SubKeyPath, List<RegValueEntry> Values)>();
            CaptureSubKeySnapshot(ctx, hive, subKey, snapshot);

            if (snapshot.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var node in snapshot)
                {
                    sb.AppendLine(RegFileHandler.ExportToRegFormat($"{hive}\\{node.SubKeyPath}", node.Values));
                }
                string backupFile = Path.Combine(_sessionFolder, $"Key_{Guid.NewGuid():N}.reg");
                File.WriteAllText(backupFile, sb.ToString(), Encoding.Unicode);

                RegisterUndo($"Restore key tree {hive}\\{subKey}", () =>
                {
                    foreach (var node in snapshot)
                    {
                        using var created = ctx.OpenKey(hive, node.SubKeyPath, true);
                        foreach (var v in node.Values)
                        {
                            ctx.SetValue(hive, node.SubKeyPath, v.Name, v.Value, v.Kind);
                        }
                    }
                    return Result.Ok();
                });
            }

            ctx.DeleteSubKeyTree(hive, subKey);
            Log("Registry", description);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Log("Registry", $"{description} FAILED: {ex.Message}", false);
            return Result.Fail(ex.Message);
        }
    }

    private static void CaptureSubKeySnapshot(
        ITargetContext ctx,
        string hive,
        string subKey,
        List<(string SubKeyPath, List<RegValueEntry> Values)> snapshot)
    {
        using var key = ctx.OpenKey(hive, subKey, false);
        if (key == null) return;

        var values = new List<RegValueEntry>();
        foreach (var vName in key.GetValueNames())
        {
            var val = key.GetValue(vName);
            if (val != null)
            {
                values.Add(new RegValueEntry(vName, val, key.GetValueKind(vName)));
            }
        }

        snapshot.Add((subKey, values));

        foreach (var childName in key.GetSubKeyNames())
        {
            string childPath = string.IsNullOrEmpty(subKey) ? childName : $@"{subKey}\{childName}";
            CaptureSubKeySnapshot(ctx, hive, childPath, snapshot);
        }
    }

    private static RegistryValueKind GetValueKindSafe(
        ITargetContext ctx,
        string hive,
        string subKey,
        string valueName,
        object value)
    {
        try
        {
            using var key = ctx.OpenKey(hive, subKey, false);
            if (key != null) return key.GetValueKind(valueName);
        }
        catch { }

        return value switch
        {
            int => RegistryValueKind.DWord,
            long => RegistryValueKind.QWord,
            byte[] => RegistryValueKind.Binary,
            string[] => RegistryValueKind.MultiString,
            _ => RegistryValueKind.String
        };
    }

    public Result BackupFile(string filePath)
    {
        if (!File.Exists(filePath)) return Result.Ok();

        try
        {
            string fileName = Path.GetFileName(filePath);
            string backupDest = Path.Combine(_sessionFolder, $"{Guid.NewGuid():N}_{fileName}");
            File.Copy(filePath, backupDest, true);

            RegisterUndo($"Restore file {filePath}", () =>
            {
                File.Copy(backupDest, filePath, true);
                return Result.Ok();
            });

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"File backup failed: {ex.Message}");
        }
    }

    private void SaveRegistrySnippet(string hive, string subKey, string valueName, object? val)
    {
        try
        {
            var entries = new List<RegValueEntry>();
            if (val != null)
            {
                entries.Add(new RegValueEntry(valueName, val, RegistryValueKind.String));
            }
            string snippet = RegFileHandler.ExportToRegFormat($"{hive}\\{subKey}", entries);
            string filePath = Path.Combine(_sessionFolder, $"reg_{DateTime.Now:HHmmssfff}_{Guid.NewGuid():N}.reg");
            File.WriteAllText(filePath, snippet, Encoding.Unicode);
        }
        catch
        {
            // ошибку записи бэкапа пропускаем
        }
    }

    public Result UndoLast()
    {
        if (_undoStack.Count == 0) return Result.Fail("Undo stack is empty.");

        var item = _undoStack.Pop();
        var res = item.RevertAction();
        Log("Undo", $"Reverted: {item.Description}", res.Success, res.ErrorMessage);
        return res;
    }

    public Result UndoAll()
    {
        int total = _undoStack.Count;
        int failed = 0;
        while (_undoStack.Count > 0)
        {
            var item = _undoStack.Pop();
            var res = item.RevertAction();
            if (!res.Success) failed++;
        }

        Log("Undo", $"Undo all completed: {total - failed} reverted, {failed} failed.");
        return failed == 0 ? Result.Ok() : Result.Fail($"{failed} operations failed during undo.");
    }

    public string ExportLogTxt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Shift Unlocker Action Log ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('-', 60));
        foreach (var entry in _actionLogs)
        {
            string status = entry.Success ? "Успешно" : "Ошибка";
            sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] {status} [{entry.Category}] {entry.Description}");
            if (!string.IsNullOrEmpty(entry.Details))
            {
                sb.AppendLine($"       Details: {entry.Details}");
            }
        }
        return sb.ToString();
    }

    public string ExportLogHtml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Shift Unlocker Log</title>");
        sb.AppendLine("<style>body{background:#000;color:#fff;font-family:Consolas,monospace;padding:20px;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;}th,td{border:1px solid #555;padding:6px;text-align:left;}");
        sb.AppendLine(".ok{color:#00ff00;}.fail{color:#ff5555;}th{background:#222;}</style></head><body>");
        sb.AppendLine("<h2>Shift Unlocker - Activity Log</h2>");
        sb.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("<table><tr><th>Time</th><th>Status</th><th>Category</th><th>Description</th></tr>");
        foreach (var entry in _actionLogs)
        {
            string cls = entry.Success ? "ok" : "fail";
            string status = entry.Success ? "Успешно" : "Ошибка";
            sb.AppendLine($"<tr><td>{entry.Timestamp:HH:mm:ss}</td><td class='{cls}'>{status}</td><td>{entry.Category}</td><td>{System.Net.WebUtility.HtmlEncode(entry.Description)}</td></tr>");
        }
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }
}
