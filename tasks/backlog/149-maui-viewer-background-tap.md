---
id: 149
status: planned
assignee:
claimed:
created: 2026-10-30
---

# Закрывать MAUI viewer по нажатию на фон

## Описание
backgroundTap использует GetPosition(_image) is null и GetPosition(controls) is null как hit-test. Для подключённого view GetPosition преобразует координаты и возвращает точку также за его границами; null не означает нажатие вне картинки. Условие закрытия по фону не выполняет требование131.

Зависит от: 146.
Среда: Linux или Windows с Android SDK; реальные taps — Android manual140, Windows manual016.

## Критерии готовности
- Нажатие на свободный чёрный фон закрывает viewer; на отображённое изображение, Close/Previous/Next и toolbar — не запускает дополнительное закрытие.
- AspectFit letterbox учитывается как фон, сам видимый bitmap не закрывает viewer.
- До успешной загрузки и при неизвестной геометрии нет случайного закрытия по изображению/кнопкам; при неизвестных dimensions viewport Image целиком считается защищённым.
- Геометрия проверена unit tests, MAUI Android собирается.

## Анализ
Evidence: MauiImageViewerService.cs:81–85. GetPosition не является проверкой попадания. У Image Fill/AspectFit размер view может занимать всё поле, поэтому проверки только bounds view недостаточно для letterbox. Вместе с146/148 это остаток131; при правке shared Maui file соблюдать порядок146→149→148.

## План
1. Прочитать построение Grid и обработчик backgroundTap. Использовать координаты tap в корневом Grid и фактические bounds image/toolbar/close в той же системе координат.
2. Добавить чистый helper геометрии в Presentation/Services/ImageViewerHitTest.cs без MAUI/Avalonia типов: точка, прямоугольники контролов, viewport изображения, размеры image header. AspectFit scale=min(viewWidth/pixelWidth,viewHeight/pixelHeight); центрированный visible rectangle.
3. Тесты сначала: tap внутри visible image не закрывает; letterbox сверху/сбоку закрывает; controls/Close не закрывают дополнительным действием; внешняя padding area закрывает; нулевые размеры/нет точки дают безопасное отсутствие закрытия. Проверить границы прямоугольников.
4. Выбран один путь данных: добавить optional RelativePath в ImageViewerItem; в GenerationItemViewModel открыть уже существующий _generationImagePath как readonly RelativeImagePath, без нового I/O; ChatPageModel заполняет это поле snapshot. MauiImageViewerService получает существующий IImageFileStore через DI и передаёт page. В LoadAsync MAUI прочитать header dimensions через TryReadDimensionsAsync(RelativePath, token); AbsolutePath в этот относительный API не передавать. Результат применить только с текущим epoch guard146. При отсутствии relative path/dimensions считать весь Image viewport защищённым. Не добавлять decoder/parser, новый DB lookup или отдельный cache. Старые трёхаргументные вызовы ImageViewerItem остаются допустимыми, Avalonia поле игнорирует.
5. backgroundTap использует helper с локальными координатами; null point → ничего не делать. Buttons имеют собственные действия и не вызывают background close. Сохранить lifecycle/epoch проверки146.
6. В140/016 дописать aspect ratios portrait/landscape, taps на bitmap, letterbox, padding и toolbar; записать ручные ограничения. Изменения ImageViewerItem должны сохранить source compatibility остальных shell; DI constructor calls MauiImageViewerService найти через rg.

Файлы: `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs`, `ImageGen/ImageGen.Presentation/Services/ImageViewerHitTest.cs` (новый), соответствующие Presentation.Tests; `ImageGen/ImageGen.Presentation/Services/ImageViewerItem.cs`, `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ChatPageModel.cs` и их тесты для relative path; задачи016/140.

Проверка: Presentation.Tests; Avalonia.Tests при изменении общего item; `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`.

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
