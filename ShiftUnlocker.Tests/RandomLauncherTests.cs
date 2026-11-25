using System.Diagnostics;
using ShiftUnlocker.Core.Security;
using Xunit;

namespace ShiftUnlocker.Tests;

public class RandomLauncherTests
{
    [Fact]
    public void GenerateRandomName_ShouldProduceCorrectLengthAndCharset()
    {
        string name = RandomLauncher.GenerateRandomName(12);
        Assert.Equal(12, name.Length);
        Assert.True(name.All(char.IsLetterOrDigit));
    }

    [Theory]
    [InlineData("ShiftUnlocker.exe", false)]
    [InlineData("ShiftUnlocker.App.exe", false)]
    [InlineData("k9x2m4p8z1.exe", true)]
    [InlineData("a1b2c3d4e5f6g7h8i.exe", true)]
    [InlineData("a.exe", false)]
    public void IsRandomlyNamed_ShouldIdentifyKnownVsRandomExe(string fileName, bool expectedRandom)
    {
        bool result = RandomLauncher.IsRandomlyNamed(fileName);
        Assert.Equal(expectedRandom, result);
    }
}
