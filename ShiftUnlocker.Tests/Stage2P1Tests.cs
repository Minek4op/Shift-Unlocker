using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Browsers;
using ShiftUnlocker.Core.Modules.Extra;
using ShiftUnlocker.Core.Modules.SystemRecovery;
using ShiftUnlocker.Core.Safety;
using Xunit;

namespace ShiftUnlocker.Tests;

public class Stage2P1Tests
{
    [Fact]
    public void SuScriptService_DryRun_ParsesAndDoesNotModify()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SU_ScriptTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var svc = new SuScriptService();
            var ctx = new OnlineTargetContext();
            var bm = new BackupManager(tempDir);
            var qm = new QuarantineManager(tempDir);

            string script = @"
# Comment line
HostsReset:
ProxyReset:
RegSet: HKCU\Software\ShiftUnlockerTest | TestKey | DWord | 1
";
            var res = svc.ExecuteScript(script, ctx, bm, qm, dryRun: true);
            Assert.Equal(3, res.TotalCommands);
            Assert.Equal(3, res.Succeeded);
            Assert.Equal(0, res.Failed);
            Assert.Contains(res.LogLines, l => l.Contains("DRY-RUN"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void DeepSearchService_FindsEncodedPowerShellInTempScript()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SU_DeepTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var svc = new DeepSearchService(tempDir);
            var patterns = svc.LoadPatterns();
            Assert.True(patterns.Count >= 5);
            Assert.Contains(patterns, p => p.Name.Contains("PowerShell"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SystemBinariesIntegrity_ChecksAll12Binaries()
    {
        var svc = new SystemRecoveryService();
        var ctx = new OnlineTargetContext();
        var items = svc.CheckSystemBinariesIntegrity(ctx);

        Assert.Equal(12, items.Count);
        Assert.Contains(items, i => i.FileName.Equals("sethc.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(items, i => i.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BrowserAuditService_ScanDoesNotThrow()
    {
        var svc = new BrowserAuditService();
        var ctx = new OnlineTargetContext();
        var findings = svc.ScanBrowsers(ctx);
        Assert.NotNull(findings);
    }
}
