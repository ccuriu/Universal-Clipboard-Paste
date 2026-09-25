# Архитектура

## Принцип
Universal Clipboard Paste минимально вмешивается в Clipboard.

Ctrl+Shift+V определяет тип содержимого:
1. FileDrop -> обычный Ctrl+V без изменения Clipboard.
2. Image/Bitmap -> обычный Ctrl+V без изменения Clipboard.
3. Text -> text-as-file.
4. Other -> обычный Ctrl+V без изменения Clipboard.

## Text-as-file
Текст сохраняется во временный UTF-8 .txt в системном %TEMP%, вне каталога установки.
Windows Shell IDataObject создаётся через SHCreateDataObject.
OleSetClipboard и OleFlushClipboard публикуют файл в системный Clipboard.
SendInput отправляет Ctrl+V в активное поле.
После передачи файла исходный Unicode-текст возвращается в Clipboard.
Временный payload удаляется автоматически.

## Надёжность
- корректная x64-структура INPUT;
- один экземпляр через Mutex;
- защита от наложения операций через Busy;
- fallback keybd_event при неполном SendInput.

## Приватность
Журнал содержит только технические события, тип маршрута и время выполнения.
Содержимое Clipboard, имена файлов пользователя и названия активных окон не записываются.