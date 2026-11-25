using ShiftUnlocker.Core.Modules.Launcher;
using ShiftUnlocker.Core.Modules.Restrictions;
using Xunit;

namespace ShiftUnlocker.Tests;

public class CustomItemsTests
{
    [Fact]
    public void RestrictionsService_AddAndRemoveCustomRestriction_Works()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SU_RestTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var svc = new RestrictionsService(tempDir);
            var dto = new CustomRestrictionDto
            {
                Title = "Test Lock",
                Category = "Пользовательские",
                Hive = "HKCU",
                SubKey = @"Software\Test\Policy",
                ValueName = "TestVal",
                RestrictedValue = "1",
                AllowedValue = "0",
                ValueKind = "DWord",
                DeleteToAllow = true,
                Description = "Desc"
            };

            var addRes = svc.AddCustomRestriction(dto);
            Assert.True(addRes.Success);

            var loaded = svc.LoadCustomRestrictions();
            Assert.Single(loaded);
            Assert.True(loaded[0].IsCustom);
            Assert.Equal("Test Lock", loaded[0].Title);

            var remRes = svc.RemoveCustomRestriction(loaded[0].Id);
            Assert.True(remRes.Success);
            Assert.Empty(svc.LoadCustomRestrictions());
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void LauncherService_SaveAndRemoveCustomUtility_Works()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SU_LaunchTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var svc = new LauncherService(tempDir);
            var custom = new UtilityItem(
                "u1",
                "MyTool",
                "Пользовательские",
                "Custom",
                @"C:\NonExistent\mytool.exe",
                "",
                true,
                true,
                false,
                false,
                true);

            var builtIn = new UtilityItem(
                "b1",
                "BuiltInTool",
                "Система",
                "BuiltIn",
                @"C:\Windows\notepad.exe",
                "",
                true,
                false,
                true,
                true,
                false);

            svc.SaveConfigUtilities(new[] { custom, builtIn });

            var all = svc.GetAllUtilities();
            var foundCustom = all.FirstOrDefault(u => u.Id == "u1");
            Assert.NotNull(foundCustom);
            Assert.True(foundCustom.IsUserAdded);

            var remRes = svc.RemoveCustomUtility("u1");
            Assert.True(remRes.Success);

            var allAfter = svc.GetAllUtilities();
            Assert.DoesNotContain(allAfter, u => u.Id == "u1");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
