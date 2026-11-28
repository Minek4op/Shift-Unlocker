using Microsoft.Win32;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Security;

public class RegistryGuardService : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly List<Task> _watchers = new();
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public event Action<string>? OnKeyChanged;

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var watchList = new (RegistryHive Hive, string SubKey)[]
        {
            (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"),
            (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options")
        };

        foreach (var (hive, subKey) in watchList)
        {
            _watchers.Add(Task.Run(() => WatchKeyLoop(hive, subKey, token), token));
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _watchers.Clear();
    }

    private void WatchKeyLoop(RegistryHive hive, string subKey, CancellationToken ct)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey, false);
            if (key == null) return;

            IntPtr hKey = key.Handle.DangerousGetHandle();
            using var evt = new AutoResetEvent(false);

            while (!ct.IsCancellationRequested)
            {
                int rc = Advapi32.RegNotifyChangeKeyValue(
                    hKey,
                    true,
                    Advapi32.REG_NOTIFY_CHANGE_NAME | Advapi32.REG_NOTIFY_CHANGE_LAST_SET,
                    evt.SafeWaitHandle.DangerousGetHandle(),
                    true);

                if (rc != 0) break;

                int idx = WaitHandle.WaitAny(new[] { evt, ct.WaitHandle });
                if (idx == 1 || ct.IsCancellationRequested) break;

                OnKeyChanged?.Invoke($@"{hive}\{subKey}");
            }
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
