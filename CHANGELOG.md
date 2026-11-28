# Changelog

## [2.0.0] — 2026-11-28
### Добавлено
- **P1/P2/P3 модули восстановления и диагностики:**
  - Расширенное сканирование автозагрузки в `AutorunService`: COM-hijack (`HKCU\...\CLSID`), AppCompat shims, Time Providers, Print Monitors, Winsock LSP, PowerShell-профили, переменные среды (`COR_PROFILER`, `__COMPAT_LAYER`).
  - Расширенный FRST/AVZ-отчёт в `SystemReportService` и движок fixlist-скриптов **SU Script** (`SuScriptService`: `RegDelete`, `RegSet`, `FileQuarantine`, `FileDelete`, `ServiceDisable`, `TaskDisable`, `ProcessKill`, `HostsReset`, `ProxyReset`) с `Dry-Run` и Undo.
  - Проверка целостности 12 системных бинарников (`sethc`, `Utilman`, `osk`, `Narrator`, `Magnify`, `DisplaySwitch`, `AtBroker`, `cmd`, `explorer`, `Taskmgr`, `regedit`, `mmc`) по SHA-256 с `WinSxS`, `OriginalFilename` и IFEO.
  - Аудит браузеров (`BrowserAuditService`: расширения, `ExtensionInstallForcelist`, политики, прокси/PAC, ярлыки `--load-extension`, без чтения паролей/куки).
  - Антируткит Cross-View (`AntiRootkitService`), глубокий поиск по реестру/диску (`DeepSearchService`), мастер спасения данных (`DataRescueService`) и режим «Страж» (`RegistryGuardService` через `RegNotifyChangeKeyValue`).
  - Редактор BCD (`BcdService`) с переключателями SafeMode (Minimal/Network), `bootstatuspolicy=ignoreallfailures` и перезагрузкой в WinRE.
  - Помощник по шифровальщикам (`RansomwareHelperService`) с редактируемой базой расширений (`ransomware_extensions.json`), поиском записок о выкупе и просмотром теневых копий (`vssadmin`).
  - Просмотр офлайн-журналов `.evtx`, сетевая диагностика (DNS/ARP/Route + сброс Winsock/TCP-IP) и очистка Temp/Prefetch/кэшей с предпросмотром размера (`DiagnosticsCleanupService`).
  - Экспорт/импорт профилей лечения в JSON (`TreatmentProfileService`).
  - Мини-сборщик обезличенного ZIP-пакета для форума (`ForumPackageService`), скриншот окна в журнал и базовое read-only чтение `$MFT` (`MftReaderService`).
- **Интерфейс и UX:**
  - Индикаторы загрузки (`Идёт сканирование…`, `ProgressBar`) и подсказки пустого состояния в `QuickScanView` и `AutorunView`.
  - Очередь изменений (`ChangeQueueBar` — «К применению (N)» с предпросмотром «было → станет») в `QuickScanView`, `AutorunView` и `RestrictionsView`.
  - Группировка левого меню по смыслу (`ДИАГНОСТИКА`, `ЛЕЧЕНИЕ`, `СИСТЕМА`, `ДИСКИ И ЗАГРУЗКА`, `ИНСТРУМЕНТЫ`) с сохранением `Alt+1…9`.
  - Палитра команд (`Ctrl+K` / `CommandPaletteDialog`), контекстная справка (`F1`) и полный переключатель локализации `RU / EN` для всех 9 вкладок (`LangManager.ApplyLanguage`).
- **Документация:** `LICENSE` (MIT), `docs/AUDIT.md`, `THIRD-PARTY-NOTICES.md`, `CHANGELOG.md` и `CONTRIBUTING.md`.

### Исправлено
- Исправлено расхождение аргумента `--spawned-random` в `RandomLauncher.cs` и `Program.cs`, благодаря чему очистка временного клона `ScheduleSelfDelete()` срабатывает при штатном закрытии.
- Улучшен откат (Undo) в `BackupManager` и `SuScriptService`: рекурсивное сохранение и восстановление подразделов в `BackupAndDeleteSubKeyTree`, сохранение исходного `RegistryValueKind`, а также откат перемещения задачи в `TaskDisable`.
- Добавлен выбор пользовательского профиля `HKCU` (`NTUSER.DAT`) при работе с офлайн-установкой Windows (`OfflineTargetContext` + `TargetSelectDialog`).
- Переименован диагностический ZIP-пакет в пакет с частичной маскировкой и добавлен обязательный предпросмотр содержимого перед сохранением.
- Реализовано определение реального размера физического диска в `MbrBootService.EnumeratePhysicalDrives()` через `IOCTL_DISK_GET_LENGTH_INFO`.
- Автоматический однократный запуск сканирования при первом открытии `QuickScanView` и `AutorunView` за сессию (со сбросом при смене целевой ОС).
- Защита от дубликатов и фильтрация штатного шума в `AutorunService` (`DeduplicateEntries`, фильтрация стандартных записей COM/TimeProviders/PrintMonitors).
- Корректный разбор путей автозагрузки с запятыми и суффиксами (например, `userinit.exe,`, `shell32.dll,Control_RunDLL`) в `AutorunService.ExtractExecutablePath`.
- Полноценный сброс темы Windows на стандартную (`aero.theme`) в `SystemRecoveryService.RestoreDefaultTheme` (сброс `ThemeManager`, `Personalize`, `HighContrast`, обновление обоев через `SystemParametersInfo` и применение через `desk.cpl`).
- Восстановление заблокированных/занятых `sethc.exe` и `Utilman.exe` в `SystemRecoveryService.RestoreAccessibilityFilesFromWinSxS` (проверка SHA-256, завершение одноимённых процессов, переименование в `*.old_*` с отложенным удалением `MOVEFILE_DELAY_UNTIL_REBOOT`).
- Очистка временных клонов `RandomLauncher` в `%TEMP%` и ограничение `PatchVersionStrings` строго пределами PE-секции `.rsrc`.
- Удаление сторонних бинарников и `sethc.exe`/`Utilman.exe` из `EmbeddedResource`; удалён устаревший `ShiftUnlocker.App.csproj.user`.
- Убран ненужный блок сценариев из `QuickScanView` и опция запуска под случайным именем в `LauncherView`.
