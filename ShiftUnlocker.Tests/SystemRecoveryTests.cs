using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.SystemRecovery;
using ShiftUnlocker.Core.Safety;
using Xunit;

namespace ShiftUnlocker.Tests;

public class SystemRecoveryTests
{
    [Fact]
    public void AccessibilityCheck_ReturnsValidStatus()
    {
        var service = new SystemRecoveryService();
        var ctx = new OnlineTargetContext();
        var status = service.CheckAccessibilityStatus(ctx);

        Assert.NotNull(status);
        Assert.NotNull(status.SethcTarget);
        Assert.NotNull(status.UtilmanTarget);
    }

    [Fact]
    public void DiskCleanupService_GetVolumes_DoesNotThrow()
    {
        var service = new DiskCleanupService();
        var vols = service.GetVolumes();

        Assert.NotNull(vols);
    }
}
