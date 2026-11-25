namespace ShiftUnlocker.Core.Modules.MbrBoot;

public record PhysicalDriveInfo(
    int DriveIndex,
    string DevicePath,
    string FriendlyName,
    long SizeBytes,
    string PartitionStyle,
    int PartitionCount);

public record MbrPartitionEntry(
    int Index,
    bool IsBootable,
    byte PartitionType,
    string PartitionTypeName,
    uint StartLba,
    uint SectorCount,
    long SizeBytes);

public class MbrSectorInfo
{
    public byte[] RawBytes { get; }
    public uint DiskSignature { get; }
    public bool HasValidSignature55AA { get; }
    public bool IsGptProtectiveMbr { get; }
    public List<MbrPartitionEntry> Partitions { get; } = new();
    public List<string> Anomalies { get; } = new();
    public string HexDump { get; }

    public MbrSectorInfo(byte[] rawBytes)
    {
        RawBytes = rawBytes;
        HexDump = FormatHex(rawBytes);

        if (rawBytes.Length < 512)
        {
            HasValidSignature55AA = false;
            Anomalies.Add("Длина сектора меньше 512 байт!");
            return;
        }

        // сигнатура MBR 0x55AA в конце сектора
        HasValidSignature55AA = rawBytes[510] == 0x55 && rawBytes[511] == 0xAA;
        if (!HasValidSignature55AA)
        {
            Anomalies.Add("ОТСУТСТВУЕТ загрузочная сигнатура 0x55AA в конце сектора!");
        }

        // подпись диска по смещению 440
        DiskSignature = BitConverter.ToUInt32(rawBytes, 440);

        // ищем следы буткита/локера в коде загрузчика
        string bootText = ExtractAsciiStrings(rawBytes[..440]);
        string[] suspiciousWords = ["lock", "ransom", "crypt", "pay", "tor", "onion", "wallet"];
        foreach (var word in suspiciousWords)
        {
            if (bootText.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                Anomalies.Add($"В коде MBR обнаружено подозрительное слово '{word}': возможен MBR-локер!");
            }
        }

        // разбираем 4 записи таблицы разделов
        bool gptFound = false;
        int activeCount = 0;

        for (int i = 0; i < 4; i++)
        {
            int offset = 446 + (i * 16);
            byte bootInd = rawBytes[offset];
            byte partType = rawBytes[offset + 4];
            uint startLba = BitConverter.ToUInt32(rawBytes, offset + 8);
            uint numSectors = BitConverter.ToUInt32(rawBytes, offset + 12);

            bool isBootable = bootInd == 0x80;
            if (isBootable) activeCount++;

            if (partType == 0xEE)
            {
                gptFound = true;
            }

            if (partType != 0)
            {
                long size = (long)numSectors * 512;
                string typeName = GetPartitionTypeName(partType);
                Partitions.Add(new MbrPartitionEntry(i + 1, isBootable, partType, typeName, startLba, numSectors, size));
            }
        }

        IsGptProtectiveMbr = gptFound;
        if (gptFound)
        {
            Anomalies.Add("Диск имеет структуру GPT (Protective MBR, тип 0xEE). Перезапись кодом MBR уничтожит разметку!");
        }

        if (activeCount > 1)
        {
            Anomalies.Add($"Аномалия таблицы: активно более одного раздела ({activeCount} активных разделов)!");
        }

        if (Partitions.Count == 0 && !gptFound)
        {
            Anomalies.Add("Таблица разделов пуста (все 4 записи пусты или зашифрованы)!");
        }
    }

    private static string ExtractAsciiStrings(byte[] bytes)
    {
        var sb = new System.Text.StringBuilder();
        foreach (byte b in bytes)
        {
            if (b is >= 32 and <= 126)
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }

    private static string FormatHex(byte[] bytes)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < bytes.Length; i += 16)
        {
            sb.Append($"{i:X4}: ");
            int count = Math.Min(16, bytes.Length - i);

            for (int j = 0; j < 16; j++)
            {
                if (j < count)
                    sb.Append($"{bytes[i + j]:X2} ");
                else
                    sb.Append("   ");
            }

            sb.Append(" | ");
            for (int j = 0; j < count; j++)
            {
                byte b = bytes[i + j];
                sb.Append(b is >= 32 and <= 126 ? (char)b : '.');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string GetPartitionTypeName(byte type) => type switch
    {
        0x00 => "Empty",
        0x01 => "FAT12",
        0x04 => "FAT16 (<32MB)",
        0x05 => "Extended (CHS)",
        0x06 => "FAT16 (>32MB)",
        0x07 => "NTFS / exFAT",
        0x0B => "FAT32 (CHS)",
        0x0C => "FAT32 (LBA)",
        0x0E => "FAT16 (LBA)",
        0x0F => "Extended (LBA)",
        0x27 => "Windows Recovery (WinRE)",
        0x82 => "Linux Swap",
        0x83 => "Linux Native",
        0xEE => "GPT Protective MBR",
        0xEF => "EFI System Partition (ESP)",
        _ => $"Unknown (0x{type:X2})"
    };
}
