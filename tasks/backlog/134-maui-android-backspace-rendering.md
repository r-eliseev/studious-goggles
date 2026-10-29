---
id: 134
status: planned
assignee:
claimed:
created: 2026-10-29
---

# MAUI Android: Backspace должен сразу перерисовывать Editor

## Описание
Наблюдение владельца 2026-10-29: на Android Backspace, вероятно, меняет внутренний text, но UI продолжает показывать удалённые символы. Это отдельный IME/native rendering defect, не keyboard inset task095. Единственный Android-specific input code — `ChatEditorImeHandling` с `SetRawInputType` и event subscriptions; причина пока не подтверждена.

Зависит от: 095.
Среда: Android device/emulator с воспроизводящимся IME; код можно писать Linux/Windows, закрыть без ручной проверки нельзя.

## Критерии готовности
- Single/held Backspace visually and in model deletes correct graphemes; delete to empty no ghost text.
- Native EditText.Text, ChatEditor.Text, ChatPageModel.InputText and sent text agree.
- ASCII, selection, emoji/surrogate, composing suggestion tested; cursor preserved.
- Software keyboard владельца и hardware Backspace если доступен; device/Android/IME version в отчёте.
- IME Send, hardware Enter, Shift+Enter and reconnect/rotation regressions pass.
- No production logging of typed content; no polling/full-control forced redraw or manual substring Backspace workaround.

## Анализ
`SetRawInputType` can reset Android input connection/composing state; AppendToMapping may subscribe handlers repeatedly after reconnect. Это hypotheses only. Нужно compare plain MAUI Editor/no mapper and instrument locally without persisting text. Task095 affects layout only, dependency allows combined regression.

## План
Для Haiku/Luna: first reproduce. If no Android environment/device info, comment + blocked; no speculative code.

1. Record device model, Android, keyboard/IME, exact sequence/video. Verify sent/model text vs visible.
2. Temporary local diagnostics (not committed content logs): sequence native AfterTextChanged, MAUI TextChanged, InputText PropertyChanged, handler connect/disconnect; record lengths/selection/composing only, not text.
3. Minimal experiment plain ChatEditor without `ChatEditorImeHandling`, then mapper with ImeOptions only, then SetRawInputType. Identify first layer diverging.
4. Fix only confirmed cause. Preferred: preserve existing native InputType and set only ImeOptions; mapper registration idempotent; attach named handlers once and detach on handler change. Do not assign editor.Text on Backspace.
5. Extract pure helper only if actual logic exists; no fake test solely to satisfy TDD. Static source test checks no anonymous repeated subscriptions/SetRawInputType only if fix removes them.
6. Build Android and manually run matrix criteria; retest task095 inset and task050 Send/Enter.
7. Add confirmed MAUI/Android fact to docs/knowledge/maui.md, without user text.

### Проверка
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -c Release -f net10.0-android
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Maui/Platforms/Android/ChatEditorImeHandling.cs`, `docs/knowledge/maui.md`, `tasks/backlog/095-maui-android-keyboard-inset.md` (report cross-regression only). Если диагностика подтверждает handler lifecycle, также `ImageGen/ImageGen.Maui/Controls/ChatEditor.cs`; если появляется устойчивый source contract, новый `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatEditorAndroidSourceTests.cs`. Любой дополнительный файл сначала записать в комментарий с подтверждённой причиной.

## Отчёт

## Комментарии
