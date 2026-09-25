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