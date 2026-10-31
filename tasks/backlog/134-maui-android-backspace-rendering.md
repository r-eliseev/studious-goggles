---
id: 134
status: approved
assignee:
claimed:
created: 2026-10-29
---

# MAUI Android: Backspace должен сразу перерисовывать Editor

## Описание
Наблюдение владельца 2026-10-29: на Android Backspace, вероятно, меняет внутренний text, но UI продолжает показывать удалённые символы. Это отдельный IME/native rendering defect, не keyboard inset task095. Единственный Android-specific input code — `ChatEditorImeHandling` с `SetRawInputType` и event subscriptions; причина пока не подтверждена.

Зависит от: —
Среда: Linux или Windows с Android SDK и подключённым Android-устройством/эмулятором, на котором воспроизводится дефект с нужным IME. До claim проверить доступность устройства; без него задачу не занимать, сообщить координатору. Обязательное воспроизведение нужно для выбора исправления; после него доступные проверки выполняются по docs/tasks.md, недоступные дополнительные устройства/IME не блокируют завершение.

## Критерии готовности
- Single/held Backspace visually and in model deletes correct graphemes; delete to empty no ghost text.
- Native EditText.Text, ChatEditor.Text, ChatPageModel.InputText and sent text agree.
- ASCII, selection, emoji/surrogate, composing suggestion tested; cursor preserved.
- Software keyboard владельца и hardware Backspace если доступен; device/Android/IME version в отчёте.
- IME Send, hardware Enter, Shift+Enter and reconnect/rotation regressions pass.
- No production logging of typed content; no polling/full-control forced redraw or manual substring Backspace workaround.

## Анализ
`SetRawInputType` can reset Android input connection/composing state; AppendToMapping may subscribe handlers repeatedly after reconnect. Это hypotheses only. Нужно compare plain MAUI Editor/no mapper and instrument locally without persisting text. 095 меняет только safe area в XAML и не является функциональным предшественником; Backspace можно исследовать независимо. Если 095 уже done, повторить inset regression по Android checklist140. После архивирования 095 не искать исходный файл и не восстанавливать его. 050 уже завершена: IME Send/Enter — обязательная регрессия этого исправления.

## План
Для Haiku/Luna: сначала воспроизвести, затем исправлять подтверждённый слой. До claim получить модель устройства, Android и имя/version IME. Если устройство исчезло после claim и дефект не удалось воспроизвести, записать это в комментарий и запросить среду у координатора; не менять код на основании гипотез. Если дефект воспроизведён и узкий fix проверен на исходном IME, остальные отсутствующие устройства/клавиатуры перечислить как непроверенные в 140.

1. Записать модель, Android/API, пакет/version IME, software/hardware input. Воспроизведение на заведомо несекретной строке: ASCII, выделение середины, emoji, composing suggestion → одиночный/удержанный Backspace → до пустого → Send. Сравнить длины/позиции каретки native EditText.Text, ChatEditor.Text, ChatPageModel.InputText и текста, переданного command. Видео только с синтетическим вводом, не включать ключи/чужие сессии.
2. Temporary local diagnostics (not committed content logs): sequence native AfterTextChanged, MAUI TextChanged, InputText PropertyChanged, handler connect/disconnect; record lengths/selection/composing only, not text.
3. Minimal experiment plain ChatEditor without `ChatEditorImeHandling`, then mapper with ImeOptions only, then SetRawInputType. Identify first layer diverging.
4. Исправить только причину, подтверждённую шагами 1–3. Если первая дивергенция в mapper: preserve existing native InputType and set only ImeOptions; Register вызывается один раз из MauiProgram; сами event handlers создаются для конкретного editor/PlatformView. Повторный mapper/reconnect не добавляет вторую подписку; старые EditorAction/KeyPress отписываются при HandlerChanging/Disconnect, новые подписываются один раз. Нужную lifecycle логику можно разместить в ChatEditor или отдельном Android handler helper по фактическому API; не держать статическую коллекцию редакторов. Do not assign editor.Text on Backspace.
5. Если появляется логика выбора IME flags/обработки Enter — выделить чистую функцию и добавить регрессионные tests: Send ровно один раз, Shift+Enter не отправляет, Backspace не перехватывается. Если меняется только native lifecycle/API — основная проверка это воспроизведение до/после на устройстве и Android build. Не добавлять string/source тест, который лишь повторяет новый код, и не имитировать native rendering тестом Presentation.
6. Build Android and manually run matrix criteria; retest task095 inset and task050 Send/Enter.
7. Подтверждённую причину и поддержанные Android/IME записать в docs/knowledge/maui.md без пользовательского текста. Передать в 140 регрессию Backspace/Send/rotation/inset со ссылкой на этот отчёт и список ещё непроверенных устройств. 095 не редактировать.
8. Заполнить «Отчёт» с воспроизведением до/после и командами проверки; завершить по docs/tasks.md, release-task.sh134 и scripts/check.sh. Локальную диагностику из шага 2 удалить перед коммитом.

### Проверка
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -c Release -f net10.0-android
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Maui/Platforms/Android/ChatEditorImeHandling.cs`, `docs/knowledge/maui.md`, задача ручной проверки Android140 (регрессия, без правки архивированной095). Если диагностика подтверждает handler lifecycle, также `ImageGen/ImageGen.Maui/Controls/ChatEditor.cs`; если выделяется тестируемая platform-neutral функция обработки клавиш — её файл и содержательные unit-тесты записать в «Файлы» перед изменением. Строковый тест исходника не обязателен и сам по себе дефект не проверяет. Любой дополнительный файл сначала записать в комментарий с подтверждённой причиной.

## Отчёт

## Комментарии

### 2026-10-29, codex-review
Задача нужна как подтверждённый пользователем Android input defect. 095 исключена из обязательных зависимостей: она меняет другую функциональность. До занятия требуется доступное воспроизведение; дополнительные ручные регрессии передаются140, архивированную095 не править.

### 2026-10-31, claude-audit
Повтор владельца 2026-10-31 на релизе v0.1.5: стирание символов по-прежнему не работает (Gboard, раскладка PL/EN). Устройство у владельца есть; модель и версии Android/Gboard ещё не записаны.
Гипотеза по коду (не проверена на устройстве): `ChatEditorImeHandling` подписывается на `field.KeyPress`, а в .NET for Android у `View.KeyEventArgs` для этого события `Handled` по умолчанию `true`. Обработчик ставит `Handled` только для Enter и не сбрасывает его для остальных клавиш, поэтому все остальные key events поглощаются. Буквы Gboard вводит через `InputConnection.commitText` (не key events), а Backspace без composing-текста часто отправляет как `KEYCODE_DEL` — он и теряется. Узкое исправление: в `KeyPress` для всех клавиш, кроме обрабатываемого Enter, явно `args.Handled = false`. Шаги 1–3 плана (воспроизведение и диагностику) может заменить проверка владельцем APK с этим исправлением, если владелец согласен; иначе план остаётся как есть.
