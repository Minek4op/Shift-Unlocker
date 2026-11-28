using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using ShiftUnlocker.App.UI;
using ShiftUnlocker.Core.Security;

namespace ShiftUnlocker.App;

static class Program
{
    private static Mutex? _instanceMutex;

    [STAThread]
    static void Main(string[] args)
    {
        // глобальный перехват ошибок, чтобы не падало молча
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            string err = e.ExceptionObject?.ToString() ?? "Unknown fatal error";
            MessageBox.Show($"Критическая ошибка приложения:\n\n{err}", "Shift Unlocker - Фатальный сбой", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        Application.ThreadException += (s, e) =>
        {
            MessageBox.Show($"Ошибка в потоке UI:\n\n{e.Exception}", "Shift Unlocker - Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        try
        {
            // запрашиваем права администратора при старте; если отказали — продолжаем без них
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            bool noElevate = args.Any(a => a.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase));
            if (!isAdmin && !noElevate)
            {
                try
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        var forwardArgs = string.Join(" ", args.Select(a => $"\"{a}\"")) + " --no-elevate";
                        var psi = new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = forwardArgs.Trim(),
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                        return;
                    }
                }
                catch
                {
                    // пользователь отказал в UAC — запускаемся с обычными правами
                }
            }

            // включаем нужные привилегии
            PrivilegeManager.EnableAllPrivileges();

            // чистим старые осиротевшие клоны в %TEMP%
            RandomLauncher.CleanupOrphanedClones();

            // запуск копии под случайным именем от киллеров
            bool noRandom = args.Any(a => a.Equals("--no-random", StringComparison.OrdinalIgnoreCase));
            if (!noRandom)
            {
                string? spawnedExe = RandomLauncher.TrySpawnRandomClone(args);
                if (spawnedExe != null)
                {
                    // клон запущен, исходный процесс закрываем
                    return;
                }
            }

            // проверка на второй экземпляр
            try
            {
                string mutexName = "Local\\SU_" + RandomLauncher.GenerateRandomName(12);
                _instanceMutex = new Mutex(true, mutexName, out bool createdNew);
            }
            catch
            {
                // если мьютекс не взлетел — не критично
            }

            // старт интерфейса
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());

            if (_instanceMutex != null)
            {
                GC.KeepAlive(_instanceMutex);
            }

            // если работали из временного клона — удаляем его после закрытия окна
            if (args.Any(a => a.Equals(RandomLauncher.ArgSpawned, StringComparison.OrdinalIgnoreCase)
                             || a.Equals("--spawned", StringComparison.OrdinalIgnoreCase)))
            {
                string? selfPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(selfPath) && RandomLauncher.IsRandomlyNamed(selfPath))
                {
                    RandomLauncher.ScheduleSelfDelete(selfPath);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка запуска Shift Unlocker:\n\n{ex}", "Shift Unlocker - Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
