# 2026-10-30: аудит завершённых задач и планы исправлений

## Сделано

По поручению владельца проверены все **47 неархивированных done задач** на свежем origin/main `08a1a92` (merge PR51). Ранее архивированные исторические задачи заново не открывались. Сверены исходные критерии, одобренные планы, отчёты, code/test evidence; три read-only помощника проверяли независимые группы по docs/subagents.md, координатор перепроверил ключевые findings. Это аудит, production-код не изменён.

Подтверждена41 задача; они архивируются штатным scripts/archive-tasks.sh. Дополнительно архивируются три ранее cancelled задачи091/119/120, их отмена не выдаётся за реализацию. Шесть задач возвращены из done в blocked и оставлены на месте до конкретных follow-ups. Старые отчёты/комментарии не переписывались, текст архивированных задач сохраняется в git истории и путь записан в tasks/DONE.md.

### Матрица подтверждённых задач

Пути типов в таблице относительно ImageGen; детальные исходные критерии и исторические команды находятся в задачах на базовом коммите08a1a92.

| Задача | Вывод | Evidence в коде/тестах/документах |
|---|---|---|
| 090 | Подтверждена | SessionModelsPageModel tabs/selection; SessionModelsFlowTests sticky actions и обе оболочки. |
| 092 | Подтверждена | ModelVendors/group models; ModelVendorsTests и раскрытие/поиск SessionModelsPageModelTests. |
| 093 | Подтверждена | OpenRouter pricing parser, migration0009/prices_json, ImagePriceText; migration/roundtrip/headless row tests. |
| 094 | Подтверждена | POC Android csproj+реальные linked views, docs/knowledge/avalonia-mobile.md, proposed ADR0019; launch/TalkBack/performance честно переданы140, решение владельца не подменено. |
| 095 | Подтверждена | Все7 MAUI pages SafeAreaEdges=All; MauiSafeAreaTests; runtime клавиатуры в140. |
| 096 | Подтверждена | RunItemViewModel подписан на статусы/цены и пересчитывает header; RunItemViewModelTests и GeneratingCard_Done_UpdatesRunHeader. |
| 097 | Подтверждена | TranscriptFollower + lifecycle/manual-scroll guards; unit и Avalonia TranscriptScrollTests на длинную историю/рост/viewport. |
| 098 | Подтверждена | MAUI FlexLayout, Avalonia WrapPanel и width policy; markup/layout tests, ручная FlexLayout проверка140. |
| 100 | Подтверждена | ImageGenWindowsRid применяется только Windows TFM; release.yml проверяет PE Machine. GitHub release run41038127258 на441c03a success подтверждён read-only API; запуск ARM устройства не заявлен. |
| 101 | Подтверждена | FuzzyMatcher Levenshtein по префиксам L−1/L/L+1 и min4/same-first-letter; positive/negative/order tests; IME manual140. |
| 102 | Подтверждена | InMemoryQueuedImageProvider own CTS, atomic snapshots, strict handle,10min TTL и cancellation; соответствующие provider tests. |
| 103 | Подтверждена | New session MAUI semantic description и markup checks; interactive labels обеих оболочек сохранены. |
| 104 | Подтверждена | Пакет Avalonia.Fonts.Inter/WithInterFont отсутствует в props/csproj/cs. |
| 106 | Подтверждена | WatchQueuedAsync catch только MoveNext; ApplyUpdate снаружи; safe ApplyPollingFailure и provider recovery; GenerationServiceTests1661+. |
| 107 | Подтверждена | Все fal HTTP операции используют timeout до body read; FalImageProviderTests submit/probe/catalog/status/result/download/cancel/cancellation. |
| 108 | Подтверждена | GenerationService start leases, tombstone, shared stop task, own CTS, registration before execution и recovery; deterministic lifecycle tests. |
| 109 | Подтверждена | FalUrlValidator/validation до secret+HTTP, safe URI logs; validator и forged-handle tests; DNS/private-IP residual risk явно принят в исходном плане. |
| 110 | Подтверждена | AssistantRequestBudget и пересчёт history при image removal last-run→user; шесть image-budget boundary tests и estimator/factor checks. |
| 111 | Подтверждена | Один per-provider SemaphoreSlim(4,4), independent providers, native submit slot/local lifecycle; queued/recovery/cancelled-waiter tests. |
| 113 | Подтверждена | RunRepository affectedRows=1 и StorageWriteException; supervisor safe logs, no false events; file-save failure144 — новая политика вне original scope. |
| 114 | Подтверждена | AddUserWithAttachmentsAsync transaction insert+link, session/unlinked guards и rollback; Data tests invalid/missing/duplicate/cancel; Assistant атомарно принимает до provider. |
| 115 | Подтверждена | Manifest allowBackup=false + fullBackupContent/dataExtractionRules; обе XML schemas deny all9 domains без include, AndroidBackupPolicyTests. OEM D2D/runtime не заявлены проверенными. |
| 116 | Подтверждена | Нормативное Replicate заменено, historical mentions обозначены; проверен исторический0e14086 и дальнейшее built-in wording136. |
| 117 | Подтверждена | architecture/chat-sessions ссылаются на budget+summary C1–C4; fixed20 cap отсутствует в ContextBuilder,20 остаётся summary protection. |
| 118 | Подтверждена | README различает исходники/v0.1.4/roadmap, built-ins/live limits,9 packages, arm64 caveat, privacy и команды; относительные источники существуют. |
| 121 | Подтверждена | Stop ждёт submit settlement, remote cancel exactly once с None token до cleanup; Fal validation/timeout и native/local/recovery tests; best effort live не выдан за гарантию. |
| 122 | Подтверждена | SaveResultAndPersistAsync компенсирует только успешно возвращённый path при failed terminal DB persistence, notify вне catch; native/local/retry/cancel tests. Partial write прямо передан141. |
| 123 | Подтверждена | Отдельные accepted outcome/persist phases, caller cancellation, narrow attachment error, один terminal save; malformed tool validation и after-accept UI tests. |
| 124 | Подтверждена | Stable named fal transport AllowAutoRedirect=false; registry/custom-id redirect tests и DI Composition pipeline test. |
| 125 | Подтверждена | Три UPDATE attempts только transient SQLite5/6, два100ms injectable delays, event после success; cancellation/permanent/mapping tests. |
| 126 | Подтверждена | DI alias того же GenerationService singleton; delete stop→DB→files→notify с независимым token; integration ждёт transport и проверяет отсутствие resurrected files/rows. |
| 128 | Подтверждена | Исторический6be58ac подтверждает wrapper staging без queued interface; текущее capability включение сделано129, router cache/isolation tests. |
| 129 | Подтверждена | Единый queued dispatch, synchronous transport wrapper, persisted local handle/recovery Interrupted, native resume без submit, failure cancel once; service+provider tests. |
| 130 | Подтверждена | ADR0020 принят с ownership/TTL/staging; ADR0005 historical body сохранён, изменён только status/ссылка. |
| 132 | Подтверждена | Whitelist config formats, generic client knobs и configured media type; Together wire/probe/status tests, ADR0023; live paid result намеренно не заявлен. |
| 133 | Подтверждена | Metadata header reader с semaphore2, dimensions/GCD, epoch/session/card/path guards и task cache; Data/Presentation/Avalonia tests, оба XAML и manual016/046/140. |
| 136 | Подтверждена | Builtin Together4 exact model IDs, seed/base64/png/180s, no assistant; override/catalog/mocked generation/no-key tests и документы. |
| 137 | Подтверждена | Android Window-specific fullscreen leases и stop/resume/handler session; Avalonia меняет только viewer; headless controller/escape/missing tests, native runtime в046/140. |
| 138 | Подтверждена | Четыре characterisation tests card menu commands; ShellDialogService безопасные debug count/cancel и operational Warning без текста; исходный exception сохраняется. |
| 142 | Подтверждена | LC_ALL/LANG en_US.UTF-8 в harness/harness-audit/build Linux jobs; кириллический диапазон ранее устранён в source, current check проходит. |
| 143 | Подтверждена | SessionsListView.axaml использует PlaceholderText, прежний Watermark отсутствует. |

