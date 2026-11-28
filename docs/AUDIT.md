# Аудит кода (ЭТАП 0)

## 1. Автозагрузка данных во вкладках
- **Проблема**: `MainForm.SwitchView(int index)` обновлял данные только для `RestrictionsView`, `ProcessView` и `ToolsLogView`. При первом открытии `QuickScanView` и `AutorunView` таблицы оставались пустыми до ручного клика.
- **Исправление**: В `QuickScanView` добавлены `_hasScannedOnce` и `EnsureScannedOnceAsync()`, в `AutorunView` — `_hasLoadedOnce` и `EnsureLoadedOnceAsync()`. При первом открытии вкладки сканирование запускается автоматически; повторные переключения не сбрасывают найденные результаты. При смене целевой ОС (`Alt+T`) флаги сбрасываются. В `RegEditorView` корневые узлы (`HKLM/HKCU/HKCR/HKU/HKCC`) инициализируются в конструкторе (`InitRootNodes()`).

## 2. Очистка временных клонов (`RandomLauncher`)
- **Проблема**: Временные копии со случайным именем в `%TEMP%` накапливались при каждом запуске, а `ScheduleSelfDelete` не вызывался.
- **Исправление**: Добавлен `RandomLauncher.CleanupOrphanedClones()` (удаляет осиротевшие клоны и `su_*.exe` старше 10 минут, не трогая активные заблокированные процессы), а также вызов `ScheduleSelfDelete` при штатном закрытии клона (`--spawned`).

## 3. Безопасность `PatchVersionStrings` при `EnableCompressionInSingleFile=true`
- **Проблема**: Предыдущая реализация `PatchVersionStrings` сканировала байтовый массив всего `.exe` целиком. При включённом `EnableCompressionInSingleFile=true` в конце PE-файла находится сжатый single-file bundle overlay со всеми сборками. Сканирование всего файла создавало риск повреждения оверлея.
- **Исправление**: Добавлен разбор PE-заголовка и таблицы секций (`TryGetResourceSectionBounds`), ограничивающий замену UTF-16LE строк **строго диапазоном секции `.rsrc`** (`[PointerToRawData, PointerToRawData + SizeOfRawData)`). Код и single-file бандл не затрагиваются.
- **TODO**: Для полной независимости от длины строк в будущем перевести обновление `VS_VERSIONINFO` на `BeginUpdateResourceW` / `UpdateResourceW(RT_VERSION)` / `EndUpdateResourceW` с пересборкой структуры `StringFileInfo` (без риска сдвига single-file overlay, либо выполнять до склейки).

## 4. Исключение сторонних и системных бинарников из `EmbeddedResource`
- **Проблема**: В `ShiftUnlocker.App.csproj` были вшиты сторонние закрытые бинарники (`NHelperV4.exe` ~204 МБ, `SU.exe`, `RecoveryKit.exe`) и системные файлы Windows (`sethc.exe`, `Utilman.exe`) без лицензии на перераспространение.
- **Исправление**: Все чужие и проприетарные бинарники удалены из `<EmbeddedResource>`. Модуль «Утилиты» работает как чистый лаунчер системных средств и пользовательских `.exe` из папки `Tools\`. Восстановление `sethc.exe` / `Utilman.exe` переведено на `.bak` и `WinSxS` (`RestoreAccessibilityFilesFromWinSxS`). Добавлен `THIRD-PARTY-NOTICES.md`.

## 5. Очистка проекта
- Удалён `ShiftUnlocker.App.csproj.user`, ссылавшийся на несуществующий `Form1.cs`.
