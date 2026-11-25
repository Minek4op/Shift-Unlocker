using ShiftUnlocker.Core.Modules.MbrBoot;
using Xunit;

namespace ShiftUnlocker.Tests;

public class MbrParserTests
{
    [Fact]
    public void MbrSectorInfo_ShouldValidateValidMbr()
    {
        byte[] sector = new byte[512];
        // сигнатура 0x55AA
        sector[510] = 0x55;
        sector[511] = 0xAA;

        // подпись диска по смещению 440
        sector[440] = 0x78;
        sector[441] = 0x56;
        sector[442] = 0x34;
        sector[443] = 0x12;

        // первый раздел NTFS с 2048 сектора
        sector[446] = 0x80;
        sector[450] = 0x07;
        BitConverter.GetBytes(2048u).CopyTo(sector, 454);
        BitConverter.GetBytes(1000000u).CopyTo(sector, 458);

        var mbr = new MbrSectorInfo(sector);

        Assert.True(mbr.HasValidSignature55AA);
        Assert.Equal(0x12345678u, mbr.DiskSignature);
        Assert.False(mbr.IsGptProtectiveMbr);
        Assert.Single(mbr.Partitions);

        var p = mbr.Partitions[0];
        Assert.True(p.IsBootable);
        Assert.Equal(0x07, p.PartitionType);
        Assert.Equal(2048u, p.StartLba);
        Assert.Equal(1000000u, p.SectorCount);
    }

    [Fact]
    public void MbrSectorInfo_ShouldDetectMissingSignature()
    {
        byte[] sector = new byte[512];
        sector[510] = 0x00;
        sector[511] = 0x00;

        var mbr = new MbrSectorInfo(sector);

        Assert.False(mbr.HasValidSignature55AA);
        Assert.Contains(mbr.Anomalies, a => a.Contains("0x55AA"));
    }

    [Fact]
    public void MbrSectorInfo_ShouldDetectGptProtectiveMbr()
    {
        byte[] sector = new byte[512];
        sector[510] = 0x55;
        sector[511] = 0xAA;

        // тип 0xEE — защитный MBR для GPT
        sector[450] = 0xEE;
        BitConverter.GetBytes(1u).CopyTo(sector, 454);
        BitConverter.GetBytes(0xFFFFFFFFu).CopyTo(sector, 458);

        var mbr = new MbrSectorInfo(sector);

        Assert.True(mbr.IsGptProtectiveMbr);
        Assert.Contains(mbr.Anomalies, a => a.Contains("GPT"));
    }

    [Fact]
    public void MbrSectorInfo_ShouldDetectSuspiciousRansomTextInCode()
    {
        byte[] sector = new byte[512];
        sector[510] = 0x55;
        sector[511] = 0xAA;

        // тестовая строка локера в загрузчике
        byte[] ransom = System.Text.Encoding.ASCII.GetBytes("Your hard drive is locked! Send ransom to wallet.");
        Array.Copy(ransom, 0, sector, 10, ransom.Length);

        var mbr = new MbrSectorInfo(sector);

        Assert.Contains(mbr.Anomalies, a => a.Contains("подозрительное слово"));
    }
}
