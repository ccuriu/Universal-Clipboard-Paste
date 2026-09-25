# Архитектура 1.2

## Главный принцип
Universal Clipboard Paste не преобразует Clipboard без необходимости.

Ctrl+Shift+V сначала определяет тип содержимого:
1. FileDrop -> passthrough Ctrl+V.
2. Image/Bitmap -> passthrough Ctrl+V.
3. Text -> text-as-file.
4. Other -> passthrough Ctrl+V.

## Text-as-file
Текст сохраняется в UTF-8 .txt.
SHCreateDataObject создаёт настоящий Windows Shell IDataObject.
OleSetClipboard + OleFlushClipboard публикуют файл в системный Clipboard.
SendInput отправляет Ctrl+V в активное поле.
После захвата файла целевым приложением исходный Unicode-текст возвращается в Clipboard.
Payload удаляется автоматически.

## Надёжность
x64 INPUT содержит MOUSEINPUT и KEYBDINPUT.
Mutex запрещает второй экземпляр.
Busy не допускает наложение операций.
Fallback keybd_event применяется только если SendInput отправил не четыре события.

## Намеренно отсутствует
Нет логики ChatGPT, UI Automation, DOM, кликов, координат, меню «+» и диалогов выбора файла.
Граница универсальности определяется возможностями самого целевого приложения.