### Задачи, для которых done не подтверждён

| Исходная | Доказательство незавершённости | Fix tasks |
|---|---|---|
|089|План шаг1 требует isolation каждого subscriber, но SessionChangeNotifier.cs:8 вызывает один multicast Invoke; шаг2 требует notify после Create, но SessionService.cs:31–32 делает Add→return.|154,151|
|099|Критерий всех новых done между тегами: release-notes.sh:64 NR==FNR теряет current set при пустом previous. Воспроизведено в локальном fixture: v0.0.1 без done, v0.0.2 с001 done → ошибочно «Новых завершённых задач нет».|147|
|112|План шаг5 whitespace: оба DecodeImage (OpenRouter:186, compatible:100) считают raw length до base64 decode.49MiB wrapped CRLF может превышать guard при JSON<72MiB. Client limits matrix Fal/compatible урезана.|150,152|
|127|Logging tests: Information known response и oversized declared-header JSON/request есть, но нет обязательных unknown/dishonest length, Image/GeneratedImage boundaries, trace oversize, cancel body и больших valid request/replay cases. Runtime нарушение сверх150 не установлено.|152|
|131|Avalonia:188 и MAUI:181 откатывают index-1 при failed previous. Windows handler MAUI:125–133 обрабатывает лишь Escape. MAUI background tap:82–87 использует null GetPosition вместо попадания в bounds.|146,148,149|
|135|SettingsPage.xaml:27,33 задаёт MinimumHeightRequest44 вместо inherited Android48 (Common.xaml:35–39), нарушая platform targets из плана5.|153|

