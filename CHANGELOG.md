# Changelog

## [Unreleased] — ЭТАП 2 (P2 и P3 функции)
### Добавлено
- **P2**: Редактор BCD (`BcdService`) с переключателями SafeMode (Minimal/Network), `bootstatuspolicy=ignoreallfailures` и перезагрузкой в WinRE.
- **P2**: Помощник по шифровальщикам (`RansomwareHelperService`) с редактируемой базой расширений (`ransomware_extensions.json`), поиском записок о выкупе и просмотром теневых копий (`vssadmin`).
- **P2**: Просмотр офлайн-журналов `.evtx`, сетевая диагностика (DNS/ARP/Route + сброс Winsock/TCP-IP) и очистка Temp/Prefetch/кэшей с предпросмотром размера (`DiagnosticsCleanupService`).
- **P2**: Экспорт/импорт профилей лечения в JSON (`TreatmentProfileService`: «Локер с баннером», «Ярлыки на флешке», «Реклама в браузере»).
- **P3**: Мини-сборщик обезличенного ZIP-пакета для форума (`ForumPackageService`), скриншот окна в журнал и базовое read-only чтение `$MFT` (`MftReaderService`).

## [Unreleased] — ЭТАП 2 (P1 функции)
### Добавлено
- Расширенное сканирование автозагрузки в `AutorunService`: COM-hijack (`HKCU\...\CLSID`), AppCompat shims, Time Providers, Print Monitors, Winsock LSP, PowerShell-профили, переменные среды (`COR_PROFILER`, `__COMPAT_LAYER`).
- Расширенный FRST/AVZ-отчёт в `SystemReportService` и движок fixlist-скриптов **SU Script** (`SuScriptService`: `RegDelete`, `RegSet`, `FileQuarantine`, `FileDelete`, `ServiceDisable`, `TaskDisable`, `ProcessKill`, `HostsReset`, `ProxyReset`) с `Dry-Run` и Undo.
- Проверка целостности 12 системных бинарников (`sethc`, `Utilman`, `osk`, `Narrator`, `Magnify`, `DisplaySwitch`, `AtBroker`, `cmd`, `explorer`, `Taskmgr`, `regedit`, `mmc`) по SHA-256 с `WinSxS`, `OriginalFilename` и IFEO.
- Аудит браузеров (`BrowserAuditService`: расширения, `ExtensionInstallForcelist`, политики, прокси/PAC, ярлыки `--load-extension`, без чтения паролей/куки).
- Антируткит Cross-View (`AntiRootkitService`), глубокий поиск по реестру/диску (`DeepSearchService`), мастер спасения данных (`DataRescueService`) и режим «Страж» (`RegistryGuardService` через `RegNotifyChangeKeyValue`).

## [Unreleased] — ЭТАП 1 (Интерфейс и UX)
### Добавлено
- Главная панель сценариев «Что случилось?» в `QuickScanView` с переходом к нужному модулю и фильтру.
- Индикаторы загрузки (`Идёт сканирование…`, `ProgressBar`) и подсказки пустого состояния в `QuickScanView` и `AutorunView`.
- Очередь изменений (`ChangeQueueBar` — «К применению (N)» с предпросмотром «было → станет») в `QuickScanView`, `AutorunView` и `RestrictionsView`.
- Группировка левого меню по смыслу (`ДИАГНОСТИКА`, `ЛЕЧЕНИЕ`, `СИСТЕМА`, `ДИСКИ И ЗАГРУЗКА`, `ИНСТРУМЕНТЫ`) с сохранением `Alt+1…9`.
- Палитра команд (`Ctrl+K` / `CommandPaletteDialog`), контекстная справка (`F1`) и переключатель локализации `RU / EN` (`LangManager`).

## [Unreleased] — ЭТАП 0 (Аудит и стабилизация)
### Исправлено
- Автоматический однократный запуск сканирования при первом открытии `QuickScanView` и `AutorunView` за сессию (со сбросом при смене целевой ОС).
- Очистка временных клонов `RandomLauncher` в `%TEMP%` (`CleanupOrphanedClones` при старте и `ScheduleSelfDelete` при выходе).
- Ограничение `RandomLauncher.PatchVersionStrings` строго пределами PE-секции `.rsrc` (безопасно для `EnableCompressionInSingleFile=true`).
- Удаление сторонних бинарников (`NHelperV4.exe`, `SU.exe`, `RecoveryKit.exe`) и системных файлов (`sethc.exe`, `Utilman.exe`) из `EmbeddedResource`; восстановление `sethc`/`Utilman` переведено на `.bak` и `WinSxS`.
- Удалён устаревший `ShiftUnlocker.App.csproj.user`.

### Добавлено
- Документация аудита `docs/AUDIT.md`, `THIRD-PARTY-NOTICES.md` и `CHANGELOG.md`.
