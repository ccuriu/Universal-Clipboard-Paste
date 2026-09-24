# Указание рабочему чату проекта

Работай только от версии 1.1.0 и её архитектуры Shell Clipboard. Не возвращай обычный Ctrl+V из 1.0.0 и не возвращай меню ChatGPT/UI Automation из 0.3.x без отдельного решения управляющего чата.

Источник истины:
- src\ChatGPTClipboardFilePaste.cs
- deploy.ps1
- %LOCALAPPDATA%\ChatGPTClipboardFilePaste\hotkey.log
- main GitHub-репозитория.

При любой доработке сохраняй основной путь:
Clipboard text -> UTF-8 payload -> SHCreateDataObject -> OleSetClipboard -> OleFlushClipboard -> Ctrl+V -> восстановление исходного Clipboard -> автоочистка payload.

Проверяй:
- файл реально появляется в активном поле;
- содержимое совпадает байт-в-текст с исходником;
- кириллица сохранена;
- Clipboard восстановлен;
- Ctrl/Shift не зажаты;
- count=4, fallback=False;
- нет HOTKEY_SKIPPED_BUSY при нормальном темпе;
- задержка не регрессирует к секундам.

Следующий этап: матрица приложений и Smart Clipboard для изображений/готовых файлов. Любую новую сложность добавлять только после воспроизводимого теста.