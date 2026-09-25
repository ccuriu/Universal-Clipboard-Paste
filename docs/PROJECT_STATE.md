# Состояние проекта

Текущая версия: 1.2.1.

## Рабочие компоненты
- `src/UniversalClipboardPaste.cs`
- `BUILD.cmd`
- `deploy.ps1`
- `installer.iss`
- `.github/workflows/release.yml`

Локальная папка `dist` является build-output и не хранится в Git.

## Публичный релиз
Release: `v1.2.1`

Assets:
- `UniversalClipboardPaste-Setup-1.2.1.exe`
- `UniversalClipboardPaste-Setup-1.2.1.sha256.txt`

SHA-256 опубликованного installer asset:
`84E606DC618ACBB77A7BE5F0A5A7E6116D963DDD7B559CBCCB6347B9501EA677`

Релиз собран на чистом GitHub Actions runner из исходников репозитория.

## Установка
`%LOCALAPPDATA%\UniversalClipboardPaste\UniversalClipboardPaste.exe`

В рабочем каталоге находятся только EXE и технический журнал.
Служебные данные стандартного деинсталлятора вынесены в:
`%LOCALAPPDATA%\UniversalClipboardPasteUninstall`

Автозапуск:
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run -> UniversalClipboardPaste`

## Гарантии текущей архитектуры
- только текст превращается во временный `.txt`;
- файлы и изображения не пересохраняются;
- нет зависимости от конкретного приложения;
- нет UI Automation и координат в runtime-пути;
- журнал не хранит содержимое Clipboard или названия окон;
- временный текст хранится только в `%TEMP%` и автоматически удаляется;
- каталог установки не содержит пользовательское содержимое;
- старые реализации в дереве проекта отсутствуют.
