---
id: 034
status: in-progress
assignee: claude-qnrqyt
claimed: 2026-10-27T00:00:00Z
created: 2026-10-27
---

# Пустое MAUI-приложение и MAUI-сборка в CI

## Описание
Создать проект `ImageGen.Maui` шаблоном `dotnet new maui`, подключить центральные версии пакетов, CommunityToolkit и ссылку на `ImageGen.Core`, добавить проект в решение и вернуть в CI задачу `maui` (сборка под Windows и Android на `windows-latest`). Отделено от задачи 001, потому что требует MAUI-нагрузок.

Зависит от: 001.
Среда: только Windows с установленными нагрузками MAUI (`dotnet workload list` показывает `maui-windows` и `android`).

## Критерии готовности
- `ImageGen/ImageGen.Maui/` создан шаблоном `dotnet new maui` и изменён только так, как сказано в шагах 2–3.
- `ImageGen/ImageGen.slnx` содержит строку проекта `ImageGen.Maui`.
- В `.github/workflows/build.yml` есть задача `maui` из шага 5, задача `core` не изменена.
- Все команды из раздела «Проверка» завершаются без ошибок и предупреждений.

## Анализ
- Проверено пробной сборкой 2026-10-27 (`docs/knowledge/maui.md`, раздел «Проверено пробной сборкой»): шаблон отформатирован табами (нужен один запуск `dotnet format`); `CommunityToolkit.Maui` без `.UseMauiCommunityToolkit()` даёт ошибку `MCT001`; при центральных версиях атрибут `Version` в `PackageReference` запрещён.
- Работать в клоне по короткому пути (например `C:\git\studious-goggles`): длинный путь ломает Windows-сборку (`MSB3030`).
- Шаблонная `MainPage` остаётся до задачи 013.
- Правка `.github/workflows/build.yml` разрешена одобрением этого плана владельцем.
- Итоговый csproj пробной сборки: `poc/v0.1/034-maui-scaffold/` (подсказка, см. `poc/README.md`).

## План
1. Из корня репозитория:
   ```
   dotnet new maui -n ImageGen.Maui -o ImageGen/ImageGen.Maui
   ```
   Сообщение шаблона об ошибке восстановления пакетов (`Restore failed`) ожидаемо: центральные версии ещё не подключены. Восстановление пройдёт при сборке после шага 2.

