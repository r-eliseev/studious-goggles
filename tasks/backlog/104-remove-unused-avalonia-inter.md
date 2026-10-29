---
id: 104
status: approved
assignee:
claimed:
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
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, после каждого шага — сборка и тесты из «Проверки». Имена файлов переноси дословно. Если шаг не получается так, как написано, не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

Прочитай перед началом: `ImageGen/Directory.Packages.props`.

1. Убедиться, что пакет не используется: `grep -rn "Avalonia.Fonts.Inter\|WithInterFont" ImageGen --include=*.csproj --include=*.cs --include=*.props --include=*.axaml`. Ожидается ровно одна строка — в `ImageGen/Directory.Packages.props`. Если строк больше — поставить `blocked` и перечислить их в «Комментариях».
2. Удалить из `ImageGen/Directory.Packages.props` строку `<PackageVersion Include="Avalonia.Fonts.Inter" Version="12.1.4" />`. Больше ничего в файле не менять.
3. Выполнить «Проверку».
4. «Отчёт»: вывод шага 1 и результат проверки.

### Проверка
```
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
./scripts/check.sh
```

Файлы: `ImageGen/Directory.Packages.props`

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал claude-plan)
Решение: убрать неиспользуемый `Avalonia.Fonts.Inter`; `CommunityToolkit.Maui` оставить (ADR 0018).
