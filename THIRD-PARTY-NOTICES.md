# Third-Party Notices

**Shift Unlocker** написан полностью с нуля как оригинальный проект на C# / .NET (`ShiftUnlocker.App`, `ShiftUnlocker.Core`, `ShiftUnlocker.Native`, `ShiftUnlocker.Tests`).

## Внешние бинарники
- В сборку и репозиторий **не вшиваются** сторонние закрытые утилиты (`NHelperV4.exe`, `SU.exe`, `RecoveryKit.exe`) и проприетарные системные бинарники Microsoft (`sethc.exe`, `Utilman.exe`).
- Восстановление `sethc.exe` и `Utilman.exe` выполняется из локальных резервных копий (`.bak`) или эталонного хранилища `WinSxS` целевой Windows.
- Пользователь при необходимости может самостоятельно поместить свои портативные `.exe` утилиты в папку `Tools\` рядом с программой — лаунчер (`LauncherService`) автоматически обнаружит их, не включая в дистрибутив.

## Используемые библиотеки и SDK
- **.NET 11 Windows Desktop / WinForms** — MIT License (Microsoft)
- **xUnit** (`ShiftUnlocker.Tests`) — Apache-2.0 License
