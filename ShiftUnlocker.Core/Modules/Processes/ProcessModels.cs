namespace ShiftUnlocker.Core.Modules.Processes;

public record ProcessInfoItem(
    int Id,
    int ParentId,
    string Name,
    string FilePath,
    long WorkingSetBytes,
    int ThreadCount,
    int HandleCount,
    int SessionId,
    DateTime? StartTime,
    bool IsSuspicious,
    string SuspiciousReason);

public record NetworkConnectionItem(
    string Protocol,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort,
    string State,
    int OwningPid,
    string ProcessName);

public record ServiceInfoItem(
    string ServiceName,
    string DisplayName,
    string ImagePath,
    uint StartType,
    string StartTypeDescription,
    bool IsNonMicrosoft,
    bool IsDriver);
