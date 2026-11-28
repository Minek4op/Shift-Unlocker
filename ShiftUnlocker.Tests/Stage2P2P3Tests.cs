using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Extra;
using ShiftUnlocker.Core.Safety;
using Xunit;

namespace ShiftUnlocker.Tests;

public class Stage2P2P3Tests
{
    [Fact]
    public void RansomwareHelper_LoadsDatabaseAndAnalyzes()
    {
        var svc = new RansomwareHelperService();
        var db = svc.LoadExtensionDatabase();
        Assert.True(db.ContainsKey(".lockbit"));
        Assert.Equal("LockBit", db[".lockbit"]);

        var rep = svc.AnalyzeUserFiles(new OnlineTargetContext());
        Assert.NotNull(rep.RansomNotesFound);
    }

    [Fact]
    public void TreatmentProfileService_ExportAndImport_Works()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"su_prof_{Guid.NewGuid():N}.json");
        try
        {
            var svc = new TreatmentProfileService();
            var builtIn = svc.GetBuiltInProfiles();
            Assert.Equal(3, builtIn.Count);

            var expRes = svc.ExportProfile(builtIn[0], tempFile);
            Assert.True(expRes.Success);

            var imported = svc.ImportProfile(tempFile);
            Assert.NotNull(imported);
            Assert.Equal(builtIn[0].Name, imported.Name);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void DiagnosticsCleanupService_TempPreview_ReturnsEntries()
    {
        var svc = new DiagnosticsCleanupService();
        var preview = svc.GetTempCachePreview(new OnlineTargetContext());
        Assert.True(preview.Count >= 3);
    }
}
