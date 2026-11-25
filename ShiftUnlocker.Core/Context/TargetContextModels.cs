namespace ShiftUnlocker.Core.Context;

public record WindowsInstallationInfo(
    string DriveLetter,
    string WindowsDirectory,
    string ProductName,
    string VersionString,
    DateTime? InstallDate,
    bool IsCurrentOS,
    bool IsWinPE);

public record UserProfileInfo(
    string UserName,
    string ProfilePath,
    string NtUserDatPath,
    string? LoadedHiveKey);
