using ShiftUnlocker.Core.Modules.FileManager;
using Xunit;

namespace ShiftUnlocker.Tests;

public class FileManagerServiceTests
{
    [Fact]
    public void CreateFolder_Rename_Unhide_Delete_WorksCorrectly()
    {
        var service = new FileManagerService();
        string tempDir = Path.Combine(Path.GetTempPath(), "SU_TestDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // создание папки
            var resCreate = service.CreateFolder(tempDir, "SubTestFolder");
            Assert.True(resCreate.Success);
            string subPath = Path.Combine(tempDir, "SubTestFolder");
            Assert.True(Directory.Exists(subPath));

            // переименование
            var resRename = service.RenameItem(subPath, "RenamedSubFolder");
            Assert.True(resRename.Success);
            string renamedPath = Path.Combine(tempDir, "RenamedSubFolder");
            Assert.True(Directory.Exists(renamedPath));
            Assert.False(Directory.Exists(subPath));

            // снятие скрытности
            string testFile = Path.Combine(renamedPath, "test.txt");
            File.WriteAllText(testFile, "hello");
            File.SetAttributes(testFile, FileAttributes.Hidden | FileAttributes.ReadOnly);

            var resUnhide = service.UnhideItem(testFile);
            Assert.True(resUnhide.Success);
            var attrs = File.GetAttributes(testFile);
            Assert.False(attrs.HasFlag(FileAttributes.Hidden));

            // удаление
            var resDel = service.DeleteItem(testFile);
            Assert.True(resDel.Success);
            Assert.False(File.Exists(testFile));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
