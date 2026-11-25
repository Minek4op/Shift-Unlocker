@echo off
:: Пример startnet.cmd для сред восстановления Windows (WinPE / WinRE)
:: Размещается в: X:\Windows\System32\startnet.cmd

wpeinit

echo Инициализация среды WinPE...

:: В WinPE RAM-диск X: ограничен в размерах.
:: Перенаправляем временную папку распаковки .NET single-file на быстрый временный каталог
set DOTNET_BUNDLE_EXTRACT_BASE_DIR=X:\Windows\Temp\.net

:: Поиск флешки или диска с Shift Unlocker
for %%d in (C D E F G H I J K L M N O P Q R S T U V W Y Z) do (
    if exist "%%d:\ShiftUnlocker\ShiftUnlocker.exe" (
        echo Запуск Shift Unlocker с диска %%d:...
        "%%d:\ShiftUnlocker\ShiftUnlocker.exe" --no-random
        goto :done
    )
    if exist "%%d:\ShiftUnlocker.exe" (
        echo Запуск Shift Unlocker с корня %%d:...
        "%%d:\ShiftUnlocker.exe" --no-random
        goto :done
    )
)

echo ShiftUnlocker.exe не найден на внешних дисках. Запуск командной строки...
cmd.exe

:done
