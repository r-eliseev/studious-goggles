---
id: 048
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Headless-тесты Avalonia трогают настоящую папку данных пользователя

## Описание
Дефект (ревью 2026-10-28). Каждый headless-тест Avalonia запускает настоящий `App`, а `App.OnFrameworkInitializationCompleted` берёт `new DesktopAppPaths()` — настоящую папку данных пользователя (`%LOCALAPPDATA%\ImageGen` на Windows, `~/.local/share/ImageGen` на Linux) и `DesktopSecretStore.ForCurrentPlatform` — настоящее хранилище ключей. В результате прогон тестов:
- открывает и **мигрирует настоящую базу** `imagegen.db` (тесты ветки с новой миграцией переведут рабочую базу на невыпущенную схему);
- пишет в настоящую папку логов: прогон 2026-10-28 на Windows создал 30 файлов `imagegen-20261028_003.log` … `_032.log` по одному на тест (логгер предыдущего экземпляра `App` не закрыт, Serilog открывает новый файл с суффиксом);
- читает настоящее хранилище ключей (DPAPI-файл на Windows, Secret Service на Linux — на рабочем столе это может вызвать запрос на разблокировку связки ключей).

Нужно, чтобы тесты работали только во временной папке и с хранилищем ключей в памяти, а приложение по-прежнему использовало настоящую папку.

Зависит от: —.
Среда: Linux или Windows.

## Критерии готовности
- Прогон `ImageGen.Avalonia.Tests` не создаёт и не меняет ничего в настоящей папке данных (проверка в «Отчёте»: время изменения `imagegen.db` и число файлов в `logs/` до и после прогона на Windows и Linux).
- Тесты не обращаются к настоящему хранилищу ключей (кроме отдельных интеграционных тестов DPAPI и Secret Service, которые и сейчас работают со своей временной папкой или явно со службой).
- Один процесс тестов не плодит файлы логов: экземпляр `App` закрывает свой логгер при завершении теста.
- Переменная `IMAGEGEN_DATA_DIR` задаёт папку данных Avalonia-приложения (для тестов и для ручной отладки, например второго профиля на Linux).

## Анализ
- Где: `ImageGen/ImageGen.Avalonia/App.axaml.cs` (`OnFrameworkInitializationCompleted`: `new DesktopAppPaths()`, `DesktopSecretStore.ForCurrentPlatform(paths)`), `ImageGen/ImageGen.Avalonia/Platform/DesktopAppPaths.cs`, `ImageGen/ImageGen.Avalonia.Tests/TestAppBuilder.cs` (`AppBuilder.Configure<App>().UseHeadless(...)` без подмены путей). Тестовые `TempAppPaths` и `FixedAppPaths` есть, но используются только в контейнерах, которые тесты собирают сами, а не в старте `App`.
- Освобождение: `Services?.Dispose()` вызывается только на `desktop.Exit`; в headless-тестах классического жизненного цикла нет, и провайдер с файловым логгером не освобождается.
- MAUI-оболочка не затронута: её тесты не запускают `MauiApp`.
- Пересечений с задачами в очереди нет (027 и 025 не трогают `App` и тестовый хост Avalonia).

## План
1. `DesktopAppPaths`: папка данных — `IMAGEGEN_DATA_DIR`, если переменная задана и не пустая (полный путь; создаётся при первом использовании), иначе как сейчас. Документировать в `docs/knowledge/dev-environment.md` и `AGENTS.md` («Команды»).
2. `App`: точка подмены для тестов — статическое свойство `App.HostOverrides` (пути, хранилище ключей; по умолчанию `null`), которое `OnFrameworkInitializationCompleted` использует вместо `DesktopAppPaths` и `DesktopSecretStore.ForCurrentPlatform`. Рабочий запуск его не задаёт.
3. `TestAppBuilder`: перед `UseHeadless` задаёт `App.HostOverrides` с временной папкой на прогон (удаляется после) и хранилищем ключей в памяти. Если тестам нужна своя папка на тест — фикстура xUnit, а не настоящий путь.
4. Освобождение: у `App` метод `Shutdown`/`Dispose`, который освобождает `Services` (и файловый логгер); вызывать из `desktop.Exit` и из завершения headless-теста (фикстура или `AvaloniaTestApplication` с изоляцией по тесту — по документации Avalonia, `docs/knowledge/avalonia.md`).
5. Тест-кейсы:
   - `DesktopAppPaths` с `IMAGEGEN_DATA_DIR=<temp>` → `DataDirectory` = `<temp>`; без переменной — `LocalApplicationData/ImageGen`.
   - Старт `App` в headless-тесте → `App.Services` разрешает `IAppPaths` с временной папкой; `ISecretStore` — в памяти.
   - Два теста подряд в одном процессе → в временной папке логов один файл за день, без суффиксов `_001`.
   - Охранный тест: в начале и конце прогона время изменения настоящего `imagegen.db` (если он есть) не меняется — или, проще, тест проверяет, что `App` в тестах никогда не создаёт `DesktopAppPaths` без переменной (через `HostOverrides`).
6. Ручная проверка в «Отчёте»: на Windows и Linux число файлов в настоящей `logs/` и время `imagegen.db` до и после `dotnet test`.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Avalonia/App.axaml.cs`, `ImageGen/ImageGen.Avalonia/Platform/DesktopAppPaths.cs`, `ImageGen/ImageGen.Avalonia.Tests/TestAppBuilder.cs`, `ImageGen/ImageGen.Avalonia.Tests/**` (новые тесты), `docs/knowledge/dev-environment.md`, `docs/knowledge/avalonia.md`, `AGENTS.md`

## Отчёт

## Комментарии

### 2026-10-28, ревью (claude, Windows)
Найдено при прогоне тестов на Windows: после `dotnet test` в `%LOCALAPPDATA%\ImageGen\logs` появилось 30 файлов по 677 байт со стартом приложения и переходом на страницу ключа, `imagegen.db` в той же папке обновлён. Эти файлы не удалял: это папка данных владельца. Их можно удалить вручную.

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.
