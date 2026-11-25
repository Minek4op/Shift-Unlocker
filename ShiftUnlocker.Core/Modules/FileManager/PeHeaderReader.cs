namespace ShiftUnlocker.Core.Modules.FileManager;

public static class PeHeaderReader
{
    public static PeHeaderInfo ReadPeInfo(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new PeHeaderInfo(false, "Не найден", null, "", 0);
        }

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(fs);

            if (fs.Length < 64)
            {
                return new PeHeaderInfo(false, "Слишком мал", null, "", 0);
            }

            // сигнатура MZ
            ushort dosMagic = reader.ReadUInt16();
            if (dosMagic != 0x5A4D)
            {
                return new PeHeaderInfo(false, "Не PE-файл", null, "", 0);
            }

            // смещение PE-заголовка (0x3C)
            fs.Seek(0x3C, SeekOrigin.Begin);
            int peOffset = reader.ReadInt32();

            if (peOffset < 0 || peOffset + 24 > fs.Length)
            {
                return new PeHeaderInfo(false, "Поврежденный PE", null, "", 0);
            }

            // сигнатура PE\0\0
            fs.Seek(peOffset, SeekOrigin.Begin);
            uint peSig = reader.ReadUInt32();
            if (peSig != 0x00004550)
            {
                return new PeHeaderInfo(false, "Неверная PE сигнатура", null, "", 0);
            }

            // заголовок COFF
            ushort machine = reader.ReadUInt16();
            ushort numberOfSections = reader.ReadUInt16();
            uint timeDateStamp = reader.ReadUInt32();
            reader.ReadUInt32(); // PointerToSymbolTable
            reader.ReadUInt32(); // NumberOfSymbols
            ushort sizeOfOptionalHeader = reader.ReadUInt16();
            reader.ReadUInt16(); // Characteristics

            string arch = machine switch
            {
                0x014c => "x86 (32-bit)",
                0x8664 => "x64 (64-bit)",
                0xaa64 => "ARM64",
                0x0200 => "Itanium (IA-64)",
                _ => $"Unknown (0x{machine:X4})"
            };

            DateTime? compileDate = null;
            try
            {
                compileDate = DateTimeOffset.FromUnixTimeSeconds(timeDateStamp).LocalDateTime;
            }
            catch { }

            string subsystemStr = "";
            if (sizeOfOptionalHeader >= 68 && fs.Position + 68 <= fs.Length)
            {
                long optStart = fs.Position;
                // подсистема лежит по смещению 68
                fs.Seek(optStart + 68, SeekOrigin.Begin);
                ushort subsystem = reader.ReadUInt16();
                subsystemStr = subsystem switch
                {
                    1 => "Native",
                    2 => "Windows GUI",
                    3 => "Windows Console (CUI)",
                    7 => "POSIX Console",
                    9 => "Windows CE GUI",
                    10 => "EFI Application",
                    14 => "Xbox",
                    _ => $"Subsystem_{subsystem}"
                };
            }

            return new PeHeaderInfo(true, arch, compileDate, subsystemStr, numberOfSections);
        }
        catch
        {
            return new PeHeaderInfo(false, "Ошибка чтения", null, "", 0);
        }
    }
}
