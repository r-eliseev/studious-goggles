---
id: 023
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Сравнение результатов

## Описание
Сравнение двух результатов рядом и слайдером поверх друг друга, с синхронным zoom/pan (F6.3). Обычный viewer F6.2 принадлежит 131, настоящий OS fullscreen — 137 и её платформенной части.

Зависит от: 131.
Среда: Linux или Windows; обе UI-оболочки, ручная проверка desktop.

016 — ручная проверка, а не prerequisite кода; fullscreen не нужен для сравнения двух изображений. Статус `todo`: планировать после стабилизации контракта viewer 131, реализация после одобрения плана.

## Критерии готовности
- Пользователь выбирает ровно два успешных результата, видит их рядом и в режиме наложения со слайдером.
- Zoom/pan синхронные; разные разрешения и пропорции не растягивают изображение и не ломают совмещение.
- Под каждой картинкой показаны модель, сид и цена, неизвестная цена не отображается как ноль.
- Отсутствующий файл/удалённая сессия дают ошибку с возможностью закрыть сравнение; bitmap и подписки освобождаются при закрытии.
- Режим сравнения работает в MAUI и Avalonia без требования OS fullscreen.

## Анализ
Не дублировать декодирование и жизненный цикл окна из 131. Общие метаданные/selection принадлежат Presentation; bitmap, указатель, touch gestures и clipping принадлежат оболочкам. Тип текущего элемента прогона — `RunItemViewModel`; для выбора результата использовать GenerationId и проверку успешного статуса/пути, а не имя временного UI-контрола.

С 131 общий код диалога/viewer и строки ресурсов. До её завершения план можно исследовать, но реализацию не занимать. Разные размеры требуют общей нормализованной системы viewport, а не копирования абсолютных пикселей pan между картинками. Прежде чем давать исполнение небольшой модели, в плане должны быть определены порядок выбора, способ открытия сравнения и поведение изображения за границами viewport.


Уточнение 2026-10-31 (claude-audit): код viewer 131 в main (`IImageViewerService`, `ImageViewerItem`, `ImageViewerNavigationState`; Avalonia и MAUI реализации); 131 держится только Windows-задачей 148. Контракт открытия уже есть — сравнение делается отдельным сервисом рядом, а не режимом существующего viewer.

## План
Реализация — после того как 131 станет done. Сначала падающий тест, затем код.

1. **Выбор.** В меню карточки успешной генерации — «Compare…». Открывает выбор второй картинки: успешные генерации той же сессии (кроме исходной), в порядке ленты, с моделью и сидом. Сравнение между сессиями — позже через галерею 022 (вне объёма).
2. **Модель.** `Presentation/PageModels/ComparisonPageModel` (не зависит от UI-типов): два `ImageViewerItem` + метаданные (модель, сид, цена: `ImagePriceText`/«price unknown»), `Mode` (`SideBySide | Overlay`), `Zoom` (1..8), `Offset` в нормализованных координатах (доля кадра, −0.5..0.5 после clamp), `SliderFraction` 0..1. Обе картинки вписываются (fit) в одинаковый viewport; zoom/offset применяются к нормализованным координатам, поэтому разные размеры и пропорции совпадают по центру и не растягиваются.
3. **Сервис.** `IImageComparisonService.ShowAsync(ComparisonPageModel)` — реализации `AvaloniaImageComparisonService` (модальное окно поверх главного, как viewer) и `MauiImageComparisonService` (modal page). Загрузка и освобождение bitmap — теми же способами, что во viewer 131; отсутствующий файл → сообщение и закрытие, без висящего окна.
4. **Жесты.** Avalonia: колесо — zoom вокруг курсора, перетаскивание — pan, оба изображения через один `TransformGroup`. MAUI: `PinchGestureRecognizer` + `PanGestureRecognizer` на общем контейнере, `Scale`/`TranslationX/Y` обоих `Image`. Overlay: верхнее изображение обрезается `Clip` по `SliderFraction`, `Slider` под картинкой; клавиши ←/→ двигают слайдер на десктопе.
5. **Тесты.** Presentation: выбор только двух успешных; второй удалён до открытия; clamp zoom/offset/slider; нормализация для разных пропорций (вписанный прямоугольник); неизвестная цена не ноль. Avalonia headless: открытие, переключение режима, синхронный zoom обоих, закрытие освобождает обе картинки. Ручные: 046 (desktop), 140 (Android, pinch), 016 (Windows).

Проверки: Presentation.Tests, Avalonia.Tests, сборка MAUI Android, `dotnet format`, `scripts/check.sh`. Документы: `docs/specs/image-client.md` F6.3, `docs/architecture.md`.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/ComparisonPageModel.cs`, `ComparisonMode.cs`, `ImageGen/ImageGen.Presentation/Services/IImageComparisonService.cs`, `ComparisonViewport.cs` (новые), `GenerationItemViewModel.cs`, `ChatPageModel.cs`, `AppResources.resx/.cs`; `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageComparisonService.cs`, `Views/ComparisonView.axaml(.cs)` (новые), `Views/ChatView.axaml`, `Composition/AppServices.cs`; `ImageGen/ImageGen.Maui/Services/MauiImageComparisonService.cs`, `Pages/ComparisonPage.xaml(.cs)` (новые), `Pages/ChatPage.xaml`, `MauiProgram.cs`; тесты Presentation и Avalonia; `tasks/backlog/046-desktop-manual-check.md`, `tasks/backlog/140-android-manual-check.md`, `tasks/v0.1/016-skeleton-manual-check.md`.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Ревью по поручению владельца: устаревшая блокировка ожиданием 016 снята; скелет уже существует. Задача возвращена в `todo` для подготовки одобряемого плана. Текущие границы и критерии уточнены по коду и спецификации; это не одобрение реализации. Исторические комментарии выше сохранены.

### 2026-10-31, claude-audit
План готов, статус planned; занимать после done 131. Допущения: вторая картинка выбирается из той же сессии; zoom 1–8×; сравнение — отдельное модальное окно, не режим viewer.
