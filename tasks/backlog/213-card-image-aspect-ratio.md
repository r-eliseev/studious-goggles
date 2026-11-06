---
id: 213
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Картинка в карточке — в исходных пропорциях, без обрезки

## Описание
Отчёт владельца (v0.1.11, Android): «пропорции картинок не соответствуют оригиналу, картинка обрезана» — на скриншоте картинка 1024×1024 (1:1) показана в области 3:2 и обрезана сверху и снизу. Причина: область картинки карточки фиксирована 264×176 и заполняется с обрезкой — MAUI `Image Aspect="AspectFill"` (`ChatPage.xaml`), Avalonia `Image Stretch="UniformToFill"` в `Border Width="264" Height="176"` (`ChatView.axaml`).

Зависит от: 211 и 212 (тот же `ChatPage.xaml`; делать после их слияния).
Среда: Linux (Presentation, Avalonia headless, сборка MAUI Android).

## Критерии готовности
- Ширина области картинки прежняя (264), высота — по пропорциям картинки: `264 × высота / ширина`, ограничена диапазоном 132–352 (крайне широкие/высокие картинки — с полями, без обрезки). Пока размеры неизвестны (загрузка, нет файла, не картинка) — 176, как сейчас.
- Картинка вписывается целиком: MAUI `Aspect="AspectFit"`, Avalonia `Stretch="Uniform"`; поля — фоном области (`IgSunkenColor`/текущий фон).
- Расчёт — чистая функция в Presentation (`CardImageLayout.HeightFor(int? pixelWidth, int? pixelHeight)` в `ImageGen.Presentation/PageModels/` или `Utilities/`), свойство `GenerationItemViewModel.ImageAreaHeight` (`double`) обновляется в `ApplyImageDimensions` с уведомлением.
- MAUI: `HeightRequest="{Binding ImageAreaHeight}"` у `Border` области картинки (вместо `HeightRequest="176"`), Avalonia: `Height="{Binding ImageAreaHeight}"`. Плейсхолдеры статусов (идёт, ошибка) остаются 176.
- Тесты: функция (1:1 → 264; 16:9 → 148.5; 9:16 → 352 (ограничение); 3:2 → 176; `null` → 176; 1×1000 → 352; 1000×1 → 132); модель карточки после `ApplyImageDimensions(1024×1024)` — `ImageAreaHeight == 264` и событие изменения; Avalonia headless — у картинки карточки `Stretch.Uniform`.
- Сборка MAUI Android; пункт 140: квадратная, горизонтальная и вертикальная картинки показаны целиком.

## Анализ
`GenerationItemViewModel.ApplyImageDimensions(ImageDimensions?)` уже вычисляет текст размеров (`ImageDimensionsText`, `HasImageDimensions`); размеры приходят из `IImageFileStore.TryReadDimensionsAsync`. MAUI-карточка — `ChatPage.xaml` (`Border WidthRequest="264" HeightRequest="176"` вокруг `Image`), Avalonia — `ChatView.axaml` (строка около 188). Галерея (022) и просмотрщик не меняются.

## План
1. Падающие тесты функции и модели.
2. Функция и свойство.
3. Разметка обеих оболочек; тест Avalonia; сборка MAUI Android; пункт 140.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/CardImageLayout.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/CardImageLayoutTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Avalonia.Tests/Views/` (тест разметки карточки), `tasks/backlog/140-android-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
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

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца о сборке v0.1.11 на Android (MAUI, логи 2026-11-05 и 2026-11-06, скриншоты). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
