using ShiftUnlocker.Core.Modules.FileManager;
using Xunit;

namespace ShiftUnlocker.Tests;

public class PeHeaderReaderTests
{
    [Fact]
    public void ReadPeInfo_ShouldParseSynthesizedPe()
    {
        string tempPe = Path.Combine(Path.GetTempPath(), $"test_pe_{Guid.NewGuid():N}.exe");
        try
        {
            using (var fs = File.Create(tempPe))
            using (var bw = new BinaryWriter(fs))
            {
                // заголовок MZ
                bw.Write((ushort)0x5A4D);
                // добиваем до 0x3C
                bw.Write(new byte[58]);
                // указатель на PE-заголовок
                bw.Write(128);

                // выравниваем до 128
                int pad = 128 - (int)fs.Position;
                bw.Write(new byte[pad]);

                // сигнатура PE
                bw.Write(0x00004550u);

                // заголовок COFF
                bw.Write((ushort)0x8664); // Machine = x64
                bw.Write((ushort)4);      // NumberOfSections = 4
                bw.Write(1600000000u);    // TimeDateStamp
                bw.Write(0u);             // PointerToSymbolTable
                bw.Write(0u);             // NumberOfSymbols
                bw.Write((ushort)240);    // SizeOfOptionalHeader
                bw.Write((ushort)0x0022); // Characteristics

                // добираемся до поля Subsystem
                bw.Write(new byte[68]);
                bw.Write((ushort)2);      // Windows GUI
            }

            var peInfo = PeHeaderReader.ReadPeInfo(tempPe);

            Assert.True(peInfo.IsValidPe);
            Assert.Equal("x64 (64-bit)", peInfo.Architecture);
            Assert.Equal("Windows GUI", peInfo.Subsystem);
            Assert.Equal(4, peInfo.NumberOfSections);
        }
        finally
        {
            if (File.Exists(tempPe)) File.Delete(tempPe);
        }
    }
}
