# Состояние проекта

Текущая версия: 1.2.1.

## Рабочие компоненты
- src\UniversalClipboardPaste.cs
- BUILD.cmd
- deploy.ps1
- installer.iss
- dist\UniversalClipboardPaste.exe
- dist\UniversalClipboardPaste-Setup-1.2.1.exe

## Установка
%LOCALAPPDATA%\UniversalClipboardPaste\UniversalClipboardPaste.exe

В рабочем каталоге находятся только EXE и технический журнал.
Служебные данные стандартного деинсталлятора вынесены в:
%LOCALAPPDATA%\UniversalClipboardPasteUninstall

Автозапуск:
HKCU\Software\Microsoft\Windows\CurrentVersion\Run -> UniversalClipboardPaste

## Гарантии текущей архитектуры
- только текст превращается во временный .txt;
- файлы и изображения не пересохраняются;
- нет зависимости от конкретного приложения;
- нет UI Automation и координат;
- журнал не хранит содержимое Clipboard или названия окон;
- временный текст хранится только в %TEMP% и автоматически удаляется;
- каталог установки не содержит пользовательское содержимое;
- старые реализации в дереве проекта отсутствуют.

## Контрольные SHA-256
UniversalClipboardPaste.exe:
C516F12D4B3F63E5289107DEADAC1B55D66404CEFED95A58211A1F2F7476D92E

UniversalClipboardPaste-Setup-1.2.1.exe:
D1054168CBD69686B07A6BC70E3984321AB111833EA16AD49714C191A42E549F