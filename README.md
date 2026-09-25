# Universal Clipboard Paste

Universal Clipboard Paste — фоновая Windows-утилита для Ctrl+Shift+V.

## Версия 1.2.0
Smart Clipboard вмешивается только там, где это полезно:
- текст -> временный UTF-8 .txt -> вставка как настоящий Shell-файл;
- существующий файл/файлы -> обычная вставка исходного Clipboard без пересоздания;
- изображение -> обычная вставка исходного Clipboard без конвертации;
- прочие форматы -> passthrough без изменения Clipboard.

Для текстового маршрута используется SHCreateDataObject + OleSetClipboard + OleFlushClipboard.
Нет меню ChatGPT, UI Automation, мыши, координат, Проводника и файлового диалога.

## Проверено
ChatGPT Web: text-as-file работает, кириллица и содержимое совпадают.
File passthrough: файл появился напрямую, 10 мс.
Image passthrough: изображение осталось исходным в Clipboard и появилось в поле, 6 мс.
SendInput: 4/4, fallback=False.

## Установка
Релизный установщик: dist\UniversalClipboardPaste-Setup-1.2.0.exe
Установка per-user в %LOCALAPPDATA%\UniversalClipboardPaste, без прав администратора.
Автозапуск: HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
Подробности: docs\INSTALL.md.