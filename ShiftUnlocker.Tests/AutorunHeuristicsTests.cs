using ShiftUnlocker.Core.Modules.Autorun;
using Xunit;

namespace ShiftUnlocker.Tests;

public class AutorunHeuristicsTests
{
    [Theory]
    [InlineData("powershell.exe -enc AABBAA==", "", true)]
    [InlineData("powershell -e JABhID0A", "", true)]
    [InlineData("mshta.exe javascript:alert(1)", "", true)]
    [InlineData("cmd.exe /c start evil.exe", "", true)]
    [InlineData(@"C:\Users\User\AppData\Local\Temp\update.exe", @"C:\Users\User\AppData\Local\Temp\update.exe", true)]
    [InlineData(@"C:\$Recycle.Bin\malware.exe", @"C:\$Recycle.Bin\malware.exe", true)]
    [InlineData(@"C:\Files\document.pdf.exe", @"C:\Files\document.pdf.exe", true)]
    [InlineData(@"C:\Windows\System32\notepad.exe", @"C:\Windows\System32\notepad.exe", false)]
    public void AnalyzeSuspicious_ShouldDetectHeuristicThreats(string cmd, string path, bool shouldBeSuspicious)
    {
        var (isSuspicious, reason) = AutorunService.AnalyzeSuspicious(cmd, path, true);
        Assert.Equal(shouldBeSuspicious, isSuspicious);
        if (shouldBeSuspicious)
        {
            Assert.NotEmpty(reason);
        }
    }

    [Fact]
    public void DeduplicateEntries_ShouldRemoveDuplicateFilesAndKeepDistinctCommands()
    {
        var entries = new[]
        {
            new AutorunEntry("1", "RunA", @"C:\Tools\app.exe", @"C:\Tools\app.exe", true, "HKLM Run", "HKLM", "k1", "v1", true, false, ""),
            new AutorunEntry("2", "RunB", @"c:\tools\app.exe --silent", @"c:\tools\app.exe", true, "Winsock LSP", "HKLM", "k2", "v2", true, true, "dup"),
            new AutorunEntry("3", "Cmd1", @"cmd.exe /c a.bat", @"C:\Windows\System32\cmd.exe", true, "HKCU Run", "HKCU", "k3", "v3", true, true, "cmd"),
            new AutorunEntry("4", "Cmd1Dup", @"cmd.exe  /c  a.bat", @"C:\Windows\System32\cmd.exe", true, "Task", "FS_TASK", "k4", "v4", true, true, "cmd"),
            new AutorunEntry("5", "Cmd2", @"cmd.exe /c b.bat", @"C:\Windows\System32\cmd.exe", true, "Task", "FS_TASK", "k5", "v5", true, true, "cmd")
        };

        var result = AutorunService.DeduplicateEntries(entries);

        Assert.Equal(3, result.Count);
        Assert.Equal(["1", "3", "5"], result.Select(x => x.Id).ToArray());
    }
}
