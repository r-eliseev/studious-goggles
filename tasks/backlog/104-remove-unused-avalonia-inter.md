---
id: 104
status: in-progress
assignee: opencode-vps
claimed: 2026-10-29T17:27:08Z
created: 2026-10-29
---

# Убрать неиспользуемый пакет Avalonia.Fonts.Inter

## Описание
Найдено аудитом 2026-10-29 (`docs/log/2026-10-29-audit.md`): `ImageGen/Directory.Packages.props` объявляет `<PackageVersion Include="Avalonia.Fonts.Inter" Version="12.1.4" />`, но ни один `*.csproj` на пакет не ссылается и вызова `WithInterFont()` в коде нет (проверено grep 2026-10-29). Решение владельца (2026-10-29): убрать.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Строки `Avalonia.Fonts.Inter` в `ImageGen/Directory.Packages.props` нет.
- Avalonia собирается, тесты Avalonia проходят.

## Анализ
- Пакеты версионируются централизованно (`Directory.Packages.props`), поэтому неиспользуемая строка ничего не ломает и ничего не делает. Других упоминаний в репозитории, кроме `docs/status.md` и журнала аудита, нет.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, после каждого шага — сборка и тесты из «Проверки». Имена файлов переноси дословно. Рутинное расхождение номера версии или форматирования исправлять в рамках удаления одной неиспользуемой декларации. Если после свежего rebase пакет получил реальные потребители, его не удалять вслепую: записать найденные ссылки и запросить пересмотр задачи, потому что допущение о неиспользуемости изменилось.

Прочитай перед началом: `ImageGen/Directory.Packages.props`.

1. Убедиться, что пакет не используется: `rg -n "Avalonia\.Fonts\.Inter|WithInterFont" ImageGen -g '*.csproj' -g '*.cs' -g '*.props' -g '*.axaml'`. Ожидается ровно одна строка — в `ImageGen/Directory.Packages.props`. Реальные PackageReference/WithInterFont означают изменившуюся нужность: остановить удаление и перечислить потребителей в «Комментариях». Совпадения в комментариях не являются использованием. Не удалять другие пакеты и не менять шрифт приложения.
2. Удалить из `ImageGen/Directory.Packages.props` единственный `PackageVersion` с `Include="Avalonia.Fonts.Inter"`, независимо от текущей версии пакета. Больше ничего в файле не менять.
3. Выполнить «Проверку».
4. «Отчёт»: почему задача остаётся нужной (лишняя декларация зависимости), вывод поиска до/после и результат сборки/тестов. Новые тесты для удаления неиспользуемой декларации не нужны. Завершить через `scripts/release-task.sh 104`, затем `bash scripts/check.sh`.

### Проверка
```
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
bash scripts/check.sh
```

Файлы: `ImageGen/Directory.Packages.props`

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал claude-plan)
Решение: убрать неиспользуемый `Avalonia.Fonts.Inter`; `CommunityToolkit.Maui` оставить (ADR 0018).
