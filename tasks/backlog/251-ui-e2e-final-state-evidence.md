---
id: 251
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-10T11:03:20Z
created: 2026-11-07
---

# UI-тесты: конечное состояние на записи, поворот на экране ключа, маскировка ключа на Windows

## Описание
Разбор видео прогона [42398374909](https://github.com/r-eliseev/studious-goggles/actions/runs/42398374909) по кадрам (способ — `docs/knowledge/ui-e2e.md`, «Как смотреть видео и скриншоты прогонов») нашёл три проблемы.
1. **Android: запись заканчивается раньше проверки.** `InvalidKey_ShowsError` шёл 24,9 с (`metadata.json`), а `recording.mp4` длится 6,4 с и кончается на «Checking the key...»: текста ошибки, который проверял тест, на записи нет. `Rotate_KeepsKeyScreen` кончается на списке сессий. Последние кадры перед `StopRecordingScreen` в файл не попадают, а скриншот сохраняется только при падении (`TestArtifacts.Capture`).
2. **Android: `Rotate_KeepsKeyScreen` поворачивает список сессий, а не экран ключа.** Поворот идёт сразу после запуска, когда экран ключа ещё не открыт: первый запуск показывает список сессий около 1,6 с. `Find` после поворота просто дожидается перехода, поэтому сценарий не проверяется.
3. **Windows: ключ виден открытым текстом.** Поле ключа (`Entry IsPassword`) примерно 0,2 с показывает введённый текст открытым (`sk-or-e2e-invalid`) и только потом маскирует. Ключ вводился набором FlaUI `Enter`; при вставке не проверено.

Зависит от: —
Среда: GitHub Actions (`ui-e2e.yml`); локально — сборка E2E-проектов и `dotnet format` на Linux.

## Критерии готовности
- `TestArtifacts.Capture` (Android) и `WindowsTestBase` (Windows):
  - `screenshot.png` сохраняется всегда, а не только при падении: это конечное состояние;
  - дерево UI (`ui.xml` / `ui.txt`) — как сейчас, только при падении;
  - перед остановкой записи — пауза 1,5 с (константа `FinalFrameHold`), чтобы последний экран попал в ролик.
- Тест модели артефактов (если в проекте есть модульные тесты, как `VideoRecorderTests`): порядок «скриншот → пауза → остановка записи» — через подмену действий. Если для Android такого теста нет — проверка по прогону в CI.
- `Rotate_KeepsKeyScreen`: сначала `Find(TestIds.ApiKeyEntry)` (дождаться экрана ключа), затем `Landscape`. Дальше `Find(TestIds.ApiKeyEntry).Displayed` в альбомной ориентации, затем `Portrait` и та же проверка. Ориентацию проверить через `Driver.Orientation` после каждого поворота.
- Windows, маскировка:
  - Шаг 1 (факт). Отдельный временный тест: ввод через буфер обмена (`Clipboard` + `Ctrl+V` через FlaUI `Keyboard`) и запись. По кадрам записи (способ из `docs/knowledge/ui-e2e.md`) установить, виден ли ключ открытым текстом при вставке. Результат — в `docs/knowledge/maui.md` (раздел про Windows) с номером прогона. Временный тест удалить.
  - Если при вставке ключ виден — остановиться, `blocked`, вопрос владельцу: менять ли поле ключа на Windows (например, `PasswordBox` через handler). Это изменение приложения, вне этой задачи.
  - Если не виден — записать, что открытый текст бывает только при посимвольном наборе: это задержка маскировки MAUI на Windows. Задача на этом закрывается.
- Прогон `ui-e2e` в CI зелёный. По кадрам записи подтвердить и записать в «Отчёт»:
  - последний кадр `InvalidKey_ShowsError` (Android и Windows) показывает текст ошибки;
  - в `Rotate_KeepsKeyScreen` есть кадры экрана ключа в альбомной ориентации.

## Анализ
`ImageGen/ImageGen.E2E.Android/AndroidTestBase.cs` (≈25–48: запись и `TearDown`), `ImageGen/ImageGen.E2E.Android/TestArtifacts.cs` (≈34–60: `Capture`), `ImageGen/ImageGen.E2E.Android/FirstRunTests.cs`, `ImageGen/ImageGen.E2E.Windows/WindowsTestBase.cs` (≈73–120), `docs/knowledge/ui-e2e.md`.

## План
1. Android: скриншот всегда и пауза перед остановкой записи, прогон в CI, кадры.
2. Android: порядок шагов в `Rotate_KeepsKeyScreen`, прогон, кадры.
3. Windows: то же для скриншота и паузы; шаг с вставкой ключа и вывод.
4. Документация.

Файлы: `ImageGen/ImageGen.E2E.Android/AndroidTestBase.cs`, `ImageGen/ImageGen.E2E.Android/TestArtifacts.cs`, `ImageGen/ImageGen.E2E.Android/FirstRunTests.cs`, `ImageGen/ImageGen.E2E.Windows/WindowsTestBase.cs`, `ImageGen/ImageGen.E2E.Windows/VideoRecorder.cs`, `ImageGen/ImageGen.E2E.Windows/VideoRecorderTests.cs`, `ImageGen/ImageGen.E2E.Windows/FirstRunTests.cs` (временный тест), `docs/knowledge/ui-e2e.md`, `docs/knowledge/maui.md`.

### Проверка
```
dotnet build ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj
dotnet build ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj
dotnet format ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj --verify-no-changes
bash scripts/check.sh
# в GitHub Actions: ui-e2e зелёный; кадры конца роликов — по docs/knowledge/ui-e2e.md
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
Создано по просьбе владельца посмотреть тестовые прогоны: проблемы найдены разбором видео по кадрам. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут» и «на те проблемы, для которых нет задач, создай задачи».
