---
id: 192
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-06T19:28:59Z
created: 2026-11-06
---

# Avalonia: диалоги поверх страницы (для Android)

## Описание
ADR 0026, п. 4. `AvaloniaDialogService` показывает диалоги отдельными окнами (`Window.ShowDialog`), а на Android окно одно. Нужна вторая реализация `IDialogService`, которая показывает диалог слоем поверх страницы внутри заданной панели. Код без Android API, в `ImageGen.Avalonia`, проверяется headless-тестами. Десктоп продолжает использовать оконные диалоги.

Зависит от: 191.
Среда: Linux или Windows.

## Критерии готовности
- `OverlayDialogService : IDialogService` реализует все пять методов интерфейса (`AlertAsync`, два `ConfirmAsync`, `PromptAsync`, `ChooseAsync`) с теми же текстами кнопок и порядком, что `AvaloniaDialogService` (порядок кнопок — общий `DialogButtonOrder`, если он подходит).
- Диалог — затемнённый слой на всю панель-хост с карточкой по центру; ширина карточки не больше 480 и не шире панели минус отступы (узкий экран телефона).
- Пока диалог открыт, страница под ним не получает нажатий; одновременно открыт не больше одного диалога (второй вызов ждёт закрытия первого).
- Закрытие: кнопки диалога и метод `TryCloseTop()` (для системного Back), который закрывает верхний диалог как «Отмена» (`false` / `null`) и возвращает `true`, если было что закрывать.
- Подтверждение с `destructive: true` выделяет кнопку действия так же, как оконный вариант.
- Доступность: у карточки `AutomationProperties.Name` = заголовок, фокус при открытии — на основной кнопке (у `Prompt` — на поле ввода), Enter = основная кнопка, Escape = отмена.
- Headless-тесты на каждый метод и на `TryCloseTop`.

## Анализ
Оконная реализация: `ImageGen/ImageGen.Avalonia/Services/AvaloniaDialogService.cs` (180 строк) и `DialogLayout.cs`; её тесты — `ImageGen.Avalonia.Tests/Services/DialogLayoutTests.cs`. Пробник 094 (`poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/Platform/TopLevelServices.cs`) делал пять диалогов слоем — это подсказка, не образец кода (сверяться с текущим `AvaloniaDialogService`).

Хост: служба получает `Func<Panel?> host` — панель, куда добавляется слой (на Android её даст `MainView`, задача 195). Если хоста нет, `AlertAsync` завершается без показа, остальные методы возвращают отмену, и пишется одна Warning через `ILogger`.

## План
1. Тесты `ImageGen/ImageGen.Avalonia.Tests/Services/OverlayDialogServiceTests.cs` (headless, по образцу существующих оконных тестов: `[AvaloniaFact]`, окно-хост с `Grid` как панелью):
   - `AlertAsync` → в панели появился слой с заголовком и текстом; нажатие OK (`Button.Command`/`RaiseEvent(Click)`) завершает задачу и убирает слой;
   - `ConfirmAsync` → accept даёт `true`, cancel — `false`; `destructive: true` — у кнопки действия тот же класс стиля, что в оконном варианте;
   - `PromptAsync` с начальным значением → поле содержит его; изменить текст, accept → новый текст; cancel → `null`;
   - `ChooseAsync` с тремя вариантами → нажатие второго даёт его текст; cancel → `null`;
   - `TryCloseTop()` при открытом confirm → `true`, задача даёт `false`; без диалога → `false`;
   - второй `AlertAsync`, вызванный до закрытия первого, показывается только после закрытия первого;
   - хост `null` → `ConfirmAsync` даёт `false` без исключения.
2. `ImageGen/ImageGen.Avalonia/Services/OverlayDialogService.cs`: конструктор `(Func<Panel?> host, ILogger<OverlayDialogService> logger)`; общий приватный метод показа слоя, возвращающий `Task<T>` через `TaskCompletionSource`; очередь через `SemaphoreSlim(1,1)`; разметка кодом (как в оконной версии), стили — те же классы, что у кнопок оконного диалога.
3. В DI ничего не регистрировать: регистрирует Android-голова (195).

Файлы: `ImageGen/ImageGen.Avalonia/Services/OverlayDialogService.cs` (новый), `ImageGen/ImageGen.Avalonia.Tests/Services/OverlayDialogServiceTests.cs` (новый).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
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

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй андроид на авалонии, с билдами»; выбрано: второе приложение рядом с MAUI, APK в каждом релизе). ADR 0026. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен («Да» на вопрос об одобрении 191–198 и PR).
