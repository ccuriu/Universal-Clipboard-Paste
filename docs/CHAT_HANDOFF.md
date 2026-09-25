# Указание рабочему чату проекта

Работай только от Universal Clipboard Paste 1.2.0.
Источник истины: src\UniversalClipboardPaste.cs, BUILD.cmd, deploy.ps1, installer.iss и main GitHub.

Не возвращай:
- обычный Ctrl+V для текстового маршрута из 1.0.0;
- меню ChatGPT/UI Automation/мышь/файловый диалог из 0.3.x;
- преобразование изображений в PNG без доказанной необходимости.

Архитектурное правило:
files/images/other -> passthrough исходного Clipboard;
text -> UTF-8 payload -> Shell IDataObject -> OLE Clipboard -> Ctrl+V -> восстановление текста.

При изменениях обязательно проверяй:
count=4, fallback=False, отсутствие зажатых Ctrl/Shift;
совпадение текстового payload;
неизменность исходного Clipboard для passthrough;
чистую установку, автозапуск и удаление.

Следующий этап: матрица совместимости приложений, затем UX/релизная упаковка.