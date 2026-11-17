# Ответы на вопросы рабочей сессии (W01–W05)

Вопросы дословно: `docs/factory/sources/05-owner-questions-workshop.md`. Ответы опираются на основной диалог (источник 03, вопросы M01–M43 и их разбор в `qa/03-owner-questions-walkthrough.md`), источники 01, 02 и 04 и новые чтения первоисточников (метки как в прохождении: **✔** прочитано на странице, **◐** сниппет или пересказ, **✖** исправление, **[синтез]** авторский вывод без внешнего источника). Принцип P1: всё документируется, даже если не применимо к текущему харнесу (`docs/factory/README.md`).
Живое определение, которое эти ответы уточняют: `docs/factory/definition.md`. Параллели с организациями из людей: `docs/factory/human-org-parallels.md`. Перекрёстные ссылки на другие вопросы стоят в каждом ответе.

## Исправление к прохождению (нашёл при подготовке)

В `03-owner-questions-walkthrough.md` я сначала написал, что число «48%» (ускорение при работе с Copilot на незнакомом коде) не подтверждается. Это моя ошибка: помощники искали по сниппетам. В первой версии статьи Qiao и др. ✔ ([arXiv:2511.02922v1](https://arxiv.org/abs/2511.02922v1)): «participants took 48.2% less time on average to complete Task 1 with Copilot (M=678.9 seconds, SD=477.9) than without it (M=1311.6 seconds, SD=468.3)», p=0.001. Прохождение исправлено (ответы [M25](03-owner-questions-walkthrough.md#m25-source-03-l1244) и таблица главных исправлений).

---

## W01. Чем это отличается от классической схемы «топы задают намерение, простые исполнители приводят его в действие»?

**Вопрос владельца (дословно):** «чем это отличается от клаической схемы, где топы задают намериние, а простоые исполнители приводят его в действие?»
**Фрагмент, на который ссылается владелец:** «Под этим сейчас обычно понимают повторяемую систему поставки: агенты выполняют координированную работу, а человек задаёт намерение, ограничения, критерии приёмки и правила одобрения».

**Короткий ответ.** Структура та же (делегирование намерения). Отличается не схема, а **свойства исполнителя и стоимость**: они меняют, где и чем приходится заменять доверие и общение. Агенты выполняют и «среднее звено» (планирование, ревью, проверку), а не только «простое исполнение».

**1. Это та же схема, и у неё есть название.** В военной теории командования это *mission command* ✔ ([Wikipedia](https://en.wikipedia.org/wiki/Mission_command)): стиль, который «combines centralized intent with decentralized execution subsidiarity, and promotes freedom and speed of action, and initiative within defined constraints. Subordinates, understanding the commander's intentions, their own missions, and the context of those missions, are told what effect they are to achieve and the reason that it needs to be achieved. Subordinates then decide within their delegated freedom of action how best to achieve their missions.» Приказ содержит намерение, меры контроля и цели. Это в точности «намерение, ограничения, критерии приёмки, правила одобрения». Доктрина НАТО AJP-01 (по той же странице ✔): mission command держится на двух принципах, **доверии и взаимном понимании**; «a commander's trust in subordinates is the key determinant of the autonomy they will be granted. This trust, which must be earned and sustained, includes tolerance for mistakes». В экономике это *принципал–агентская проблема* (конфликт, когда одно лицо действует от имени другого) ✔ ([Wikipedia](https://en.wikipedia.org/wiki/Principal%E2%80%93agent_problem)).
*Связь:* автономность, которая выдаётся по мере заслуженного доверия, это тот же «режим по действиям» (N2 в `owner-notes.md`, C60 в каталоге) и ваша мысль, что уровень автономности принадлежит области, а не человеку ([M12](03-owner-questions-walkthrough.md#m12-source-03-l556), [M13](03-owner-questions-walkthrough.md#m13-source-03-l615)).

**2. Что действительно иначе** (свойства исполнителя; метки у каждого пункта):

| Свойство | Люди-исполнители | Агенты | Что меняется в устройстве системы |
|---|---|---|---|
| Можно переспросить | «What did you mean by that?» нормальная стратегия, быстрое сообщение в Slack | В автономном прогоне переспросить нельзя без специального механизма: Infralovers ✔: «"What did you mean by that?" is a valid clarification strategy with humans. With agents, it isn't. The spec must be complete.» ([текст](https://www.infralovers.com/blog/2026-02-22-architektur-patterns-dark-factory/)). ✖ Уточнение: в интерактивном режиме агент спрашивает; в автономном вопрос это приостановка (deferred tool call, источник 04 ✔), ответ приходит как событие | спека должна быть полнее, либо нужны точки остановки |
| Неявный контекст | накоплен годами, часть «в головах» | нет, пока не записан; память между запусками только та, что записали (источник 04 ✔) | записывать ограничения и причины ([M15](03-owner-questions-walkthrough.md#m15-source-03-l737), [M17](03-owner-questions-walkthrough.md#m17-source-03-l829)) |
| Скорость и цена | дорого, медленно, ограничено числом людей | StrongDM ✔: клон крупного SaaS «was always possible, but never economically feasible», теперь «routine»; ориентир $1000 в день на инженера. Параллельность почти неограниченна | узкое место смещается на ревью: Шапиро ✔ про уровень 3: «Your life is diffs» ([пост](https://www.danshapiro.com/blog/2026/01/the-five-levels-from-spicy-autocomplete-to-the-software-factory/)) |
| Характер ошибок | заметные и «громкие» чаще; у людей есть «нюх» | тихие, уверенные; агент не знает, чего не знает ([M22](03-owner-questions-walkthrough.md#m22-source-03-l1121), [M16](03-owner-questions-walkthrough.md#m16-source-03-l784)) | проверки должны быть механическими и независимыми |
| Накручивание метрик | люди тоже подгоняют показатели (закон Гудхарта ✔: «When a measure becomes a target, it ceases to be a good measure», [Wikipedia](https://en.wikipedia.org/wiki/Goodhart%27s_law)) | StrongDM ✔: агент писал `return true`; METR ◐: правит тесты и код оценки | проверка, скрытая от исполнителя ([M14](03-owner-questions-walkthrough.md#m14-source-03-l694), [M36](03-owner-questions-walkthrough.md#m36-source-03-l1807)) |
| Ответственность | исполнитель отвечает карьерой и репутацией | у агента нет ставок; отвечает человек-владелец | вопрос владельца «если меня спросят, почему так, я должен знать» ([M25](03-owner-questions-walkthrough.md#m25-source-03-l1244)) |
| Независимость ошибок | люди разных школ ошибаются по-разному, но не всегда | копии одной модели ошибаются согласованно (Kim и др. ◐) | независимость источника проверки, а не «ещё один агент» ([M32](03-owner-questions-walkthrough.md#m32-source-03-l1585)) |
| Недетерминизм | один и тот же человек обычно стабилен | один вход даёт разные результаты | статистика по многим запускам ([M31](03-owner-questions-walkthrough.md#m31-source-03-l1522)) |

**3. Агенты берут на себя не только «исполнение».** В схеме «топы → исполнители» есть ещё середина: менеджеры, архитекторы, ревьюеры, QA. В фабрике они тоже агенты, человек остаётся на «точках касания» (источник 02, W03: ревью спеки, кода и продукта; см. W02). Поэтому точнее сравнивать не с «топ + исполнители», а со **всей иерархией разом**, где автоматизирован почти весь контур между намерением и результатом, а сам контур стал на порядки быстрее и дешевле.

**4. Что остаётся тем же.** Две классические задачи делегирования: как донести намерение и как убедиться, что результат ему соответствует. Это ваш пункт W04; различие только в том, чем они решаются (механизмами вместо отношений доверия).

*См. также:* [M02](03-owner-questions-walkthrough.md#m02-source-03-l105) и [M08](03-owner-questions-walkthrough.md#m08-source-03-l391) (старая и новая фабрика: одна ось жёсткости описания), [M12](03-owner-questions-walkthrough.md#m12-source-03-l556), [M13](03-owner-questions-walkthrough.md#m13-source-03-l615), [M16](03-owner-questions-walkthrough.md#m16-source-03-l784), [M19](03-owner-questions-walkthrough.md#m19-source-03-l931), [M25](03-owner-questions-walkthrough.md#m25-source-03-l1244); определение v1: `definition.md`; параллели: `human-org-parallels.md`.

---

## W02. Где остальные две точки участия человека, и почему агенты не могут генерировать идеи?

**Вопрос владельца:** «видно только одну точку. 2 остальных где-то в районе идей? почему агенты не могут их генерировать?»
**Фрагмент:** «Процесс (Warp, источник 02): идея → triage → спека → реализация → ревью (агент, затем человек) → проверка → выпуск → мониторинг → обратно в идеи. Человек вмешивается на трёх точках.»

**Что было не так в моём тексте.** Я перечислил цикл и написал «три точки», но пометил человека только на ревью. Дословно у Warp (источник 02): «ideas are going to come in at the top. Agents are going to do triage. If something is complicated, they will write a spec. Uh these little blue boxes are where humans step in. **Humans will review the spec.** Uh agents will do the implementation. **A human and agent will review the code.** Agents will verify. **Human will review the product.** You ship and then you monitor and round you go.»

**Три точки (синие блоки на схеме)** ✔ (источник 02, дословно выше):

| # | Точка | Что решает человек | После чего |
|---|---|---|---|
| 1 | **Ревью спеки** | «Это то, что мы будем строить?» (утверждение намерения, продуктовых инвариантов и архитектуры: product spec и tech spec) | после triage и написания спеки агентом |
| 2 | **Ревью кода** (человек вместе с агентом) | риски, дизайн, то, что агент-ревьюер пропустил. Ллойд: сначала код ревьюит агент, затем «it becomes over time like a risk-management exercise of like when do you bring in humans to do code review» | после реализации агентом |
| 3 | **Ревью продукта** | «Это то, что нужно пользователю?» (приёмка результата, после агентской верификации, включая computer-use и скриншоты) | перед выпуском |

Точки 1 и 3 стоят на концах цикла, и именно там появляется то, что вы называете «районом идей»: оба конца про **намерение** (что строить и подходит ли результат). Точка 2 про **качество исполнения**.

**Откуда берутся идеи (входы).** Ллойд ✔: «the inputs are really ideas... could be coming from your team, they could be coming from your users... channels... your task tracker... Slack... terminal or IDE... monitoring systems». Мониторинг после выпуска («Is it crashing? Is it being used?») **подаётся на вход фабрики**. То есть часть идей агенты **генерируют уже в этой модели** (инциденты, метрики использования, падения). Источник 04 даёт механизм: события из вебхуков, таймеров и фоновых заданий идут в inbox ✔; таймеры-«follow-up» агент ставит себе сам.

**Почему тогда человек оставлен на принятии идей.** В источниках нет утверждения «агенты не могут»; есть три довода, почему человека оставляют:
1. **Вкус и ценность.** Ллойд на вопрос о вкусе ✔: «human taste, human input, human product sense, um humans like guiding at those touch points where you can't automate stuff is absolutely like essential... what do customers want, what do people want, what's going to be valuable to them». И предупреждение: «if you have like a factory that is churning out that no one cares about, it's like what's the point?»
2. **Реальность как судья.** Проверка идеи требует данных о пользователях и рынке; агенты имеют их, только если эти каналы подключены (мониторинг, трекер). Диалог: «реальность как последний судья: приёмка заинтересованными сторонами, канареечные релизы, метрики из продакшена» ([M36](03-owner-questions-walkthrough.md#m36-source-03-l1807)).
3. **Арбитр и ответственность.** Там, где требования неоднозначны, «человек добавляет решение о том, что считать правильным, и никакая проверка эту информацию заменить не может» ([M36](03-owner-questions-walkthrough.md#m36-source-03-l1807), [M29](03-owner-questions-walkthrough.md#m29-source-03-l1465)); владелец отвечает за систему ([M25](03-owner-questions-walkthrough.md#m25-source-03-l1244)).

**Следствие для устройства фабрики [синтез].** Это решение дизайна, а не ограничение технологии: для каждого действия («предложить идею», «приоритизировать», «написать спеку», «утвердить спеку», «принять продукт») выбирается режим: агент делает и уведомляет / агент делает и ждёт утверждения / только человек. Это тот же принцип «режим по действию» (N2, C60, задача 287). Практичный порядок делегирования по плотности проверок: сначала ревью кода (механические проверки), затем генерация и приоритизация идей на основе сигналов, ревью спеки с арбитром, последним приёмка продукта. Шапиро на уровне 4 ✔: человек «пишет спеку, спорит с ней, планирует, проверяет планы» (точка 1 остаётся за человеком, остальное делегировано).

*См. также:* [M12](03-owner-questions-walkthrough.md#m12-source-03-l556) (как двигаться на уровнях), [M13](03-owner-questions-walkthrough.md#m13-source-03-l615) и [M29](03-owner-questions-walkthrough.md#m29-source-03-l1465) («человек ничего не добавляет»), [M25](03-owner-questions-walkthrough.md#m25-source-03-l1244) (контроль), [M36](03-owner-questions-walkthrough.md#m36-source-03-l1807) (арбитр), [M39](03-owner-questions-walkthrough.md#m39-source-03-l1900) (ревью нельзя убрать, можно сменить предмет), [M21](03-owner-questions-walkthrough.md#m21-source-03-l1020), [M24](03-owner-questions-walkthrough.md#m24-source-03-l1185) (спеки); каталог C01, C02, C60–C66; источник 04 (таймеры, inbox) и W03.

---

## W03. Что такое outbox?

**Вопрос владельца:** «понятно почти все кроме аутбокса. что это?»
**Фрагмент:** «…Вокруг цикла пять примитивов: inbox событий, таймеры, память, права, outbox.»

**Простыми словами.** Inbox это «входящие» агента: куда складываются события (сообщение пользователя, вебхук, таймер). **Outbox это «исходящие»**: куда агент кладёт то, что нужно сообщить человеку (итог работы, вопрос, предупреждение). Отдельный служебный процесс (**dispatcher**) забирает оттуда сообщения и решает, что и когда доставить. Это зеркало inbox.

**Дословно из источника 04** ✔ ([статья](https://claude.com/fr/resources/articles/building-always-on-agents)):
- Почему нужен другой подход, чем «поток в чате»: «With most AI agents, the response is streamed to the user as it's generated... Always-on agents, on the other hand, do most of their work in the background. The user gets the outcome and a few details, rather than the entire step-by-step breakdown.»
- Прогон может закончиться тремя способами: «The agent sends a message to the outbox, The agent decides that nothing needs to be sent, or The agent uses a deferred tool call to request permission or additional input.»
- Когда формальный outbox нужен: «Most always-on agents can get by with a "send" tool that delivers the message directly to the user. You only need a formal outbox when you want to pace your agent's messages or allow your agent to recover from errors.»
- Устройство: «The outbox is a separate table of outgoing messages, analogous to the event inbox. There's also a dispatcher, usually a background service, that's analogous to the worker. The dispatcher reads the pending messages, decides what to deliver, and sends those to the user.»
- Главная тонкость: «it's important to tune how many messages the agent sends. An agent that sends too many becomes noisy, while an agent that doesn't send enough misses things that the user needs to know. We recommend building evals to find the right balance. In practice, it's better for the agent to start quiet and then earn more of the user's attention over time.»

**Зачем отдельная таблица, а не просто «отправить»** [синтез по тексту]: (1) **темп**: объединять несколько сообщений, не заваливать человека; (2) **надёжность**: если доставка упала, сообщение не потеряно и его можно повторить; (3) **решение, что именно сказать**: диспетчер может отбросить повтор или собрать сводку; (4) **вопросы к человеку** (deferred tool call) тоже идут этим каналом, а ответ возвращается событием в inbox.

**Что это значит для фабрики.** Внимание человека самый дорогой ресурс; outbox это механизм, которым «останавливаться только на точках решения» (источник 01, «near the loop») превращается в измеримое правило: сколько сообщений, каких и когда. Метрики для этого: минуты владельца на смерженную задачу, возвраты, ожидание одобрения (N1; C73, C74 в каталоге). В источнике 04 тюнинг проверяется evals.

**Как это выглядит в этом репозитории сегодня (гипотеза, не проверено на скриптах; задача 291):** формального outbox нет; роль «исходящих» играют отчёты задач (Report, Comments), описания PR и ответы агента в чате. Темпа и порога «что стоит сообщить» нет. Для универсальной системы это отдельный элемент каталога (C24).

*См. также:* [M25](03-owner-questions-walkthrough.md#m25-source-03-l1244) (контроль и понимание: что видит владелец), [M34](03-owner-questions-walkthrough.md#m34-source-03-l1687) (понимание: сводки к изменениям), [M42](03-owner-questions-walkthrough.md#m42-source-03-l2087) и [M43](03-owner-questions-walkthrough.md#m43-source-03-l2103) (допущения и pre-mortem как обязательные элементы отчёта), W02 (точки участия человека), источник 04 и `notes/04-always-on-agents.md` (A8), задача 291.

---

## W04. Это вопрос «что должна делать система и как убедиться, что она это делает», он же верен для организаций из людей

**Вопрос владельца:** «это похоже на вопрос в целом, как понять что сисетма должна делать и как убедиться что она делает именно это. вопросы справедливы и для организый только из людей без ИИ»
**Фрагмент:** «Уиллисон формулирует главный вопрос так: как доказать, что код работает, если и реализацию, и тесты пишут агенты».

**Вы правы, это классическая пара.** В программной инженерии она называется **верификация и валидация**; Боэм сформулировал её так ✔ ([Wikipedia: Software verification and validation](https://en.wikipedia.org/wiki/Software_verification_and_validation), ссылка на Boehm, IEEE Software 1(1), 1984): «**Verification: Are we building the product right? Validation: Are we building the right product?**» Там же: «"Building the product right" checks that the specifications are correctly implemented by the system while "building the right product" refers back to the user's needs.» Вопрос Уиллисона это верификация (код соответствует спеке и тесты это доказывают), а ваша формулировка «как понять, что система должна делать» это валидация (спека соответствует нужде). Так что в диалоге они смешаны: исходный разбор про «holdout, оракул, арбитр» это про оба сразу.
- Проблема оракула (откуда мы знаем правильный ответ) старше агентов: Barr и др., IEEE TSE 2015 ◐ ([UCL](https://discovery.ucl.ac.uk/id/eprint/1471263)): «the final source of test oracle information remains the human».
- Накручивание метрик это закон Гудхарта/Кэмпбелла ✔ ([Wikipedia](https://en.wikipedia.org/wiki/Goodhart%27s_law)): «Campbell's law: "The more any quantitative social indicator is used for social decision-making, the more subject it will be to corruption pressures"». Люди делают то же самое.
- Коррелированные ошибки известны на людях: эксперимент Knight и Leveson 1986 ◐ (27 версий, написанных **студентами**, модель независимости отвергнута на 99%) ([NASA NTRS](https://ntrs.nasa.gov/api/citations/19870002808/downloads/19870002808.pdf)). То есть «независимо написанные версии ошибаются по-разному» не выполняется и среди людей.

**Какие решения давно есть в организациях из людей** (а фабрика воспроизводит их механизмами): таблица в `human-org-parallels.md`. Основные: независимое тестирование и приёмка заказчиком, правило двух человек ✔ ([Wikipedia: Two-person rule](https://en.wikipedia.org/wiki/Four-eyes_principle): «access and actions require the presence of two or more authorized people at all times»), V-модель, ревью дизайна, разбор инцидентов без поиска виновных, pre-mortem.

**Что нового именно из-за ИИ** (то, что вы можете считать «различием», а не сутью; ссылки на W01): стоимость и скорость (ревью становится узким местом), отсутствие накопленного неявного контекста и возможности переспросить в автономном режиме, тихие уверенные ошибки, отсутствие ставок у исполнителя, согласованные ошибки копий одной модели. Отсюда сдвиг: проверки и границы должны быть **механизмами**, а не отношениями.

**Вывод [синтез].** Определение фабрики (v1, `definition.md`) нужно строить как *способ организовать делегирование и верификацию*, одинаково применимый к людям, смешанным командам и агентам; ИИ меняет экономику и свойства исполнителя, а не сами вопросы. Это согласуется с вашей же линией в диалоге: «у людей то же» ([M16](03-owner-questions-walkthrough.md#m16-source-03-l784), [M19](03-owner-questions-walkthrough.md#m19-source-03-l931)), «разве агенты не могут менеджить контекст» ([M05](03-owner-questions-walkthrough.md#m05-source-03-l274)), «это буквально мок» ([M11](03-owner-questions-walkthrough.md#m11-source-03-l529)).

*См. также:* [M08](03-owner-questions-walkthrough.md#m08-source-03-l391) (корень риска общий: расхождение между намерением и результатом), [M17](03-owner-questions-walkthrough.md#m17-source-03-l829) (конфликтующие источники), [M25](03-owner-questions-walkthrough.md#m25-source-03-l1244), [M36](03-owner-questions-walkthrough.md#m36-source-03-l1807) (арбитр и оракул), [M39](03-owner-questions-walkthrough.md#m39-source-03-l1900) (от код-ревью нельзя убежать), [M22](03-owner-questions-walkthrough.md#m22-source-03-l1121) (тихий и громкий отказ), W01, `human-org-parallels.md`.

---

## W05. Определение всё ещё непонятно; метод работы

**Вопрос владельца:** «определение в целом еще не понятно. давай будем идти таким путем: по мере продвижения отвечай на мои вопросы, как и раньше используя данные из диалога и новых исслдований, перелинковывай это с другими моими вопросами. все дожно быть в репо»

**Метод (принят, действует с этого момента):**
1. Каждый новый вопрос владельца записывается дословно в `sources/05-owner-questions-workshop.md` (W06, W07, …) вместе с процитированным фрагментом.
2. Ответ ложится в `qa/05-workshop-answers.md`: короткий ответ, подтверждённые факты с метками проверки, перекрёстные ссылки на вопросы M.. и W.. и на каталог, заметки и задачи.
3. Первоисточники читаются (в этой сессии `curl` из Bash работает для большинства хостов), результаты исправляют прежние утверждения явно (как с «48%» выше).
4. Определение живёт в `docs/factory/definition.md`, версионируется и уточняется по мере ответов; открытые места перечислены в нём.
5. Обратные ссылки: в прохождении (`03-owner-questions-walkthrough.md`) у связанных вопросов стоят строки «См. также: W..».

**Определение v1** (полный текст, компоненты, пример и открытые места: `docs/factory/definition.md`):
> **Программная фабрика** это повторяемая организация работы, в которой человек задаёт намерение и правила приёмки, а ИИ-агенты выполняют всё между намерением и проверенным результатом (планирование, сборку, ревью, проверку, выпуск); проверка результата независима от тех, кто его произвёл, границы действий исполняются системой, а то, что происходит после выпуска, возвращается на вход. Человек вызывается только в определённых точках решения.

Чем «фабрика» отличается от «агента»: фабрика это **процесс** (стадии, хранилища, границы, измеряемый выход), агент это **исполнитель** внутри стадии. Чем отличается от конвейера CI: в фабрике на стадиях решения принимает агент, а не детерминированный скрипт. Чем отличается от классической фабрики Greenfield и Short (2004): та генерирует детерминированно из модели ([M02](03-owner-questions-walkthrough.md#m02-source-03-l105), [M08](03-owner-questions-walkthrough.md#m08-source-03-l391)).

*Что ещё неясно в определении, нужно ваше слово:* см. раздел «Открытые места» в `definition.md`.
