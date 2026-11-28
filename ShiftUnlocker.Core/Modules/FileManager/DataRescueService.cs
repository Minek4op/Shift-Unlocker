using System.Security.Cryptography;
using ShiftUnlocker.Core.Common;

namespace ShiftUnlocker.Core.Modules.FileManager;

public record DataRescueReport(int FilesCopied, int FilesVerified, int Failed, long BytesCopied, List<string> Errors);

public class DataRescueService
{
    public async Task<DataRescueReport> RescueUserFoldersAsync(
        string sourceUserProfile,
        string targetDriveRoot,
        bool includeDesktop,
        bool includeDocuments,
        bool includeDownloads,
        bool includePictures,
        bool verifySha256,
        IProgress<(string File, int Percent)>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var foldersToCopy = new List<string>();
            if (includeDesktop) foldersToCopy.Add("Desktop");
            if (includeDocuments) foldersToCopy.Add("Documents");
            if (includeDownloads) foldersToCopy.Add("Downloads");
            if (includePictures) foldersToCopy.Add("Pictures");

            string destBase = Path.Combine(targetDriveRoot, $"ShiftUnlocker_Rescue_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(destBase);

            var allFiles = new List<(string Src, string Dest)>();
            foreach (var f in foldersToCopy)
            {
                string srcDir = Path.Combine(sourceUserProfile, f);
                if (!Directory.Exists(srcDir)) continue;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
                    {
                        string rel = Path.GetRelativePath(sourceUserProfile, file);
                        allFiles.Add((file, Path.Combine(destBase, rel)));
                    }
                }
                catch { }
            }

            int copied = 0;
            int verified = 0;
            int failed = 0;
            long bytes = 0;
            var errors = new List<string>();

            for (int i = 0; i < allFiles.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var (src, dst) = allFiles[i];
                int pct = allFiles.Count > 0 ? (int)((i + 1) * 100.0 / allFiles.Count) : 100;
                progress?.Report((src, pct));

                try
                {
                    string? dstDir = Path.GetDirectoryName(dst);
                    if (!string.IsNullOrEmpty(dstDir)) Directory.CreateDirectory(dstDir);

                    File.Copy(src, dst, true);
                    copied++;
                    bytes += new FileInfo(dst).Length;

                    if (verifySha256)
                    {
                        if (string.Equals(ComputeSha256(src), ComputeSha256(dst), StringComparison.Ordinal))
                            verified++;
                        else
                            errors.Add($"SHA-256 mismatch: {src}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{src}: {ex.Message}");
                }
            }

            return new DataRescueReport(copied, verified, failed, bytes, errors);
        }, ct);
    }

    private static string ComputeSha256(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(fs));
    }
}
