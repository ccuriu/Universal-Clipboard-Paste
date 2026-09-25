# Changelog

## 1.2.0 — 2026-09-25
- Добавлен Smart Clipboard.
- Только текст преобразуется во временный UTF-8 .txt.
- Существующие файлы передаются как есть.
- Изображения передаются как есть, без PNG-посредника.
- Прочие типы Clipboard проходят passthrough.
- Рабочий EXE переименован в UniversalClipboardPaste.exe.
- Каталог установки: %LOCALAPPDATA%\UniversalClipboardPaste.
- Mutex переименован в Local\UniversalClipboardPaste.
- Добавлены метаданные версии EXE 1.2.0.0.
- Добавлен Inno Setup installer/uninstaller с миграцией старой установки.
- Установщик проверен: install -> uninstall -> reinstall.
- Uninstall удаляет процесс, автозапуск и каталог полностью.

## 1.1.0 — 2026-09-25
- Текст вставляется как Shell-файл через SHCreateDataObject/OLE Clipboard.
- Убраны UI Automation, меню и файловый диалог.

## 1.0.0 — переходная версия
- Ошибочно сводила Ctrl+Shift+V к обычному Ctrl+V.