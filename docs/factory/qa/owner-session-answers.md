# Ответы на вопросы рабочей сессии (the first five session questions)

Вопросы дословно: `docs/factory/sources/owner-session-questions.md`. Ответы опираются на основной диалог (`owner-dialog`, вопросы all 43 questions of the owner-dialog и их разбор в `qa/owner-dialog-walkthrough.md`), `tsf-lesson`, 02 и 04 и новые чтения первоисточников (метки как в прохождении: **✔** прочитано на странице, **◐** сниппет или пересказ, **✖** исправление, **[синтез]** авторский вывод без внешнего источника). Принцип `document-everything`: всё документируется, даже если не применимо к текущему харнесу (`docs/factory/README.md`).
Живое определение, которое эти ответы уточняют: `docs/factory/definition.md`. Параллели с организациями из людей: `docs/factory/human-org-parallels.md`. Перекрёстные ссылки на другие вопросы стоят в каждом ответе.

## Исправление к прохождению (нашёл при подготовке)

В `owner-dialog-walkthrough.md` я сначала написал, что число «48%» (ускорение при работе с Copilot на незнакомом коде) не подтверждается. Это моя ошибка: помощники искали по сниппетам. В первой версии статьи Qiao и др. ✔ ([arXiv:2511.02922v1](https://arxiv.org/abs/2511.02922v1)): «participants took 48.2% less time on average to complete Task 1 with Copilot (M=678.9 seconds, SD=477.9) than without it (M=1311.6 seconds, SD=468.3)», p=0.001. Прохождение исправлено (ответы [`keeping-control`](owner-dialog-walkthrough.md#keeping-control) и таблица главных исправлений).

---

<a id="vs-classical-delegation"></a>

## `vs-classical-delegation`. Чем это отличается от классической схемы «топы задают намерение, простые исполнители приводят его в действие»?

**Вопрос владельца (дословно):** «чем это отличается от клаической схемы, где топы задают намериние, а простоые исполнители приводят его в действие?»
**Фрагмент, на который ссылается владелец:** «Под этим сейчас обычно понимают повторяемую систему поставки: агенты выполняют координированную работу, а человек задаёт намерение, ограничения, критерии приёмки и правила одобрения».

**Короткий ответ.** Структура та же (делегирование намерения). Отличается не схема, а **свойства исполнителя и стоимость**: они меняют, где и чем приходится заменять доверие и общение. Агенты выполняют и «среднее звено» (планирование, ревью, проверку), а не только «простое исполнение».

**1. Это та же схема, и у неё есть название.** В военной теории командования это *mission command* ✔ ([Wikipedia](https://en.wikipedia.org/wiki/Mission_command)): стиль, который «combines centralized intent with decentralized execution subsidiarity, and promotes freedom and speed of action, and initiative within defined constraints. Subordinates, understanding the commander's intentions, their own missions, and the context of those missions, are told what effect they are to achieve and the reason that it needs to be achieved. Subordinates then decide within their delegated freedom of action how best to achieve their missions.» Приказ содержит намерение, меры контроля и цели. Это в точности «намерение, ограничения, критерии приёмки, правила одобрения». Доктрина НАТО AJP-01 (по той же странице ✔): mission command держится на двух принципах, **доверии и взаимном понимании**; «a commander's trust in subordinates is the key determinant of the autonomy they will be granted. This trust, which must be earned and sustained, includes tolerance for mistakes». В экономике это *принципал–агентская проблема* (конфликт, когда одно лицо действует от имени другого) ✔ ([Wikipedia](https://en.wikipedia.org/wiki/Principal%E2%80%93agent_problem)).
*Связь:* автономность, которая выдаётся по мере заслуженного доверия, это тот же «режим по действиям» (`mode-per-action` в `owner-notes.md`, `mode-per-action` в каталоге) и ваша мысль, что уровень автономности принадлежит области, а не человеку ([`move-self-and-others-up`](owner-dialog-walkthrough.md#move-self-and-others-up), [`level-4-to-5-details`](owner-dialog-walkthrough.md#level-4-to-5-details)).

**2. Что действительно иначе** (свойства исполнителя; метки у каждого пункта):

| Свойство | Люди-исполнители | Агенты | Что меняется в устройстве системы |
|---|---|---|---|
| Можно переспросить | «What did you mean by that?» нормальная стратегия, быстрое сообщение в Slack | В автономном прогоне переспросить нельзя без специального механизма: Infralovers ✔: «"What did you mean by that?" is a valid clarification strategy with humans. With agents, it isn't. The spec must be complete.» ([текст](https://www.infralovers.com/blog/2026-02-22-architektur-patterns-dark-factory/)). ✖ Уточнение: в интерактивном режиме агент спрашивает; в автономном вопрос это приостановка (deferred tool call, `always-on-agents` ✔), ответ приходит как событие | спека должна быть полнее, либо нужны точки остановки |
| Неявный контекст | накоплен годами, часть «в головах» | нет, пока не записан; память между запусками только та, что записали (`always-on-agents` ✔) | записывать ограничения и причины ([`tacit-knowledge-extraction`](owner-dialog-walkthrough.md#tacit-knowledge-extraction), [`conflicting-sources-how-humans-resolve`](owner-dialog-walkthrough.md#conflicting-sources-how-humans-resolve)) |
| Скорость и цена | дорого, медленно, ограничено числом людей | StrongDM ✔: клон крупного SaaS «was always possible, but never economically feasible», теперь «routine»; ориентир $1000 в день на инженера. Параллельность почти неограниченна | узкое место смещается на ревью: Шапиро ✔ про уровень 3: «Your life is diffs» ([пост](https://www.danshapiro.com/blog/2026/01/the-five-levels-from-spicy-autocomplete-to-the-software-factory/)) |
| Характер ошибок | заметные и «громкие» чаще; у людей есть «нюх» | тихие, уверенные; агент не знает, чего не знает ([`remember-quiet-vs-loud-failure`](owner-dialog-walkthrough.md#remember-quiet-vs-loud-failure), [`humans-have-it-too`](owner-dialog-walkthrough.md#humans-have-it-too)) | проверки должны быть механическими и независимыми |
| Накручивание метрик | люди тоже подгоняют показатели (закон Гудхарта ✔: «When a measure becomes a target, it ceases to be a good measure», [Wikipedia](https://en.wikipedia.org/wiki/Goodhart%27s_law)) | StrongDM ✔: агент писал `return true`; METR ◐: правит тесты и код оценки | проверка, скрытая от исполнителя ([`what-is-holdout`](owner-dialog-walkthrough.md#what-is-holdout), [`cannot-fit-claim-too-strong`](owner-dialog-walkthrough.md#cannot-fit-claim-too-strong)) |
| Ответственность | исполнитель отвечает карьерой и репутацией | у агента нет ставок; отвечает человек-владелец | вопрос владельца «если меня спросят, почему так, я должен знать» ([`keeping-control`](owner-dialog-walkthrough.md#keeping-control)) |
| Независимость ошибок | люди разных школ ошибаются по-разному, но не всегда | копии одной модели ошибаются согласованно (Kim и др. ◐) | независимость источника проверки, а не «ещё один агент» ([`other-ways-to-independence`](owner-dialog-walkthrough.md#other-ways-to-independence)) |
| Недетерминизм | один и тот же человек обычно стабилен | один вход даёт разные результаты | статистика по многим запускам ([`graded-satisfaction-populism`](owner-dialog-walkthrough.md#graded-satisfaction-populism)) |

**3. Агенты берут на себя не только «исполнение».** В схеме «топы → исполнители» есть ещё середина: менеджеры, архитекторы, ревьюеры, QA. В фабрике они тоже агенты, человек остаётся на «точках касания» (`warp-talk`, `lifecycle-loop`: ревью спеки, кода и продукта; см. `human-points-in-the-loop`). Поэтому точнее сравнивать не с «топ + исполнители», а со **всей иерархией разом**, где автоматизирован почти весь контур между намерением и результатом, а сам контур стал на порядки быстрее и дешевле.

**4. Что остаётся тем же.** Две классические задачи делегирования: как донести намерение и как убедиться, что результат ему соответствует. Это ваш пункт `same-question-for-human-orgs`; различие только в том, чем они решаются (механизмами вместо отношений доверия).

*См. также:* [`old-vs-new-compare`](owner-dialog-walkthrough.md#old-vs-new-compare) и [`old-new-same-axis-objection`](owner-dialog-walkthrough.md#old-new-same-axis-objection) (старая и новая фабрика: одна ось жёсткости описания), [`move-self-and-others-up`](owner-dialog-walkthrough.md#move-self-and-others-up), [`level-4-to-5-details`](owner-dialog-walkthrough.md#level-4-to-5-details), [`humans-have-it-too`](owner-dialog-walkthrough.md#humans-have-it-too), [`humans-limited-window-too`](owner-dialog-walkthrough.md#humans-limited-window-too), [`keeping-control`](owner-dialog-walkthrough.md#keeping-control); определение v1: `definition.md`; параллели: `human-org-parallels.md`.

---

<a id="human-points-in-the-loop"></a>

## `human-points-in-the-loop`. Где в потоке человек (от начала до конца)

**Вопрос владельца:** «мне не понятно, почему человек указан как участвующий в 3 точках алгоритма/флоу, но в тексте была только одна явная позиция. все остальное домыслы. просто разверни идею, где там человек. не давай дельту, опиши от начала до конца». Предыдущая версия вопроса: «видно только одну точку. 2 остальных где-то в районе идей? почему агенты не могут их генерировать?»

**Короткий ответ.** Владелец прав в упрёке к моему тексту: я написал «человек вмешивается на трёх точках», но показал одну, и число «три» моё: докладчик его не называет, он говорит «these little blue boxes» (слайд есть, в расшифровке его нет) и по ходу рассказа трижды упоминает человека. Ниже поток целиком, от входа до обратной связи, сначала по докладу Warp (с цитатами и отметкой, что сказано, а что нет), потом то же самое на живом примере этого репозитория (`living-example`, `case-study-imagegen.md`).

### Часть 1. Поток по докладу Warp (источник `warp-talk`), от начала до конца

Единый абзац докладчика, дословно: «ideas are going to come in at the top. Agents are going to do triage. If something is complicated, they will write a spec. Uh these little blue boxes are where humans step in. **Humans will review the spec.** Uh agents will do the implementation. **A human and agent will review the code.** Agents will verify. **Human will review the product.** You ship and then you monitor and round you go.»

| Этап | Кто, по докладу | Что сказано | Что **не** сказано |
|---|---|---|---|
| Вход (идеи) | команда, пользователи, системы мониторинга | «the inputs are really ideas... could be coming from your team, they could be coming from your users»; каналы: трекер задач, Slack и другие, терминал или IDE, «your monitoring systems» | кто решает, какая идея заслуживает работы; сгенерированы ли идеи агентами кроме как из мониторинга |
| Triage | **агент** | «you want an agent that is looking at issues as they come in and... if this is easy and this is unambiguous, just implement it» | утверждает ли человек решение triage |
| Спека (для сложного) | **агент** пишет; **человек проверяет** | «if an issue is hard, I recommend having an agent that produces specs»: product spec (инварианты продукта) и tech spec (архитектура и форма кода). Явно: «**Humans will review the spec**» | как человека зовут и как быстро он должен ответить |
| Реализация | **агент** | кодирующий агент в облаке делает diff | нет упоминания человека |
| Ревью кода | **агент сначала, затем человек вместе с агентом** | «I would have an agent do code review first and then it becomes over time like a risk-management exercise of like when do you bring in humans to do code review. But you want to have a step in here where humans can do it. This is a very important step for certain types of apps.» Явно: «**A human and agent will review the code**» | как часто и по какому правилу риска; при каких приложениях шаг важен |
| Верификация | **агент** | computer use (агент работает с продуктом), видео и скриншоты; CI/CD | нет упоминания человека |
| Ревью продукта | **человек** | «**Human will review the product**» | что именно человек смотрит, по каким критериям |
| Выпуск и мониторинг | **агенты** | «agents don't stop in your factory when code is shipped. They should observe what's been shipped. Is it crashing? Is it being used?»; вывод мониторинга подаётся обратно «into the top of the factory» | кто решает, что делать с наблюдением |

Отдельно докладчик говорит о человеке не как о стадии, а как о свойстве системы: среди компонентов есть «a way of bringing humans in at the correct time. So sort of like when things get stuck on the factory», и в ответе на вопрос о вкусе: «human taste, human input, human product sense, um humans like guiding at those touch points where you can't automate stuff is absolutely like essential».

**Итог части 1.** Явных упоминаний человека в потоке три: ревью спеки, ревью кода (вместе с агентом), ревью продукта. Всё остальное докладчик отдаёт агентам (triage, реализация, верификация, мониторинг). Идеи на входе приходят от людей и от систем мониторинга; сказано только это. Ответ на «почему агенты не могут генерировать идеи»: в докладе такого запрета нет, идеи из мониторинга уже идут от систем; а кто решает, какую идею взять в работу, в докладе не сказано. Это решение дизайна, а не факт из источника.

### Часть 2. Тот же поток на живом примере: репозиторий ImageGen

Те же стадии, как они реально работают здесь (факты из репозитория, подробности и цифры в `case-study-imagegen.md`).

1. **Намерение (человек).** Владелец записывает цели, границы и решения в `docs/product.md` (началось с его рукописных заметок).
2. **Идеи и работа (люди и агенты).** Основные продуктовые идеи даёт владелец (например чат с ассистентом, добавление генерации звука позже). Агенты порождают рабочие единицы сами: правило харнеса требует, чтобы находка вне задачи становилась новой задачей с фактами и предложенным планом, и так появляются, например, задачи на диагностику нестабильных тестов. То есть и здесь есть генерация «идей» агентами, но на уровне задач-находок, а не продуктовых целей.
3. **Спека и план (агент-планировщик).** Пишет спецификации, ADR, сценарии и задачи с анализом и планом.
4. **Утверждение плана (человек, делегировано заранее).** По докладу Warp это «ревью спеки». Здесь владелец дал постоянную инструкцию: «одобряй все задачи, которые появятся» (2026-11-07), и каждая задача получает запись об утверждении по этой инструкции. Человек не смотрит каждый план, но задаёт правила и решает вопросы по существу.
5. **Реализация (агент-исполнитель).** Берёт одну одобренную задачу, делает ветку, пишет тесты, открывает pull request.
6. **Проверка (CI и тесты).** Сборки и тесты на Linux, Windows, Android, iOS-симуляторе; сценарии приёмки на Gherkin.
7. **Ревью (агент-планировщик).** Сверяет работу с планом и критериями и закрывает задачу. **Человек кода не читает** (слова владельца), то есть «ревью кода человеком» в потоке отсутствует; постоянная инструкция 2026-11-17 разрешила исполнителю даже самому закрывать свои задачи.
8. **Ревью продукта (человек).** Ручные проверки у экрана, оформленные задачами (MAUI на Windows, Avalonia на Linux и Windows, Android), и решения по открытым вопросам из `docs/status.md` (например Apple CI, живые платные проверки провайдеров).
9. **Обратная связь.** Сбои CI и находки исполнителей превращаются в новые задачи. Мониторинга развёрнутого продукта нет.

**Где человек, в одной строке:** задаёт намерение и решения (шаг 1), делегирует утверждения правилами заранее (шаг 4), проверяет продукт у экрана и решает открытые вопросы (шаг 8). На шагах 2–3 и 5–7 его нет.

### Как части 1 и 2 соотносятся

| Точка у Warp | У Warp | В ImageGen |
|---|---|---|
| Ревью спеки | человек | заменено постоянной инструкцией «одобряй всё» плюс решения владельца на уровне целей |
| Ревью кода | агент, затем человек | только агент-планировщик и CI; человека нет |
| Ревью продукта | человек | ручные проверки и решения по открытым вопросам |
Это показывает, что «три точки» не закон, а выбор: каждая может быть оставлена человеку, заменена правилом или отдана агенту; в ImageGen две из трёх изменены (режим по действию: `mode-per-action`).

### Про «идеи» (что осталось от вашего вопроса)

Предыдущая версия моего ответа предполагала, что вы имели в виду, будто остальные две точки лежат «рядом с идеями», и объясняла их как «намерение». Теперь ясно, что вопрос был про другое: в тексте была показана одна позиция, остальные казались домыслом. Если я снова понял неверно, поправьте. Вопрос про генерацию идей агентами остаётся содержательным: в Warp их генерирует мониторинг, в ImageGen агенты генерируют задачи-находки, а продуктовые идеи даёт человек.

*См. также:* `vs-classical-delegation`, `reality-as-judge`, `how-factories-work`, `move-self-and-others-up`, `keeping-control`, `code-review-unavoidable`; каталог `lifecycle-loop`, `triage-agent`, `decision-points`; `case-study-imagegen.md`.

<a id="outbox-explained"></a>

## `outbox-explained`. Что такое outbox?

**Вопрос владельца:** «понятно почти все кроме аутбокса. что это?»
**Фрагмент:** «…Вокруг цикла пять примитивов: inbox событий, таймеры, память, права, outbox.»

**Простыми словами.** Inbox это «входящие» агента: куда складываются события (сообщение пользователя, вебхук, таймер). **Outbox это «исходящие»**: куда агент кладёт то, что нужно сообщить человеку (итог работы, вопрос, предупреждение). Отдельный служебный процесс (**dispatcher**) забирает оттуда сообщения и решает, что и когда доставить. Это зеркало inbox.

**Дословно из `always-on-agents`** ✔ ([статья](https://claude.com/fr/resources/articles/building-always-on-agents)):
- Почему нужен другой подход, чем «поток в чате»: «With most AI agents, the response is streamed to the user as it's generated... Always-on agents, on the other hand, do most of their work in the background. The user gets the outcome and a few details, rather than the entire step-by-step breakdown.»
- Прогон может закончиться тремя способами: «The agent sends a message to the outbox, The agent decides that nothing needs to be sent, or The agent uses a deferred tool call to request permission or additional input.»
- Когда формальный outbox нужен: «Most always-on agents can get by with a "send" tool that delivers the message directly to the user. You only need a formal outbox when you want to pace your agent's messages or allow your agent to recover from errors.»
- Устройство: «The outbox is a separate table of outgoing messages, analogous to the event inbox. There's also a dispatcher, usually a background service, that's analogous to the worker. The dispatcher reads the pending messages, decides what to deliver, and sends those to the user.»
- Главная тонкость: «it's important to tune how many messages the agent sends. An agent that sends too many becomes noisy, while an agent that doesn't send enough misses things that the user needs to know. We recommend building evals to find the right balance. In practice, it's better for the agent to start quiet and then earn more of the user's attention over time.»

**Зачем отдельная таблица, а не просто «отправить»** [синтез по тексту]: (1) **темп**: объединять несколько сообщений, не заваливать человека; (2) **надёжность**: если доставка упала, сообщение не потеряно и его можно повторить; (3) **решение, что именно сказать**: диспетчер может отбросить повтор или собрать сводку; (4) **вопросы к человеку** (deferred tool call) тоже идут этим каналом, а ответ возвращается событием в inbox.

**Что это значит для фабрики.** Внимание человека самый дорогой ресурс; outbox это механизм, которым «останавливаться только на точках решения» (`tsf-lesson`, «near the loop») превращается в измеримое правило: сколько сообщений, каких и когда. Метрики для этого: минуты владельца на смерженную задачу, возвраты, ожидание одобрения (`attention-metrics`; `efficiency-metrics`, `notification-volume-eval` в каталоге). В `always-on-agents` тюнинг проверяется evals.

**Как это выглядит в этом репозитории сегодня (гипотеза, не проверено на скриптах; задача 291):** формального outbox нет; роль «исходящих» играют отчёты задач (Report, Comments), описания PR и ответы агента в чате. Темпа и порога «что стоит сообщить» нет. Для универсальной системы это отдельный элемент каталога (`outbox-and-dispatcher`).

*См. также:* [`keeping-control`](owner-dialog-walkthrough.md#keeping-control) (контроль и понимание: что видит владелец), [`restore-understanding`](owner-dialog-walkthrough.md#restore-understanding) (понимание: сводки к изменениям), [`remember-pre-mortem`](owner-dialog-walkthrough.md#remember-pre-mortem) и [`remember-assumptions-list`](owner-dialog-walkthrough.md#remember-assumptions-list) (допущения и pre-mortem как обязательные элементы отчёта), `human-points-in-the-loop` (точки участия человека), `always-on-agents` и `notes/always-on-agents.md` (`outbox`), задача 291.

---

<a id="same-question-for-human-orgs"></a>

## `same-question-for-human-orgs`. Это вопрос «что должна делать система и как убедиться, что она это делает», он же верен для организаций из людей

**Вопрос владельца:** «это похоже на вопрос в целом, как понять что сисетма должна делать и как убедиться что она делает именно это. вопросы справедливы и для организый только из людей без ИИ»
**Фрагмент:** «Уиллисон формулирует главный вопрос так: как доказать, что код работает, если и реализацию, и тесты пишут агенты».

**Вы правы, это классическая пара.** В программной инженерии она называется **верификация и валидация**; Боэм сформулировал её так ✔ ([Wikipedia: Software verification and validation](https://en.wikipedia.org/wiki/Software_verification_and_validation), ссылка на Boehm, IEEE Software 1(1), 1984): «**Verification: Are we building the product right? Validation: Are we building the right product?**» Там же: «"Building the product right" checks that the specifications are correctly implemented by the system while "building the right product" refers back to the user's needs.» Вопрос Уиллисона это верификация (код соответствует спеке и тесты это доказывают), а ваша формулировка «как понять, что система должна делать» это валидация (спека соответствует нужде). Так что в диалоге они смешаны: исходный разбор про «holdout, оракул, арбитр» это про оба сразу.
- Проблема оракула (откуда мы знаем правильный ответ) старше агентов: Barr и др., IEEE TSE 2015 ◐ ([UCL](https://discovery.ucl.ac.uk/id/eprint/1471263)): «the final source of test oracle information remains the human».
- Накручивание метрик это закон Гудхарта/Кэмпбелла ✔ ([Wikipedia](https://en.wikipedia.org/wiki/Goodhart%27s_law)): «Campbell's law: "The more any quantitative social indicator is used for social decision-making, the more subject it will be to corruption pressures"». Люди делают то же самое.
- Коррелированные ошибки известны на людях: эксперимент Knight и Leveson 1986 ◐ (27 версий, написанных **студентами**, модель независимости отвергнута на 99%) ([NASA NTRS](https://ntrs.nasa.gov/api/citations/19870002808/downloads/19870002808.pdf)). То есть «независимо написанные версии ошибаются по-разному» не выполняется и среди людей.

**Какие решения давно есть в организациях из людей** (а фабрика воспроизводит их механизмами): таблица в `human-org-parallels.md`. Основные: независимое тестирование и приёмка заказчиком, правило двух человек ✔ ([Wikipedia: Two-person rule](https://en.wikipedia.org/wiki/Four-eyes_principle): «access and actions require the presence of two or more authorized people at all times»), V-модель, ревью дизайна, разбор инцидентов без поиска виновных, pre-mortem.

**Что нового именно из-за ИИ** (то, что вы можете считать «различием», а не сутью; ссылки на `vs-classical-delegation`): стоимость и скорость (ревью становится узким местом), отсутствие накопленного неявного контекста и возможности переспросить в автономном режиме, тихие уверенные ошибки, отсутствие ставок у исполнителя, согласованные ошибки копий одной модели. Отсюда сдвиг: проверки и границы должны быть **механизмами**, а не отношениями.

**Вывод [синтез].** Определение фабрики (v1, `definition.md`) нужно строить как *способ организовать делегирование и верификацию*, одинаково применимый к людям, смешанным командам и агентам; ИИ меняет экономику и свойства исполнителя, а не сами вопросы. Это согласуется с вашей же линией в диалоге: «у людей то же» ([`humans-have-it-too`](owner-dialog-walkthrough.md#humans-have-it-too), [`humans-limited-window-too`](owner-dialog-walkthrough.md#humans-limited-window-too)), «разве агенты не могут менеджить контекст» ([`agents-manage-context`](owner-dialog-walkthrough.md#agents-manage-context)), «это буквально мок» ([`twin-literally-mock`](owner-dialog-walkthrough.md#twin-literally-mock)).

*См. также:* [`old-new-same-axis-objection`](owner-dialog-walkthrough.md#old-new-same-axis-objection) (корень риска общий: расхождение между намерением и результатом), [`conflicting-sources-how-humans-resolve`](owner-dialog-walkthrough.md#conflicting-sources-how-humans-resolve) (конфликтующие источники), [`keeping-control`](owner-dialog-walkthrough.md#keeping-control), [`cannot-fit-claim-too-strong`](owner-dialog-walkthrough.md#cannot-fit-claim-too-strong) (арбитр и оракул), [`code-review-unavoidable`](owner-dialog-walkthrough.md#code-review-unavoidable) (от код-ревью нельзя убежать), [`remember-quiet-vs-loud-failure`](owner-dialog-walkthrough.md#remember-quiet-vs-loud-failure) (тихий и громкий отказ), `vs-classical-delegation`, `human-org-parallels.md`.

---

<a id="definition-and-method"></a>

## `definition-and-method`. Определение всё ещё непонятно; метод работы

**Вопрос владельца:** «определение в целом еще не понятно. давай будем идти таким путем: по мере продвижения отвечай на мои вопросы, как и раньше используя данные из диалога и новых исслдований, перелинковывай это с другими моими вопросами. все дожно быть в репо»

**Метод (принят, действует с этого момента):**
1. Каждый новый вопрос владельца записывается дословно в `sources/owner-session-questions.md` (`weak-sample-48-percent`, `mission-command-thread`, …) вместе с процитированным фрагментом.
2. Ответ ложится в `qa/owner-session-answers.md`: короткий ответ, подтверждённые факты с метками проверки, перекрёстные ссылки на вопросы M.. и W.. и на каталог, заметки и задачи.
3. Первоисточники читаются (в этой сессии `curl` из Bash работает для большинства хостов), результаты исправляют прежние утверждения явно (как с «48%» выше).
4. Определение живёт в `docs/factory/definition.md`, версионируется и уточняется по мере ответов; открытые места перечислены в нём.
5. Обратные ссылки: в прохождении (`owner-dialog-walkthrough.md`) у связанных вопросов стоят строки «См. также: W..».

**Определение v1** (полный текст, компоненты, пример и открытые места: `docs/factory/definition.md`):
> **Программная фабрика** это повторяемая организация работы, в которой человек задаёт намерение и правила приёмки, а ИИ-агенты выполняют всё между намерением и проверенным результатом (планирование, сборку, ревью, проверку, выпуск); проверка результата независима от тех, кто его произвёл, границы действий исполняются системой, а то, что происходит после выпуска, возвращается на вход. Человек вызывается только в определённых точках решения.

Чем «фабрика» отличается от «агента»: фабрика это **процесс** (стадии, хранилища, границы, измеряемый выход), агент это **исполнитель** внутри стадии. Чем отличается от конвейера CI: в фабрике на стадиях решения принимает агент, а не детерминированный скрипт. Чем отличается от классической фабрики Greenfield и Short (2004): та генерирует детерминированно из модели ([`old-vs-new-compare`](owner-dialog-walkthrough.md#old-vs-new-compare), [`old-new-same-axis-objection`](owner-dialog-walkthrough.md#old-new-same-axis-objection)).

*Что ещё неясно в определении, нужно ваше слово:* см. раздел «Открытые места» в `definition.md`.

---

<a id="weak-sample-48-percent"></a>

## `weak-sample-48-percent`. Выборка 18 непредставительна, данные «такие себе»

**Вопрос владельца:** «я не до конца понимаю о чем речь, но пусть будет. 18 кажется вообще не репрезентативной выборкой. они скорее всего еще и с одного универа. это такие себе данные»
**Фрагмент:** моё исправление о числе «48%» (Qiao и др., 18 аспирантов).

**Короткий ответ.** Вы правы по обоим пунктам, и даже сильнее. Проверка по тексту статьи ✔ ([arXiv:2511.02922v1](https://arxiv.org/abs/2511.02922v1)):
- Все четыре автора из **Oregon State University** (Corvallis).
- Участники набраны из **одного** полностью онлайн-курса по методам программной инженерии для магистрантов (44 записанных студента) в «large research university in the western United States»; **26 согласились участвовать, 8 исключены** за несоблюдение протокола; остались 18.
- Задания были **частью обязательного домашнего задания курса**, участие добровольное.
- Это «replication and extension» предыдущего исследования тех же авторов на «high-achieving undergraduate students».
- Это препринт; версии v2 и v3 пересмотрели выборку (15 участников) и заголовок.
- Процент касается **времени одной задачи** (Task 1): 48.2% меньше (678.9 с против 1311.6 с, p=0.001, r=0.844).
То есть: один курс, один университет, добровольцы, студенты, одна задача, препринт. Для вывода о командах профессиональных разработчиков это слабые данные.

**Что я делаю с этим.**
1. Завёл `docs/factory/evidence-register.md`: реестр всех эмпирических источников с выборкой, популяцией, статусом публикации, оговорками и **оценкой веса** (strong, moderate, weak, anecdote). Qiao получила **weak**. Принцип `verification-labels-and-evidence` дополнен (README).
2. Поправил прохождение по 43 вопросам: число остаётся подтверждённым как факт статьи, но с оговоркой о слабости.
3. Честный вывод по теме [синтез]: тезис «ИИ ускоряет, но понимание не растёт» сегодня держится на трёх небольших исследованиях разных популяций (Qiao: weak; Anthropic: 52 в основном младших инженеров, изучавших новую библиотеку, moderate-weak; METR: 16 опытных разработчиков, 246 задач, moderate). Это правдоподобная гипотеза, а не установленный эффект; решения дизайна нельзя строить на одном таком источнике.
4. Это согласуется с вашей же линией: «нет гарантий, что опыт переносим» ([`pilot-not-transferable`](owner-dialog-walkthrough.md#pilot-not-transferable)) и с мета-оптимизацией: фабрика должна собирать **собственные** показатели (`attention-metrics`), а не опираться на чужие выборки.

*См. также:* [`keeping-control`](owner-dialog-walkthrough.md#keeping-control), [`pilot-not-transferable`](owner-dialog-walkthrough.md#pilot-not-transferable), [`restore-understanding`](owner-dialog-walkthrough.md#restore-understanding); `evidence-register.md`; тема `control-understanding-review` прохождения.

---

<a id="mission-command-thread"></a>

## `mission-command-thread`. Mission command и принципал–агентская проблема: «вернёмся к этим концептам»

**Вопрос владельца:** «ок, это интереный поворот. мы вернемся к этим концептам»
**Фрагмент:** связь схемы фабрики с mission command и принципал–агентской проблемой.

**Принято, тема отложена и записана** (`open-threads.md`, `mission-command`). Что уже есть и что нет: для этих двух концептов я читал только справочные страницы Wikipedia ✔ (определения и цитаты в `vs-classical-delegation`). **Первичные тексты не читались:** доктрина НАТО AJP-01 и американский ADP 6-0 по mission command; Jensen и Meckling (1976) по теории агентских отношений. Когда вернёмся, начну с них и с вопроса, как «доверие, которое заслуживается и поддерживается» превращается в параметр системы (связь: режим по действию `mode-per-action`, `mode-per-action`, `commanders-intent-trust`; `human-org-parallels.md` строки 1–2).

---

<a id="one-model-errs-differently"></a>

## `one-model-errs-differently`. Как заставить даже одну модель ошибаться по-разному

**Вопрос владельца:** «ок, это тоже довольно важно. как заставить даже одну модель ошщибаться по разному?»
**Фрагмент:** «копии одной модели ошибаются согласованно.»

**Короткий ответ.** Одним промптом это почти не получается. Эмпирически ✔ (препринты 2026 года, [Nogueira и др.](https://arxiv.org/abs/2607.02808), [Ron и др.](https://arxiv.org/abs/2606.20158)): главный фактор — **выбор модели**, влияние стратегии промптинга «negligible», язык умеренный; ансамбли реализуют лишь **0.43–0.44** (разные модели) и **меньше 0.3** (одна модель) того выигрыша, который дала бы независимость; но голосование по тройкам всё равно снижает число отказов (Ron и др.: 387.44 → 130.99). Значимая часть общих отказов связана с **двусмысленными местами спецификации**, то есть с общим входом.

**Рычаги, которые работают частично** [подробности и источники: `concepts/error-diversity.md`]:
1. сменить **семейство модели** (проверяющий и судья, панель разных моделей: Verga и др. ✔);
2. сменить **вход**: независимые формулировки и интерпретации требования;
3. сменить **механизм** проверки: исполняемые проверки (property-based, дифференциальные, контракты, мутации), которые не зависят от мнения модели;
4. дать проверяющему **другую информацию** (скрытые сценарии) [синтез];
5. **голосование** по разнородным вариантам, с реалистичными ожиданиями;
6. **измерять независимость**, а не предполагать: парный критерий отказов и доля реализованного выигрыша.

Что **не** установлено: сравнения «персоны одной модели против разных моделей» на коде в литературе я не нашёл; влияние версий одной модели не найдено. Этот вопрос влияет на открытое место определения (`independence-wording`) и оформлен как задача 294.

*См. также:* [`other-ways-to-independence`](owner-dialog-walkthrough.md#other-ways-to-independence), [`cannot-fit-claim-too-strong`](owner-dialog-walkthrough.md#cannot-fit-claim-too-strong), [`graded-satisfaction-populism`](owner-dialog-walkthrough.md#graded-satisfaction-populism); `vs-classical-delegation`, `same-question-for-human-orgs`, `knight-leveson-details`, `five-open-definition-points`; `concepts/knight-leveson.md`.

---

<a id="ask-dont-assume-rule"></a>

## `ask-dont-assume-rule`. Правило: не предполагать, спрашивать

**Вопрос владельца (первая версия):** «не совсем то что я имел в виду, но ок. еще одно правило, не предполагай, задавай вопросы. чем раньше тем лучше (не считая текущего флоу)». **Ответ владельца на уточнение:** «да» (чтение: спрашивать в начале, до работы; идущий флоу вопросами не прерывать). Это решение, правило записано (`decisions.md`, `ask-dont-assume`, `ask-rule-exception`).

**Про «район идей».** Первая версия моего ответа предположила, что остальные две точки участия человека лежат рядом с идеями, и ошиблась. Владелец уточнил, о чём был вопрос: почему в тексте была только одна явная позиция человека, а остальные выглядели домыслом. Полный ответ от начала до конца: `human-points-in-the-loop`.

Связь: правило это человеческая сторона механизма «агент показывает допущения» (`remember-assumptions-list`) и «отложенного вызова инструмента» (`always-on-agents`): лучше всего спрашивать до действия.

---

<a id="reality-as-judge"></a>

## `reality-as-judge`. «Реальность как судья»: что это значит и при чём тут человек

**Вопрос владельца:** «что это значит и при чем тут человек? с двумя остальными все очевидно, но тут нет»
**Фрагмент:** «реальность как судь[я]» (один из трёх доводов, почему человека оставляют на принятии идей, `human-points-in-the-loop`).

**Вы правы, довод был подан неверно.** Он говорит не о человеке. Смысл: ценность идеи определяется не рассуждением (ни человека, ни агента), а тем, что происходит в реальности: используют ли продукт, растут ли показатели, жалуются ли пользователи. Данные ✔ (Kohavi, [энциклопедия A/B-тестов](https://exp-platform.com/Documents/2023-03-11EncyclopeiaMLDSABTestingFinal.pdf)): «We Are Poor at Assessing the Value of Ideas... most ideas fail to improve key metrics. Only one third of the ideas tested on the Experimentation Platform at Microsoft improved the metric(s) they were designed to improve». То есть и эксперты ошибаются в двух случаях из трёх; оценивать идеи на глаз ненадёжно.

**При чём тут человек (исправленная версия):** он не судья идей, а тот, кто:
1. **определяет, что считать успехом** (критерий оценки: у Kohavi это «OEC», выбор которого называется стратегическим, ✔ там же); реальность судит по критерию, но выбрать критерий нельзя без понимания, чего хочет продукт;
2. **подключает реальность** там, где её нельзя измерить автоматически: разговоры с клиентами, рынок, юридические и этические ограничения (агент видит только инструментированное);
3. **решает, что делать с результатом** (продолжить, остановить, изменить критерий).
Сам эксперимент, сбор показателей и отчёт может делать агент (`human-points-in-the-loop`, мониторинг на входе).

Итог: из трёх доводов остаются два для человека (вкус и критерий; арбитр и ответственность) и один про **источник истины** (реальность), которым человек не является. Это исправлено в `human-points-in-the-loop`.

*См. также:* `human-points-in-the-loop`, [`cannot-fit-claim-too-strong`](owner-dialog-walkthrough.md#cannot-fit-claim-too-strong) (реальность как последний судья в диалоге), [`mutation-testing-as-proof`](owner-dialog-walkthrough.md#mutation-testing-as-proof) (счётчик «что прошло в продакшн»); каталог `production-verification`; `concepts/review-cadence.md` (идея нельзя оценивать ревью без реальности).

---

<a id="outbox-stays-open"></a>

## `outbox-stays-open`. Outbox: оставить вопрос открытым

**Вопрос владельца:** «оставь этот вопрос открытым. все еще сильно расплывчато»
**Фрагмент:** «`outbox-explained`. Что такое outbox».

**Принято: вопрос открыт.** `outbox-explained` помечен «открыт», объяснение там остаётся как отправная точка, не как принятый ответ. Записано в `open-threads.md` (`outbox`).
**Вопрос по `ask-dont-assume`**, чтобы продвинуться, когда вернёмся: что именно расплывчато? (а) само понятие «таблица исходящих и диспетчер»; (б) как это выглядит в фабрике без мессенджера, на примерах; (в) зачем отдельная сущность, а не «просто отправить»; (г) как это связано с вашим вниманием как владельца. Можно ответить «потом».

---

<a id="outbox-owner-understanding"></a>

## `outbox-owner-understanding`. Что такое outbox: понимание владельца и что в нём пропущено

**Слова владельца:** «по аутлуку в целом понятно что это выход агента. я понимаю это как меседжи в мессенджерах, письма, код ревью, ну или просто чейнджи в коде/спеках. что я забыл?» (оговорка «аутлук» = outbox). Вопрос `outbox-explained` остаётся открытым по просьбе владельца, это продолжение с его собственной картиной.

**Что в его картине верно.** Outbox действительно про то, что выходит от агента наружу. Но в источнике (`always-on-agents`, раздел про outbox) под ним понимается узкая вещь: **сообщения человеку**, а не всё, что агент делает.

**Что нужно развести (главное, что пропущено).** Выход агента бывает двух видов:
1. **Работа агента на объектах**: изменения в коде и спеках, pull request, коммиты. Они идут в репозиторий и подчиняются правам и проверкам, это не outbox.
2. **Сообщения человеку**: итог («готово, вот что вышло»), вопрос, запрос на одобрение, предупреждение, сводка. Только это в источнике называется outbox.
Из вашего списка: сообщения в мессенджере и письма относятся ко второму виду, **изменения в коде и спеках к первому**, а «код-ревью» двойственное: сам диф и PR это работа (первый вид), а просьба посмотреть и описание PR это сообщение (второй вид).

**Что вы забыли** (по источнику и по тому, что следует из диалога):
1. **Молчание.** «Ничего сообщать не нужно» это законный исход прогона (источник: «The agent decides that nothing needs to be sent»). Без этого агент засыпает владельца сообщениями.
2. **Вопросы и запросы одобрения.** Агент приостанавливается и спрашивает; ответ человека возвращается событием во входящие, и работа продолжается (`deferred-tool-call`).
3. **Темп и сводки.** Не поток мелких сообщений, а собранная сводка; решает, что и когда отправить, отдельный процесс-диспетчер. Источник советует начинать тихо и «earn more of the user's attention over time».
4. **Сбои и тревоги**: что-то сломалось и нужен человек.
5. **Повтор при сбое доставки:** если письмо не ушло, оно не потеряно.
6. **Сообщения между агентами**: в системе из нескольких агентов у каждого есть свои входящие и исходящие [синтез, в источнике не сказано].
7. **Внешние действия с необратимым эффектом** (письма клиентам, платежи, публикации): их полезно держать в outbox с задержкой и возможностью отмены. В диалоге это называлось «отложенные внешние эффекты» (`cheap-failure-too-good`); у AWS задержка сообщений в SQS не больше 15 минут, дольше нужен Step Functions Wait (✔ документация AWS, подробности в `owner-dialog-walkthrough.md`).

**Тот же outbox в этом репозитории** (живой пример): вид 2 в нём представлен отчётами задач (Report и Comments), описаниями PR, ответами планировщика и, главное, списком открытых вопросов в `docs/status.md`: это постоянный список вопросов к владельцу, а заблокированные задачи с вопросом это запросы на решение. Диспетчера и темпа нет: агент отвечает в чате по ходу работы.

*См. также:* `outbox-explained`, `outbox-stays-open`, `attention-metrics`, `keeping-control`, `restore-understanding`, `human-points-in-the-loop`; каталог `outbox-and-dispatcher`, `notification-volume-eval`; `case-study-imagegen.md`.

---

<a id="goodhart-campbell-details"></a>

## `goodhart-campbell-details`. Закон Гудхарта и Кэмпбелла: подробности

**Вопрос владельца:** «нужно больше подробностей»
**Фрагмент:** «закон Гудхарта и Кэмпбелла».

Подробный разбор вынесен в `docs/factory/concepts/goodhart-campbell.md`. Коротко:
- **Гудхарт (1975, денежная политика)** ✔: «Any observed statistical regularity will tend to collapse once pressure is placed upon it for control purposes»; популярная форма «When a measure becomes a target, it ceases to be a good measure» принадлежит Hoskin (1996) и Strathern (1997), а не Гудхарту.
- **Кэмпбелл (формулировки с 1969)** ✔: «The more any quantitative social indicator is used for social decision-making, the more subject it will be to corruption pressures».
- **Примеры** ✔: премия за число проданных машин («even at a loss»), h-индекс и научные награды, длительность госпитализации и повторные госпитализации, цель «100 000 тестов в день» в Великобритании.
- **Четыре механизма** (Manheim и Garrabrant, 2018 ✔ [arXiv:1803.04585](https://arxiv.org/abs/1803.04585)): regressional, extremal, causal, adversarial; «the increased optimization power offered by artificial intelligence makes it especially critical for that field».
- **Для агентов:** StrongDM ✔ «Tests can be reward hacked»; METR и карточка Claude 3.7 ◐ (подгонка под тесты).
- **Выводы для фабрики** [синтез]: несколько метрик; скрытые и ротируемые проверки; мерить исход, а не выход; разделять оптимизирующего и оценивающего; ревьюить сами метрики; арбитр-человек.

*См. также:* [`what-is-holdout`](owner-dialog-walkthrough.md#what-is-holdout), [`graded-satisfaction-populism`](owner-dialog-walkthrough.md#graded-satisfaction-populism), [`cannot-fit-claim-too-strong`](owner-dialog-walkthrough.md#cannot-fit-claim-too-strong), [`other-ways-to-independence`](owner-dialog-walkthrough.md#other-ways-to-independence); `vs-classical-delegation`, `same-question-for-human-orgs`; `human-org-parallels.md` строка 5.

---

<a id="knight-leveson-details"></a>

## `knight-leveson-details`. Эксперимент Knight–Leveson: подробности

**Вопрос владельца:** «тоже»
**Фрагмент:** «эксперимент Knight–Leveson».

Подробный разбор: `docs/factory/concepts/knight-leveson.md`. Коротко ✔ (отчёт NASA/UVA 1986 прочитан, плюс два препринта 2026):
- **1986:** студенты старших курсов и магистранты в UVA и UC Irvine писали программы по одной спецификации (Launch Interceptor): **27 программ (9 и 18)**, каждая прогнана на **1 000 000 случайных тестов**; 45 ошибок; независимость отвергнута для многих пар (в отчёте 99.5%); при этом голосование по версиям всё равно снижало вероятность отказа (трёхверсионные системы: примерно на порядок).
- **2026, агенты:** Ron и др. (48 реализаций, та же спецификация): «substantial common-mode failure», связанная с трудными и двусмысленными местами спецификации; голосование по тройкам снижает среднее число отказов с 387.44 до 130.99. Nogueira и др. (224 задачи, 12 моделей, 5 языков): независимости нет, ансамбли получают 0.43–0.44 идеального выигрыша.
- **Смысл:** независимость ошибок нарушается и у людей, и у агентов; общий вход (спецификация) частично виноват; голосование полезно, но выигрыш частичный; независимость можно измерять.

*См. также:* [`other-ways-to-independence`](owner-dialog-walkthrough.md#other-ways-to-independence), [`cannot-fit-claim-too-strong`](owner-dialog-walkthrough.md#cannot-fit-claim-too-strong); `same-question-for-human-orgs`, `one-model-errs-differently`; `evidence-register.md` (грады обоих препринтов).

---

<a id="five-open-definition-points"></a>

## `five-open-definition-points`. Ответы на пять открытых мест определения

**Владелец:** 1 «да, обязательна»; 2 «вопрос не понятен до конца, дай больше информации»; 3 «человек нужен на регулярных ревью, нужно понять как часто и что он должен ревьювить»; 4 «вся цель привести определение в то состояние, чтобы не было разницы между людьми и агентами, и нужно чётко понимать почему её нет; определение не цель, цель инструкция, как построить универсальную фабрику»; 5 «оставь».

### `feedback-layer-mandatory` Слой обратной связи: да, обязателен
Принято. В определении v1.1 это условие (3): то, что происходит после выпуска, возвращается на вход. Открытое место закрыто.

### `independence-wording` Независимость проверки как условие: решено (временно)
Что спрашивалось: что должно отличаться у того, кто производит результат, и у того, кто проверяет, чтобы проверку считать независимой. Четыре оси: другой исполнитель, другая информация, другой метод, другой источник истины. Выбор владельца: **моя рекомендация**, то есть независимость **измеряется, а не предполагается, и хотя бы одна из четырёх осей различается; по умолчанию различаются метод или источник истины**. Решение **временное**: вернуться к нему, когда владелец лучше поймёт происходящее (открытая тема `independence-wording-revisit`, запись в `decisions.md`).

### `review-design` Регулярные ревью человека: решения владельца и объяснение
Ответ владельца: он ревьюит много, по несколько часов в день (это его собственная работа, не требование фабрики); регулярность по календарю для фабрики не важна, важно, что для неё **валидно**; у него нет фабрики, чтобы взять данные; он не понимает, что такое «верхняя граница допущенного дефекта»; цель нулевое число багов, он понимает, что она недостижима, и просит смотреть, **как приблизиться**; ревью правил фабрики в 100% случаев пойдёт через человека вместе с агентом, возможно через нескольких человек.

**Что записано как решение** (`decisions.md`): правила фабрики ревьюятся всегда человеком вместе с агентом, без выборки; календарная регулярность не нужна, а нужны триггеры по событиям и свидетельствам; направление к нулю дефектов без числового порога.

**Что такое «верхняя граница», простыми словами.** Вопрос «как плохо может быть на самом деле, если мы проверили и ничего не нашли». Пример: вы проверили 30 изменений и не нашли ни одной проблемы. Означает ли это, что проблем нет? Нет: возможно, они есть, но встречаются редко и в выборку не попали. Грубое правило (правило трёх ◐): при 30 проверках без находок доля проблемных изменений, скорее всего, не выше примерно 10%; при 100 проверках не выше примерно 3%; чтобы уверенно сказать «не выше 0,3%», нужны около 1000 чистых проверок. Нулевую долю выборкой не доказать никогда. Поэтому **число от владельца не нужно**: вопрос «какую границу вы допускаете» снят.

**Как приближаться к нулю** (направление, не порог) [синтез]:
1. каждый пропущенный дефект разбирается: найти причину и превратить её в новую проверку или правило; проверить, что проверка действительно падает на этом дефекте;
2. смотреть на тенденцию, а не на число: сколько дефектов дошло до человека или до продукта за период, какая доля поймана автоматикой до человека, сколько находок на час человеческого ревью;
3. ограничивать ущерб по классам риска (обратимость, защита состояния, откат);
4. человеческое внимание тратить там, где оно даёт больше всего: правила фабрики (решено: всегда), намерение и критерии, классы высокого риска; остальное выборкой, чтобы мерить, сколько ловит автоматика.

**Что для фабрики «валидно» вместо расписания** (триггеры): изменение правил фабрики или критериев приёмки (всегда человек и агент); изменение класса риска или появление нового; расхождение независимых проверок; пропущенный дефект; выход показателей за норму. Регулярный просмотр показателей по расписанию остаётся только удобством.

**Ваши несколько часов в день** это ресурс: вопрос дизайна в том, на какие артефакты их тратить, чтобы получить максимум предотвращённых дефектов. Один вопрос (по `ask-dont-assume`): на что именно уходят эти часы (дифы кода, спецификации, планы, отчёты, что-то другое)? Если ответ окажется «не по ImageGen» (по другой работе), скажите: в ImageGen вы, по вашим словам, кода почти не видели.

### `goal-and-no-difference` Цель: нет разницы между людьми и агентами; цель инструкция
Принято и перестроено (README, определение v1.1, задача 292):
- **Цель** теперь «инструкция, как построить универсальную фабрику» (задача 292). Определение, источники и каталог средства.
- **Определение v1.1** не упоминает «ИИ» в определяющей части: *исполнитель* может быть человеком, агентом или обоими.
- **Почему разницы нет** [синтез, опора `vs-classical-delegation`, `same-question-for-human-orgs`, `knight-leveson-details` и `human-org-parallels.md`]: фабрика описывается над **профилем исполнителя** (способности; надёжность и корреляция ошибок с другими; стоимость и скорость; память между задачами; возможность переспросить; стимулы и ответственность; уровень доверия по областям; наблюдаемость работы). Человек и агент это два набора значений параметров, а не разные виды сущностей; принципы (независимая проверка, границы, обратная связь, решения на точках) от значений не зависят. Сравнение значений: таблица в `definition.md`.
- **Где разница всё-таки есть, и почему это не ломает тезис:** (а) *ответственность*: юридически отвечает человек; остаётся за человеком-владельцем, фабрика это не меняет; (б) *масштаб и цена*: разница в величине, но она пересекает пороги, где меняется дизайн (ревью становится узким местом, независимость нужно измерять, а не ждать); (в) *постоянство памяти и возможность переспросить*: у агента нужны записанная память и приостановка вместо вопроса. Значит, различия сидят в **значениях параметров**, а не в принципах.

### `name-open` Название: оставлено открытым
`open-threads.md` `name-of-the-thing`.

### Итог
Из пяти открытых мест: слой обратной связи обязателен (закрыто); цель и «нет разницы между людьми и агентами» (закрыто, `goal-and-no-difference`); независимость проверки выбрана временно, вернуться позже (`independence-wording-revisit`); ревью человека решены частично (`review-design`); название оставлено открытым. Определение обновлено до v1.2 (`definition.md`), живой пример: `case-study-imagegen.md`.
