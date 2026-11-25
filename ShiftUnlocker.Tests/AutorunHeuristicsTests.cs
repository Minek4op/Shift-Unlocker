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
}
