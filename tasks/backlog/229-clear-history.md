---
id: 229
status: done
assignee:
claimed:
created: 2026-11-07
---

# Полная очистка истории

## Описание
Решение владельца 2026-11-07: «давай добавим полную очистку истории». В Settings — действие, которое удаляет всю историю: все сессии со всеми сообщениями, прогонами, генерациями, вложениями, описаниями, сводками, файлами картинок/видео/аудио и обложек, и индекс семантического поиска. Ключи провайдеров, настройки (оформление, модели по умолчанию, поиск, лимиты расходов) и журнал расходов **сохраняются** (журнал расходов — независимый учёт денег, ADR 0002; допущение, владелец может поменять — тогда отдельная кнопка очистки журнала).

В базе всё связано с `sessions` через `ON DELETE CASCADE` (runs, generations, chat_messages, chat_attachments, image_descriptions, conversation_summaries, text_embeddings — миграции 0001, 0004, 0005, 0007, 0013); `expenses` не связан. Файлы — в `<данные>/images/<session>/`.

Зависит от: —
Среда: Linux (Core, Data, Presentation, Avalonia headless, сборка MAUI Android); проверка — 140, 046.

## Критерии готовности
- `HistoryService.ClearAllAsync(CancellationToken)` (Core): (1) для каждой сессии `generationCoordinator.StopSessionAsync` (как `SessionService.DeleteAsync`) — активные генерации и опросы видео останавливаются; (2) `ISessionRepository.DeleteAllAsync` — одна команда `DELETE FROM sessions` в транзакции (каскад удаляет остальное); (3) `IImageFileStore.DeleteAllMediaAsync` — удаляет папку `images/` целиком (с повторной попыткой на `IOException`, ошибки — Warning, не прерывают); (4) `text_embeddings` пусты через каскад — проверить тестом; индекс семантического поиска после очистки — `Ready` с нулём; (5) уведомление `SessionChangeNotifier` (новый метод «всё изменилось» или уведомление по каждому удалённому id — выбрать, записать); (6) Information в журнал: число удалённых сессий и файлов.
- Если в это время пользователь в чате — после очистки навигация на список сессий (Presentation).
- Settings: кнопка «Delete all history» (стиль опасного действия), `ConfirmAsync(..., destructive: true)` с текстом «Delete all sessions, images, videos, audio and the search index? Provider keys, settings and spending history are kept. This cannot be undone.» (ресурс); после — алерт «History deleted: N sessions.».
- Обе оболочки (MAUI `SettingsPage.xaml`, Avalonia `SettingsView.axaml`), доступность.
- Тесты: Data — после `DeleteAllAsync` пусты все таблицы истории, `expenses` и `providers`/настройки не тронуты; файловое хранилище — папка `images/` удалена, логи и `search.json` на месте; Core — `ClearAllAsync` останавливает генерации всех сессий до удаления (порядок вызовов) и уведомляет; Presentation — отказ в подтверждении ничего не удаляет; согласие вызывает службу и показывает число; открытый чат уходит на список.
- Сборка MAUI Android; пункты 140 и 046: после очистки список сессий пуст, галерея пуста, расходы и ключи на месте, новая сессия работает.

## Анализ
`ImageGen/ImageGen.Core/Services/SessionService.cs` (`DeleteAsync`: остановка, удаление, файлы, уведомление), `ISessionRepository` и `SessionRepository`, `IImageFileStore`/`FileImageStore` (`DeleteSessionFilesAsync`), `SessionChangeNotifier`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, навигация — `INavigationService`. Регистрация `HistoryService` — `CoreServiceCollectionExtensions`.

## План
1. Падающие тесты Data и файлового хранилища.
2. `DeleteAllAsync`, `DeleteAllMediaAsync`.
3. Падающие тесты `HistoryService`, реализация, регистрация.
4. Settings: команда, ресурсы, разметка обеих оболочек; переход из открытого чата.
5. Сборки; пункты 140 и 046.

Файлы: `ImageGen/ImageGen.Core/Services/HistoryService.cs` (новый), `ImageGen/ImageGen.Core/Storage/ISessionRepository.cs`, `ImageGen/ImageGen.Core/Storage/IImageFileStore.cs`, `ImageGen/ImageGen.Core/Services/SessionChangeNotifier.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data/Repositories/SessionRepository.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, тесты Core, Data, Presentation, Avalonia, тестовые реализации `IImageFileStore`/`ISessionRepository` (`rg -n ": IImageFileStore|: ISessionRepository" ImageGen`), `tasks/backlog/140-android-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

### 2026-11-07, codex-gpt6
- Добавлен `HistoryService.ClearAllAsync`: останавливает генерации всех сессий, удаляет сессии одним транзакционным `DELETE FROM sessions`, удаляет папку `images/` (до 3 попыток при IOException), публикует `NotifyAll`, обновляет статус включённого семантического индекса, пишет число сессий и файлов.
- Добавлены `ISessionRepository.DeleteAllAsync`, `IImageFileStore.DeleteAllMediaAsync`, event `AllSessionsChanged`; список сессий перечитывается. Settings получила destructive confirmation, оба UI action и переход из чата на список после очистки.
- Добавлены тесты Core (порядок остановки/удаления/события и refresh индекса), Data (каскад, embeddings, расходы остаются; файлы удаляются, logs/search.json сохраняются), Presentation (отмена/подтверждение).
- Проверки: Core 811/811, Data 146/146, Presentation 528/528, Avalonia 180 passed / 2 skipped (D-Bus и Windows DPAPI); MAUI Android build 0 warnings/errors. Форматирование Core, Core.Tests, Data, Data.Tests, Presentation, Presentation.Tests, Avalonia, Avalonia.Tests и MAUI прошло; `scripts/check.sh` — «все проверки пройдены».
- Пункты 046 и 140 не выполнялись у физического экрана; добавлен сценарий ручной проверки ниже.

## Комментарии

### 2026-11-07, claude-audit
Создано по решению владельца («давай добавим полную очистку истории»). Допущение: журнал расходов, ключи и настройки сохраняются. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
