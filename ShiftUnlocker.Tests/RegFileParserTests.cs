using Microsoft.Win32;
using ShiftUnlocker.Core.RegHandler;
using Xunit;

namespace ShiftUnlocker.Tests;

public class RegFileParserTests
{
    [Fact]
    public void ParseRegContent_ShouldParseStringsAndDwordsCorrectly()
    {
        string reg =
@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\TestApp]
@=""DefaultValue""
""StringVal""=""HelloWorld""
""DwordVal""=dword:00000001
""EscapedVal""=""C:\\Windows\\System32\\test.exe""
";

        var entries = RegFileHandler.ParseRegContent(reg);
        Assert.Single(entries);

        var key = entries[0];
        Assert.Equal(@"HKEY_LOCAL_MACHINE\SOFTWARE\TestApp", key.KeyPath);
        Assert.False(key.IsDeletion);
        Assert.Equal(4, key.Values.Count);

        var defVal = key.Values.First(v => v.Name == "");
        Assert.Equal("DefaultValue", defVal.Value);

        var strVal = key.Values.First(v => v.Name == "StringVal");
        Assert.Equal("HelloWorld", strVal.Value);

        var dwVal = key.Values.First(v => v.Name == "DwordVal");
        Assert.Equal(1, dwVal.Value);

        var escVal = key.Values.First(v => v.Name == "EscapedVal");
        Assert.Equal(@"C:\Windows\System32\test.exe", escVal.Value);
    }

    [Fact]
    public void ParseRegContent_ShouldParseHexBinaryAndMultiLine()
    {
        string reg =
@"Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\TestHex]
""BinData""=hex:01,02,03,04,\
  05,06,07,08
";

        var entries = RegFileHandler.ParseRegContent(reg);
        Assert.Single(entries);

        var val = entries[0].Values.First(v => v.Name == "BinData");
        byte[] expected = [1, 2, 3, 4, 5, 6, 7, 8];
        Assert.Equal(expected, (byte[])val.Value);
    }

    [Fact]
    public void ExportToRegFormat_ShouldProduceValidHeaderAndEntries()
    {
        var values = new List<RegValueEntry>
        {
            new("StrKey", "TestString", RegistryValueKind.String),
            new("DwKey", 42, RegistryValueKind.DWord)
        };

        string exported = RegFileHandler.ExportToRegFormat(@"HKEY_LOCAL_MACHINE\SOFTWARE\MyApp", values);

        Assert.Contains("Windows Registry Editor Version 5.00", exported);
        Assert.Contains("[HKEY_LOCAL_MACHINE\\SOFTWARE\\MyApp]", exported);
        Assert.Contains("\"StrKey\"=\"TestString\"", exported);
        Assert.Contains("\"DwKey\"=dword:0000002a", exported);
    }
}