### Новые подробные планы

144 — terminal Failed при file-save error (новая политика вне113/122);145 — reference support/limit перед Other model;146 — viewer failed navigation и stale guards;147 — release notes empty previous set;148 — Windows-only arrows;149 — MAUI background hit-test;150 — bounded base64 whitespace;151 — notification after create;152 — недостающая HTTP verification matrix;153 — Android touch targets;154 — subscriber isolation.

Каждый план содержит воспроизведение/evidence, малый scope, точные production/test files, TDD scenarios, команды, критерии и ограничения среды. Статус planned: реализация ещё не начиналась и не считается одобренной. Порядок общих файлов:144→145;146→149→148;150→152;154→151. Циклов с удержанными родительскими задачами нет.

## Решения и причины

- Готовность оценивалась в исходном одобренном объёме, с учётом последующих задач: staging128 не объявлялся ошибкой из-за включения129, а file-store partial write122 не переоткрывалась вопреки её явной границе141.
- Отсутствие устройства, real paid API, Windows/macOS runtime или TalkBack само по себе не превращалось в incomplete: docs/tasks.md допускает done при выполненном коде и явных follow-ups016/046/140.
- Невыполненные обязательные code/test пункты не скрыты успешным общим test run. Поэтому127 удержана именно за verification gap; не приписываем ей неподтверждённый runtime баг.
- Изоляция notifier154 выделена при независимой проверке плана151: текущий API не имеет обещанной isolation, полагаться на неё нельзя.
- Новые задачи созданы штатным new-task.sh --publish в отдельной audit ветке; production/scripts/workflows не исправлялись.

## Не закончено

- Реализация144–154 и141 не входит в этот аудит. Шесть удержанных parent задач возвращать в done только после соответствующих fixes и исходных acceptance checks; затем можно архивировать.
- Runtime/ручные сценарии остаются016/046/140; live Together/Fal, OEM backup/transfer, macOS/iOS и Windows on ARM не подтверждены этим проходом.
- Proposed ADR0019/0022/0023 оставлены на решение владельца; их принятие не подменялось аудитом.

## На что обратить внимание

Проверки кода в текущей сессии на том же production tree (git diff f9e7510 origin/main -- ImageGen пуст): Core631/631, Presentation387/387, Avalonia147passed+2platform skips; повторный отдельный Data96/96. Первые параллельные Data сборки конфликтовали по общему PDB, затем был MSBuild/handshake сбой; последовательный штатный запуск прошёл. Новые планы требуют последовательных dotnet команд, большие boundary fixtures150 — также непараллельной xUnit collection.

Initial scripts/audit.sh/check.sh прошли. Штатный scripts/archive-tasks.sh перенёс44 файла (41done+3cancelled); итоговые scripts/check.sh и scripts/audit.sh после архивации прошли. Проверка списка git deletions подтвердила ровно44 ожидаемых архивных файла; неархивированных done больше нет. Кодовые тесты подтверждают существующий tree, не выполняют ещё не реализованные regression cases144–154.

GitHub harness run41082612012 на f9e7510 также success (read-only проверка CI для142). GitHub read-only проверка release run41038127258 на441c03a подтвердила success workflow_dispatch из отчёта100; workflow содержит PE-machine guards. Сам аудит не создавал релизов и не выполнял платных API запросов.
