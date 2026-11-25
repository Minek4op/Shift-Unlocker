using System.Security.Cryptography;
using System.Text.Json;
using ShiftUnlocker.Core.Common;

namespace ShiftUnlocker.Core.Safety;

public record QuarantineItem(
    string Sha256,
    string OriginalPath,
    string FileName,
    long FileSize,
    DateTime QuarantineDate,
    string Reason,
    FileAttributes OriginalAttributes,
    string StorageFileName);

public class QuarantineManager
{
    private readonly string _quarantineDir;

    public string QuarantineDirectory => _quarantineDir;

    public QuarantineManager(string? baseDir = null)
    {
        string root = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _quarantineDir = Path.Combine(root, "SU_Quarantine");

        try
        {
            Directory.CreateDirectory(_quarantineDir);
        }
        catch
        {
            _quarantineDir = Path.Combine(Path.GetTempPath(), "SU_Quarantine");
            Directory.CreateDirectory(_quarantineDir);
        }
    }

    public Result<QuarantineItem> QuarantineFile(string filePath, string reason)
    {
        if (!File.Exists(filePath))
        {
            return Result<QuarantineItem>.Fail($"File not found: {filePath}");
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var originalAttrs = fileInfo.Attributes;

            // снимаем системные атрибуты перед переносом
            File.SetAttributes(filePath, FileAttributes.Normal);

            string sha256;
            using (var fs = File.OpenRead(filePath))
            {
                using var sha = SHA256.Create();
                sha256 = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
            }

            string safeName = Path.GetFileName(filePath);
            string storageFileName = $"{sha256}_{safeName}.vir_dat";
            string targetPath = Path.Combine(_quarantineDir, storageFileName);

            File.Move(filePath, targetPath, true);

            var item = new QuarantineItem(
                sha256,
                filePath,
                safeName,
                fileInfo.Length,
                DateTime.Now,
                reason,
                originalAttrs,
                storageFileName);

            string manifestPath = Path.Combine(_quarantineDir, $"{sha256}.json");
            string json = JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestPath, json);

            return Result<QuarantineItem>.Ok(item);
        }
        catch (Exception ex)
        {
            return Result<QuarantineItem>.Fail($"Failed to quarantine file: {ex.Message}");
        }
    }

    public List<QuarantineItem> ListQuarantined()
    {
        var list = new List<QuarantineItem>();
        if (!Directory.Exists(_quarantineDir)) return list;

        foreach (var manifestFile in Directory.GetFiles(_quarantineDir, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(manifestFile);
                var item = JsonSerializer.Deserialize<QuarantineItem>(json);
                if (item != null && File.Exists(Path.Combine(_quarantineDir, item.StorageFileName)))
                {
                    list.Add(item);
                }
            }
            catch
            {
                // битый json пропускаем
            }
        }

        return list;
    }

    public Result RestoreFile(string sha256)
    {
        string manifestPath = Path.Combine(_quarantineDir, $"{sha256}.json");
        if (!File.Exists(manifestPath))
        {
            return Result.Fail("Quarantine record not found.");
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            var item = JsonSerializer.Deserialize<QuarantineItem>(json);
            if (item == null) return Result.Fail("Corrupted quarantine record.");

            string storagePath = Path.Combine(_quarantineDir, item.StorageFileName);
            if (!File.Exists(storagePath)) return Result.Fail("Quarantined payload file not found.");

            string? targetFolder = Path.GetDirectoryName(item.OriginalPath);
            if (!string.IsNullOrEmpty(targetFolder) && !Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            File.Move(storagePath, item.OriginalPath, true);
            try
            {
                File.SetAttributes(item.OriginalPath, item.OriginalAttributes);
            }
            catch { }

            File.Delete(manifestPath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to restore file: {ex.Message}");
        }
    }

    public Result DeletePermanently(string sha256)
    {
        string manifestPath = Path.Combine(_quarantineDir, $"{sha256}.json");
        if (!File.Exists(manifestPath))
        {
            return Result.Fail("Quarantine record not found.");
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            var item = JsonSerializer.Deserialize<QuarantineItem>(json);
            if (item != null)
            {
                string storagePath = Path.Combine(_quarantineDir, item.StorageFileName);
                if (File.Exists(storagePath))
                {
                    File.Delete(storagePath);
                }
            }

            File.Delete(manifestPath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to delete quarantined item: {ex.Message}");
        }
    }
}
