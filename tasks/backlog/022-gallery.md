---
id: 022
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Галерея всех генераций

## Описание
Раздел со всеми генерациями всех сессий: лента по прогонам, фильтры по модели и дате, переход к исходной сессии (F7.3, F7.4). Сейчас история доступна только в отдельном чате; глобальной галереи нет.

Зависит от: —
Среда: Linux или Windows; обе UI-оболочки.

Код скелета уже есть, 016 не блокирует планирование. Статус `todo`: сначала подготовить и одобрить план; данный обзор не разрешает писать код.

## Критерии готовности
- Данные читаются страницами из БД; миниатюры загружаются только для видимых элементов, без чтения всей истории в память.
- При одинаковом времени у записей порядок стабилен: на границах страниц нет пропусков/дублей.
- Фильтры применяются до пагинации; смена фильтра сбрасывает курсор и отменяет либо игнорирует старый ответ.
- Карточка открывает исходную сессию; удалённая сессия или отсутствующий файл дают понятное состояние.
- Одинаковое поведение доступно в MAUI и Avalonia; существующий поиск по сессиям сохраняется.

## Анализ
`IRunRepository` имеет только session-scoped `ListRunsBySessionAsync`/`ListGenerationsBySessionAsync`, поэтому загрузка всех сессий с последующим объединением нарушит критерий памяти. Нужен отдельный запрос глобальных результатов с фильтрами и стабильным cursor/keyset-порядком; SQL остаётся в Data. `SessionSearchService` ищет сессии, его нельзя выдавать за поиск/пагинацию галереи.

Проверить область F7.4: задача не должна молча обещать избранное/оценки/теги, которых нет в доменной модели. В будущем плане явно назвать включённые фильтры (модель и дата уже входят в описание), а дополнительные функции вынести отдельными задачами.

UI надо делать в двух оболочках через общую PageModel; сквозная задача слишком велика для Haiku/Luna. Рекомендуемое разбиение после согласования: read-model/репозиторий с пагинацией → общая PageModel/маршрут → две оболочки и виртуализация.


Уточнение 2026-10-31 (claude-audit, по коду `b4d00d9`): `IRunRepository` умеет только выборки по сессии; индекса по `generations.created_at` нет (`ix_generations_run`, `_session`, `_status`). Миниатюр в коде нет: Avalonia `FilePathToBitmapConverter` и MAUI `FilePathToImageSourceConverter` грузят файл целиком.

## План
Сначала падающий тест, затем код (ub-mode). Два этапа, отдельные PR.

**Этап A. Данные и модель страницы.**
1. В галерею входят только успешные генерации с файлом (`Succeeded`, `image_path` не NULL). Ошибки, очередь и отфильтрованные остаются в чатах — допущение.
2. Core: `Storage/IGalleryRepository.ListAsync(GalleryQuery, CancellationToken)` → `GalleryPage(Items, NextCursor)`. `GalleryQuery`: `PageSize` (1..100, иначе `ArgumentOutOfRangeException`), `Cursor?`, `ModelRef?`, `From?` (включительно), `To?` (исключительно, локальные даты переводятся в UTC в PageModel). `GalleryItem`: GenerationId, RunId, SessionId, SessionTitle, ModelRef, Prompt, ImagePath, MediaType, CostUsd, CompletedAt. Курсор — пара (`completed_at`, `id`), порядок убывающий.
3. Data: миграция `NNNN_gallery_index.sql` — индекс `generations(status, completed_at DESC, id DESC)`; запрос keyset `WHERE (completed_at, id) < (@at, @id)` с фильтрами до `LIMIT`; join с `sessions` для названия.
4. Presentation: `GalleryPageModel` (маршрут `gallery`): `Items`, `LoadMoreCommand` (один запрос за раз), `HasMore`, `IsBusy`, фильтры модели (из моделей, встречающихся в галерее — `IGalleryRepository.ListModelsAsync`) и периода (всё время / 7 дней / 30 дней). Смена фильтра сбрасывает список и курсор; ответ старого запроса отбрасывается по номеру эпохи. `OpenCommand` открывает сессию (`Routes.ChatFor`); удалённая сессия/нет файла — сообщение и строка помечается.
5. Тесты Data: одинаковые `completed_at` на границе страниц — без пропусков и дублей; фильтр модели и дат; удаление между страницами; пустой результат; неверный размер страницы. Presentation: сброс при смене фильтра, устаревший ответ, удалённая сессия.

**Этап B. Экраны.**
1. Avalonia `Views/GalleryView.axaml`: `ItemsRepeater`/`ListBox` с виртуализацией, `UniformGridLayout`, миниатюра через новый `ThumbnailConverter` (`Bitmap.DecodeToWidth(…, 256)`), догрузка при прокрутке к концу.
2. MAUI `Pages/GalleryPage.xaml`: `CollectionView` с `GridItemsLayout`, `RemainingItemsThreshold` → `LoadMoreCommand`; миниатюры через `Image` с `HeightRequest` (MAUI не уменьшает декодирование — память ограничивает виртуализация, проверить на Android в 140).
3. Вход: кнопка «Gallery» внизу списка сессий в обеих оболочках (рядом с Settings).
4. Тесты Avalonia headless: открытие, догрузка, переход в сессию. Ручные пункты — 046 и 140.

Проверки: Data.Tests, Presentation.Tests, Avalonia.Tests, сборка MAUI Android, `dotnet format`, `scripts/check.sh`. Документы: `docs/specs/image-client.md` F7.3–F7.4 (что сделано), `docs/architecture.md` (новые типы).

Файлы: `ImageGen/ImageGen.Core/Storage/IGalleryRepository.cs`, `ImageGen/ImageGen.Core/Domain/Gallery/GalleryQuery.cs`, `GalleryPage.cs`, `GalleryItem.cs`, `GalleryCursor.cs` (новые); `ImageGen/ImageGen.Data/Migrations/NNNN_gallery_index.sql`, `ImageGen/ImageGen.Data/Repositories/GalleryRepository.cs` (новые), `DataServiceCollectionExtensions.cs`; `ImageGen/ImageGen.Presentation/PageModels/GalleryPageModel.cs`, `GalleryItemViewModel.cs` (новые), `Routes.cs`, `SessionsPageModel.cs`, `AppResources.resx/.cs`; `ImageGen/ImageGen.Avalonia/Views/GalleryView.axaml(.cs)`, `Converters/ThumbnailConverter.cs`, `Services/Navigation/PageRoutes.cs`, `Views/SessionsListView.axaml`, `Composition/AppServices.cs`; `ImageGen/ImageGen.Maui/Pages/GalleryPage.xaml(.cs)`, `Controls/SessionsListView.xaml`, `MauiProgram.cs`; тесты Data/Presentation/Avalonia.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Ревью по поручению владельца: устаревшая блокировка ожиданием 016 снята; скелет уже существует. Задача возвращена в `todo` для подготовки одобряемого плана. Текущие границы и критерии уточнены по коду и спецификации; это не одобрение реализации. Исторические комментарии выше сохранены.

### 2026-10-31, claude-audit
План готов, статус planned. Допущения: в галерее только успешные генерации с файлом; фильтр периода — всё время / 7 / 30 дней; вход — кнопка в списке сессий. Избранное, теги и коллекции не входят (отдельные задачи).

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен вместе с допущениями из комментария выше.