2. Изменить `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, созданный шаблоном:
   - у всех `PackageReference` убрать атрибут `Version` (версии центральные, из `Directory.Packages.props`): это `Microsoft.Maui.Controls` и `Microsoft.Extensions.Logging.Debug`;
   - рядом с `Microsoft.Maui.Controls` добавить `PackageReference` на `CommunityToolkit.Mvvm` и `CommunityToolkit.Maui` (тоже без версии);
   - `ApplicationTitle` поменять с `ImageGen.Maui` на `ImageGen`;
   - в конец добавить `ItemGroup` с `ProjectReference` на `../ImageGen.Core/ImageGen.Core.csproj`.
   Остальное в csproj не менять (в том числе `ApplicationId`).

3. В `ImageGen/ImageGen.Maui/MauiProgram.cs` добавить `using CommunityToolkit.Maui;` первой строкой и строку `.UseMauiCommunityToolkit()` сразу после `.UseMauiApp<App>()`.

4. В `ImageGen/ImageGen.slnx` перед строкой `</Solution>` добавить строку `  <Project Path="ImageGen.Maui/ImageGen.Maui.csproj" />`. Затем отформатировать шаблонные файлы: `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` (без `--verify-no-changes`).

5. В конец `.github/workflows/build.yml` (после задачи `core`, через пустую строку, с теми же отступами) добавить задачу `maui` с комментарием, почему Android собирается на Windows (установка `maui-android` на ubuntu-latest падала с «Disk quota exceeded»). Задача: `runs-on: windows-latest`; шаги по порядку:
   - checkout и setup-dotnet с `dotnet-version: 10.0.x` (те же версии действий, что у `core`);
   - `dotnet workload install maui`;
   - `dotnet format` MAUI-проекта с `--verify-no-changes`;
   - сборка Release под `net10.0-windows10.0.19041.0`;
   - сборка Release под `net10.0-android`.

Файлы: `ImageGen/ImageGen.Maui/**`, `ImageGen/ImageGen.slnx`, `.github/workflows/build.yml`

### Проверка
```
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт

Статус `in-progress`: Windows-часть не проверена (сборка Windows-цели на Linux невозможна, владелец подтвердил). Занятость не снимается до проверки на Windows.

**Сделано (по шагам плана):**
1. `dotnet new maui -n ImageGen.Maui -o ImageGen/ImageGen.Maui`. Восстановление упало с NU1008 (ожидаемо, см. план).
2. `ImageGen.Maui.csproj`: убран `Version` у `Microsoft.Maui.Controls` и `Microsoft.Extensions.Logging.Debug`; добавлены `CommunityToolkit.Mvvm` и `CommunityToolkit.Maui` без версии (версии уже есть в `Directory.Packages.props`, новых версий не понадобилось); `ApplicationTitle` = `ImageGen`; `ItemGroup` с `ProjectReference` на `../ImageGen.Core/ImageGen.Core.csproj`. `ApplicationId` не менялся.
3. `MauiProgram.cs`: `using CommunityToolkit.Maui;` первой строкой и `.UseMauiCommunityToolkit()` после `.UseMauiApp<App>()`.
4. `ImageGen/ImageGen.slnx`: строка `ImageGen.Maui/ImageGen.Maui.csproj`. `dotnet format` по проекту выполнен.
5. `.github/workflows/build.yml`: задача `maui` (windows-latest, с комментарием про Disk quota). Задача `core` не менялась.

**Проверено здесь (Linux, SDK 10.0.403, maui-android, Android SDK platform-36):**
- `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes`: без изменений.
- `dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes`: без изменений.
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`: успешно, 0 предупреждений, 0 ошибок. Также `-c Release`: успешно.
- `dotnet build ImageGen.Core`, `dotnet test` для ImageGen.Core.Tests (59/59) и ImageGen.Data.Tests (16/16): пройдены.
- `scripts/check.sh`: проходит (см. финальный прогон ниже).
- Юнит-тестов в плане нет; MAUI-тестов на устройстве здесь не запускал (нет эмулятора).

**Не проверено на Windows:**
- `dotnet format ... --verify-no-changes` для MAUI-проекта на Windows (CI-шаг задачи `maui`) — выполнено только на Linux.
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` (Linux не может собрать Windows-цель, NETSDK1100/XAML-компилятор, см. `docs/knowledge/claude-cloud-session.md`). Проверить также, что нет `MSB3030` (путь клона `C:\git\studious-goggles`).
- Запуск под Windows не выполнялся.
- CI: задача `maui` ещё не запускалась на GitHub.

**Отклонения от плана:**
- `docs/architecture.md`: раздел «Состояние кода» в плане не добавлялся. По `docs/log/2026-10-27-architecture-without-progress.md` такого раздела больше нет; архитектура описывает задуманное, и код с ней не расходится.
- В `ImageGen.Maui.csproj` оставлена `ProjectReference` только на `ImageGen.Core` (как в плане). `docs/architecture.md` предполагает `Maui → Data`; ссылка на Data, вероятно, нужна в задаче 012. Проверить там.
- Шаблон создал `ImageGen/ImageGen.Maui/.gitignore` и `Platforms/*`, `Resources/*`, `MainPage.xaml`: оставлены как есть (план их не трогает). `csproj` по-прежнему с табами (шаблон), `dotnet format` XML не переформатирует. План этого не требует.
- Android SDK в облачной сессии ставился заново (по `docs/knowledge/claude-cloud-session.md`, раздел «Как поставить»). Без него `dotnet format` по MAUI-проекту не загружается. Записано в `docs/knowledge/claude-cloud-session.md`.

**Файлы:** `ImageGen/ImageGen.Maui/` (новый, из шаблона, с правками), `ImageGen/ImageGen.slnx`, `.github/workflows/build.yml`, `docs/knowledge/claude-cloud-session.md`, `tasks/v0.1/034-maui-scaffold.md`.

## Комментарии
