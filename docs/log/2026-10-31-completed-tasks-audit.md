# 2026-10-31: аудит завершённых задач и ревью документации

## Сделано

По поручению владельца: ревью соответствия документации и кода, затем проверка всех неархивированных done задач на `origin/main` `c6f8948` (merge PR #57). Production-код не менялся.

Проверка: `scripts/check.sh` и `scripts/audit.sh` проходят. Тесты в облачной сессии (.NET 10): Core 685/685, Data 96/96, Presentation 402/402, Avalonia 149 passed + 2 skipped (D-Bus и Windows-only). `bash scripts/tests/release-notes-empty-previous.sh` проходит. Четыре теста, которые в отчётах 151/153/154 падали на тогдашнем `main` (`RunOnModelAsync_*`, `Pick_TargetDoesNotSupportSourceReferences_*`), теперь проходят: проверку восстановил follow-up 145. Сборка MAUI (Android/Windows) здесь не запускалась: её проверяет CI.

### Подтверждённые задачи (архивированы `scripts/archive-tasks.sh`)

| Задача | Доказательство |
|---|---|
| 089 | `SessionsPageModel` подписан на `SessionChangeNotifier`; остаток закрыт 151 и 154; тесты `SessionChanged_*` в `SessionsPageModelTests`, включая `SessionChanged_NewRun_UpdatesRunCount`. |
| 099 | `scripts/release-notes.sh` различает первый файл по `FILENAME` (147); способ описан в `docs/knowledge/dev-environment.md`, «Релиз»; регрессия проходит. |
| 112 | Лимиты `ProviderResponseLimits` (8/72/50 MiB, request 72 MiB); `BoundedHttpContentReader`; whitespace base64 — 150, client matrix — 152. |
| 127 | `LoggingHttpHandler`: Information без чтения тел, Debug/trace через ограниченный reader, `SkipResponseBody` и проверка ключа без чтения; test matrix — 152. |
| 135 | `SettingsPageModel` перечисляет `providers.All`, `ApiKeyPageModel` валидирует provider id маршрута, Remove с предупреждением для OpenRouter; touch targets — 153. |
| 144 | `ErrorKind.ImageSaveFailed`, узкий catch `IOException`/`UnauthorizedAccessException` в `GenerationService.SaveResultAndPersistAsync`, текст в `AppResources.resx`. |
| 145 | `GenerationService.RunOnModelAsync` проверяет поддержку и число референсов до создания прогона. |
| 146 | `Presentation/Services/ImageViewerNavigationState` и тесты; используется в Avalonia и MAUI viewer. |
| 147 | См. 099; `scripts/tests/release-notes-empty-previous.sh`. |
| 149 | `Presentation/Services/ImageViewerHitTest` (AspectFit, неизвестная геометрия) и тесты; используется `MauiImageViewerService`. |
| 150 | `BoundedBase64ImageDecoder` вызывают `OpenRouterClient` и `OpenAiCompatibleClient`. |
| 151 | `SessionService.CreateAsync` вызывает `changes.Notify` после `AddAsync`; тесты в `SessionServiceTests`. |
| 152 | Тесты границ `LoggingHttpHandler`, Fal и OpenAI-compatible oversize/cancellation; Core 685/685. |
| 153 | `SettingsPage.xaml`: у Change/Remove нет локального `MinimumHeightRequest`; проверка в `MauiProviderKeysMarkupTests`. Устройство — ручная 140. |
| 154 | `SessionChangeNotifier.Notify` вызывает каждый обработчик в своём try/catch, Warning без исключения. |

155 — пустая заготовка с именем выполненной 145, создана `new-task.sh --publish` (коммит 6a27422): отменена и архивирована.

### Новые задачи
- 156 — выбор модели ассистента только из OpenRouter против ADR 0009 и `docs/architecture.md`; ждёт решения владельца.
- 157 — 24 обработчика в PageModels показывают `ex.Message` пользователю вопреки правилу «ядро не формирует тексты интерфейса» (пример — отказ 145 на английском).
- 158 — правки документации по итогам ревью: `docs/status.md`, `docs/architecture.md`, `docs/conventions.md`, `docs/index.md`, `dev-environment.md`, `debug-trace.md`, `image-client.md`, `chat-sessions.md`; статус ADR 0022/0023.

## Решения и причины
- Документацию в этом PR не исправлял, кроме записи итога в `docs/status.md`: владелец попросил оформить расхождения задачами (158).
- Новые задачи созданы без `--publish` и заполнены до коммита, чтобы не оставлять пустых заготовок, как 155.

## Не закончено
- Ручные проверки устройств остаются в 016, 046, 140; 131 остаётся blocked до Windows-only 148.

## На что обратить внимание
- В отчётах 099 и 112 нет финального комментария о закрытии остатка: статус вернули в done коммиты 1ac6c8d (147) и 615e2ba (150–152). Сверено по этим коммитам и тестам.
- Вопросы владельцу добавлены в `docs/status.md`, «Открытые вопросы»: модель ассистента (156) и ADR 0022/0023.
