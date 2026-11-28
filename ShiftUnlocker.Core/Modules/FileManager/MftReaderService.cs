using System.Runtime.InteropServices;
using System.Text;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Modules.FileManager;

public record MftDeletedEntry(long RecordNumber, string FileName, bool IsDirectory);

public class MftReaderService
{
    // Базовое read-only чтение первых записей $MFT на NTFS-томе (требует прав администратора)
    public (Result Status, List<MftDeletedEntry> DeletedEntries) ScanDeletedMftEntries(string driveLetter, int maxRecords = 512)
    {
        var list = new List<MftDeletedEntry>();
        string cleanDrive = driveLetter.Trim().TrimEnd('\\', '/');
        if (!cleanDrive.EndsWith(':')) cleanDrive += ":";
        string volumePath = $@"\\.\{cleanDrive}";

        using var hVol = Kernel32.CreateFileW(
            volumePath,
            DiskIoControlConstants.GENERIC_READ,
            DiskIoControlConstants.FILE_SHARE_READ | DiskIoControlConstants.FILE_SHARE_WRITE,
            IntPtr.Zero,
            DiskIoControlConstants.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (hVol.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            return (Result.Fail($"Не удалось открыть том {cleanDrive} (нужны права Администратора): {Win32Error.GetMessage(err)}"), list);
        }

        byte[] boot = new byte[512];
        if (!Kernel32.ReadFile(hVol, boot, 512, out uint read, IntPtr.Zero) || read < 512)
            return (Result.Fail("Не удалось прочитать загрузочный сектор тома."), list);

        string oem = Encoding.ASCII.GetString(boot, 3, 4);
        if (!string.Equals(oem, "NTFS", StringComparison.Ordinal))
            return (Result.Fail($"Том {cleanDrive} не является NTFS ({oem})."), list);

        ushort bytesPerSector = BitConverter.ToUInt16(boot, 0x0B);
        byte sectorsPerCluster = boot[0x0D];
        long mftCluster = BitConverter.ToInt64(boot, 0x30);
        long mftOffset = mftCluster * bytesPerSector * sectorsPerCluster;

        const int recordSize = 1024;
        int totalBytes = maxRecords * recordSize;
        byte[] mftBuf = new byte[totalBytes];

        Kernel32.SetFilePointerEx(hVol, mftOffset, out _, 0);

        if (!Kernel32.ReadFile(hVol, mftBuf, (uint)totalBytes, out uint mftRead, IntPtr.Zero))
            return (Result.Fail("Не удалось прочитать записи $MFT."), list);

        int count = (int)(mftRead / recordSize);
        for (int i = 0; i < count; i++)
        {
            int off = i * recordSize;
            if (Encoding.ASCII.GetString(mftBuf, off, 4) != "FILE") continue;

            ushort flags = BitConverter.ToUInt16(mftBuf, off + 0x16);
            bool inUse = (flags & 0x01) != 0;
            bool isDir = (flags & 0x02) != 0;
            if (inUse) continue; // интересуют удалённые (неиспользуемые) записи

            string? name = TryExtractFileNameAttr(mftBuf, off, recordSize);
            if (!string.IsNullOrWhiteSpace(name))
            {
                list.Add(new MftDeletedEntry(i, name, isDir));
            }
        }

        return (Result.Ok(), list);
    }

    private static string? TryExtractFileNameAttr(byte[] buf, int recOff, int recSize)
    {
        ushort firstAttr = BitConverter.ToUInt16(buf, recOff + 0x14);
        int pos = recOff + firstAttr;
        int end = recOff + recSize;

        while (pos + 8 < end)
        {
            uint attrType = BitConverter.ToUInt32(buf, pos);
            if (attrType == 0xFFFFFFFF || attrType == 0) break;
            uint attrLen = BitConverter.ToUInt32(buf, pos + 4);
            if (attrLen < 16 || pos + attrLen > end) break;

            if (attrType == 0x30 && buf[pos + 8] == 0) // $FILE_NAME resident
            {
                ushort contentOff = BitConverter.ToUInt16(buf, pos + 20);
                int fnBase = pos + contentOff;
                if (fnBase + 66 < end)
                {
                    byte nameLen = buf[fnBase + 64];
                    int nameBytes = nameLen * 2;
                    if (fnBase + 66 + nameBytes <= end && nameLen > 0)
                    {
                        return Encoding.Unicode.GetString(buf, fnBase + 66, nameBytes);
                    }
                }
            }

            pos += (int)attrLen;
        }

        return null;
    }
}
