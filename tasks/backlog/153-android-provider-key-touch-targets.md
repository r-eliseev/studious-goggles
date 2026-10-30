---
id: 153
status: planned
assignee:
claimed:
created: 2026-10-30
---

# Соблюдать Android touch target у кнопок ключей

## Описание
В MAUI Settings новые provider-row Change/Remove имеют локальный MinimumHeightRequest=44. Он переопределяет общий Button style с IgControlHeight=48 для Android. Это не соответствует platform target sizes из плана135 и docs/design/implementation.md.

Зависит от: — (135 удерживается аудитом до этого остатка).
Среда: Linux или Windows с Android SDK.

## Критерии готовности
- Change/Remove наследуют общий platform-aware минимум: Android48, iOS44, desktop по текущей дизайн-системе; local44 отсутствует.
- Команды, подписи, семантика и provider rows не изменены.
- MAUI Android собирается; существующая проверка разметки настроек подтверждает использование общего стиля, ручной Android пункт записан в140.

## Анализ
Evidence: ImageGen.Maui/Pages/SettingsPage.xaml:27,33 и Resources/Themes/Common.xaml:35–39 (IgControlHeight). Functional key flow135 завершён; остаток только в обязательном platform size. Не задавать48 на всех платформах и не менять все кнопки приложения. Сам rendered size на телефоне этим source-аудитом не измерялся; найдено нарушение минимальной гарантии стиля.

## План
1. Прочитать SettingsPage.xaml, Button implicit/ghost styles и IgControlHeight в Resources/Themes; найти существующий markup test настроек в Presentation.Tests/Design.
2. Удалить только два локальных MinimumHeightRequest=44 у Change/Remove, чтобы implicit Button style применил DynamicResource IgControlHeight. Если ghost style явно задаёт высоту, минимально привязать именно эти две кнопки к тому же resource вместо нового числового значения.
3. Дополнить существующую проверку разметки настроек утверждением, что эти кнопки не переопределяют platform минимум44 и используют общий стиль/ресурс. Новый test project не создавать; общие стили не переписывать.
4. Собрать MAUI Android, прогнать Presentation.Tests и format изменённого проекта; в140 добавить проверку обеих кнопок на Android (не меньше48dp, длинные provider names, TalkBack). Windows UI не объявлять проверенным на Linux.
5. В135 добавить отчёт о закрытии остатка, вернуть done после проверок. Другие критерии/исторические комментарии135 не переписывать.

Файлы: `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, существующий settings markup test в `ImageGen/ImageGen.Presentation.Tests/Design`, `tasks/backlog/140-android-manual-check.md`, `tasks/backlog/135-provider-neutral-key-ui.md`.

Проверка: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`; Presentation.Tests; harness.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
bash scripts/check.sh
```

### Порядок исполнения и проверки
План рассчитан на Luna: прочитать перечисленные файлы, выполнить шаги по порядку; сначала падающий регрессионный тест, затем минимальный фикс (ub-mode из AGENTS.md). Не реализовывать соседние задачи. Сигнатуры сверять с текущим main; при rebase сохранять чужие изменения. Не использовать платные API и не записывать секреты. Команды выполнять последовательно: общие build outputs не допускают параллельные dotnet build/test.
После изменения каждого проекта: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`; после всех изменений — перечисленные тесты и `bash scripts/check.sh`. В отчёте указать команды, результаты, ручные ограничения и отличие от плана. Код писать только после одобрения плана по docs/tasks.md.

## Отчёт

## Комментарии

### 2026-10-30, codex-audit
Подробный план по поручению владельца для Luna. Реализация не начата; planned не означает approved.
