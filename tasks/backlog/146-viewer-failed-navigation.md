---
id: 146
status: in-progress
assignee: codex-gpt6
claimed: 2026-10-30T16:37:50Z
created: 2026-10-30
---

# Сохранять позицию viewer при неудачной навигации

## Описание
Оба viewer откатывают _index в index-1 при ошибке загрузки. Открыть третью картинку, перейти назад на отсутствующую вторую: третья остаётся на экране, внутренний индекс становится первым, Next снова пробует вторую. В Avalonia ошибка после первой успешной загрузки также не показывает сообщение; в этом follow-up явно добавляется локализованное уведомление для соседней картинки. Исходная131 требовала сообщение только при ошибке начального файла; отсутствие neighbor alert само по себе не является её недоделкой.

Зависит от: — (код131/137 уже в main; исходная131 удерживается аудитом до follow-ups).
Среда: Linux или Windows; сборка MAUI Android на Linux, Windows-ветку не менять.

## Критерии готовности
- После failed next/previous сохраняются индекс, картинка, подпись и доступное имя последнего успешно показанного изображения.
- Локализованная ошибка видна в обоих shell; ошибка первой картинки закрывает viewer после сообщения, как раньше.
- Устаревший success/error и завершение загрузки после Close не меняют текущий viewer.
- Нет wrap; работают быстрые чередующиеся next/previous; bitmap освобождается как раньше.

## Анализ
Evidence: AvaloniaImageViewerService.LoadAsync:173–188; MauiImageViewerService.LoadAsync:177–189. Текущий индекс запроса нельзя использовать как индекс показанной картинки. Изначальный viewer131 не полностью удовлетворяет обработке ошибки; она остаётся неархивированной. Пересечение с149 по MauiImageViewerService: сначала146, затем149; Windows-only148 последней.

## План
1. Прочитать оба LoadAsync/Navigate/Close, ImageViewerWindowTests и Presentation csproj (общая часть не должна зависеть от UI framework).
2. Вынести минимальное общее состояние навигации в Presentation/Services/ImageViewerNavigationState.cs: количество, индекс запрошенной и индекс последней успешно показанной картинки, номер запроса/epoch. Операции начать допустимый переход, подтвердить успех актуального запроса, откатить актуальную ошибку к последнему успеху, инвалидировать при закрытии. displayedIndex изначально отсутствует; быстрые переходы считаются от последнего requested index, актуальная ошибка откатывает его к последнему displayed. Identity запроса = index+epoch. Это не loader и не event bus; пути/Bitmap/диалоги остаются в shell.
3. Сначала unit tests состояния: старт index2 → previous index1 → failure → current2; next failure аналогично; stale success/failure игнорируется; latest success; границы0/count-1; закрытие инвалидирует запрос. Не закреплять ошибочную index-1 семантику.
4. Подключить состояние в оба shell вместо произвольного отката _index. Только актуальный успешный запрос меняет Image/position/automation. Неудача сохраняет прежний source и показывает AppResources.ImageViewerUnreadable. Пустой viewer закрывается после сообщения. Поздний ответ не показывает лишний alert и не закрывает новый viewer; после await Alert ещё раз проверить epoch/closed перед Close, чтобы старый диалог не закрыл уже загруженное новое изображение.
5. Avalonia integration test с тремя реальными временными картинками: средняя отсутствует; открыть третью, Left, дождаться ошибки, проверить позицию3 и прежний source, Right не делает wrap, Left повторяет ошибку. Сходный сценарий forward и stale loading. Тестовые файлы только в tmp/репозитория, очищать после disposal.
6. Добавить ручные regression пункты в016/046/140: failed backward/forward и быстрые переходы. MAUI runtime не выдавать за проверенный на Linux.

Файлы: `ImageGen/ImageGen.Presentation/Services/ImageViewerNavigationState.cs` (новый), соответствующий новый тест Presentation.Tests, `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs`, `ImageGen/ImageGen.Avalonia.Tests/Services/ImageViewerWindowTests.cs`, `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs`, задачи ручных проверок016/046/140.

Проверка: Presentation.Tests; затем Avalonia.Tests; `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`; форматировать MAUI через доступную Android цель по docs/knowledge/dev-environment.md.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
bash scripts/check.sh
```

### Порядок исполнения и проверки
План рассчитан на Luna: прочитать перечисленные файлы, выполнить шаги по порядку; сначала падающий регрессионный тест, затем минимальный фикс (ub-mode из AGENTS.md). Не реализовывать соседние задачи. Сигнатуры сверять с текущим main; при rebase сохранять чужие изменения. Не использовать платные API и не записывать секреты. Команды выполнять последовательно: общие build outputs не допускают параллельные dotnet build/test.
После изменения каждого проекта: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`; после всех изменений — перечисленные тесты и `bash scripts/check.sh`. В отчёте указать команды, результаты, ручные ограничения и отличие от плана. Код писать только после одобрения плана по docs/tasks.md.

## Отчёт

## Комментарии

### 2026-10-30, codex-audit
Создано по поручению владельца: подробный план для Luna. В этой задаче код ещё не исправлен; статус planned не означает одобрение реализации.

### 2026-10-30, владелец (в чате, записал codex)
План одобрен явным поручением «Заапрувь таски на фиксы». Можно реализовывать по описанному плану после готовности зависимостей в main и проверки среды/занятости по docs/tasks.md.
