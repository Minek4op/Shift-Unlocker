# Руководство для контрибьюторов (Contributing to Shift Unlocker)

Спасибо за интерес к развитию **Shift Unlocker**! Мы рады любым баг-репортам, улучшениям эвристик, новым модулям восстановления и исправлениям интерфейса.

---

## Структура решения

Решение `ShiftUnlocker.sln` (`net11.0-windows`, WinForms, `win-x64`) разделено на 4 проекта:

- **`ShiftUnlocker.App`** — графический интерфейс WinForms (`MainForm`, вкладки `UI/Views/*`, кастомные контролы `UI/Controls/*`, палитра команд, локализация `Localization.cs`).
- **`ShiftUnlocker.Core`** — вся бизнес-логика и сервисы восстановления (`Modules/*`, `Context/*`, `Safety/*`). **Не зависит от WinForms**.
- **`ShiftUnlocker.Native`** — P/Invoke-обёртки над WinAPI (`kernel32`, `advapi32`, `user32`, `ntdll`, `shell32`, `rstrtmgr` и др.).
- **`ShiftUnlocker.Tests`** — модульные тесты на `xUnit`.

---

## Требования и сборка

- **OS:** Windows 10 / 11 (x64)
- **SDK:** .NET 11 SDK (`net11.0-windows`)

### Основные команды

```powershell
# Сборка решения
dotnet build ShiftUnlocker.sln -c Release

# Запуск всех тестов
dotnet test ShiftUnlocker.sln -c Release

# Полная сборка: тесты + публикация single-file в dist\ShiftUnlocker.exe
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

---

## Правила разработки

1. **Изоляция от WinForms в `Core`:** вся новая логика диагностики и лечения должна жить в `ShiftUnlocker.Core`, без ссылок на `System.Windows.Forms`.
2. **Поддержка Online и Offline (WinPE) режимов:** все операции с реестром и путями к файлам Windows должны проходить через `ITargetContext` (`ctx.GetValue`, `ctx.SetValue`, `ctx.MapPath`, `ctx.WindowsDirectory`, `ctx.System32Directory`), а не напрямую через `Registry.LocalMachine` или захардкоженный путь `C:\Windows`.
3. **Обязательный откат (Undo):** любые модифицирующие операции с реестром или файлами должны выполняться через `BackupManager` (`BackupAndSetRegistryValue`, `BackupAndDeleteRegistryValue`, `BackupFile`, `RegisterUndo`), чтобы пользователь мог безопасно откатить изменения.
4. **Никаких вшитых закрытых бинарников:** запрещено добавлять сторонние закрытые `.exe`/`.dll` в `EmbeddedResource`. Внешние утилиты пользователь добавляет сам в папку `Tools\`.
5. **Локализация (RU / EN):** новые элементы интерфейса (кнопки, заголовки колонок, вкладки) добавляйте в `LangManager.UiPairs` в `ShiftUnlocker.App/UI/Localization.cs`.
6. **Тесты:** для новых эвристик, парсеров и сервисов добавляйте модульные тесты в `ShiftUnlocker.Tests`.

---

## Как отправить Pull Request

1. Сделайте форк репозитория и создайте ветку от `main` (например, `fix/autorun-parser` или `feat/new-diagnostic`).
2. Внесите изменения, соблюдая стиль кода проекта.
3. Убедитесь, что `dotnet test ShiftUnlocker.sln -c Release` проходит на 100% без ошибок.
4. Опишите в PR, что именно изменено, как воспроизвести/проверить и не затрагивает ли изменение WinPE/офлайн-режим.
