# Changelog

## [Unreleased] — ЭТАП 0 (Аудит и стабилизация)
### Исправлено
- Автоматический однократный запуск сканирования при первом открытии `QuickScanView` и `AutorunView` за сессию (со сбросом при смене целевой ОС).
- Очистка временных клонов `RandomLauncher` в `%TEMP%` (`CleanupOrphanedClones` при старте и `ScheduleSelfDelete` при выходе).
- Ограничение `RandomLauncher.PatchVersionStrings` строго пределами PE-секции `.rsrc` (безопасно для `EnableCompressionInSingleFile=true`).
- Удаление сторонних бинарников (`NHelperV4.exe`, `SU.exe`, `RecoveryKit.exe`) и системных файлов (`sethc.exe`, `Utilman.exe`) из `EmbeddedResource`; восстановление `sethc`/`Utilman` переведено на `.bak` и `WinSxS`.
- Удалён устаревший `ShiftUnlocker.App.csproj.user`.

### Добавлено
- Документация аудита `docs/AUDIT.md`, `THIRD-PARTY-NOTICES.md` и `CHANGELOG.md`.
