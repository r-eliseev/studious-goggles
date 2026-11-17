# Source 05: the owner's questions in the working session (verbatim)

Type: the owner's messages in the Claude Code working session (not the web chat), with the passages from Claude's earlier replies that the owner attached (blockquotes, as pasted). Language: Russian, verbatim. Date: 2026-11-17.
Status: verbatim; new questions are appended here as they come (IDs W01, W02, ...). Answers: `docs/factory/qa/05-workshop-answers.md`. The questions of the web chat (source 03) are in `03a-owner-questions.md` (IDs M01-M43).
Method agreed with the owner on 2026-11-17 (W05): as we move on, every question is recorded here, answered with data from the dialog and new research, **cross-linked with the other questions**, and everything stays in the repository.

---

## W01

**Attached passage (from Claude's reply on M01):**

> Под этим сейчас обычно понимают повторяемую систему поставки: агенты выполняют координированную работу, а человек задаёт намерение, ограничения, критерии приёмки и правила одобрения

**Owner:**

> чем это отличается от клаической схемы, где топы задают намериние, а простоые исполнители приводят его в действие?

## W02

**Attached passage:**

> Процесс (Warp, источник 02): идея → triage → спека → реализация → ревью (агент, затем человек) → проверка → выпуск → мониторинг → обратно в идеи. Человек вмешивается на трёх точках.

**Owner:**

> видно только одну точку. 2 остальных где-то в районе идей? почему агенты не могут их генерировать?

## W03

**Attached passage:**

> Исполнение (Anthropic, источник 04 ✔): агент «не всегда включён», он спит и просыпается на run по событию. Вокруг цикла пять примитивов: inbox событий, таймеры, память, права, outbox.

**Owner:**

> понятно почти все кроме аутбокса. что это?

## W04

**Attached passage:**

> Уиллисон формулирует главный вопрос так: как доказать, что код работает, если и реализацию, и тесты пишут агенты

**Owner:**

> это похоже на вопрос в целом, как понять что сисетма должна делать и как убедиться что она делает именно это. вопросы справедливы и для организый только из людей без ИИ

## W05

**Attached passage:**

> Вопрос к тебе по M01: определение тебя устраивает?

**Owner:**

> определение в целом еще не понятно. давай будем идти таким путем: по мере продвижения отвечай на мои вопросы, как и раньше используя данные из диалога и новых исслдований, перелинковывай это с другими моими вопросами. все дожно быть в репо

---

## W06

**Attached passage (from Claude's correction of the 48% finding):**

> Исправление к прошлому ответу. Я написал, что число «48%» (ускорение с Copilot) не подтверждается. Это была моя ошибка: помощники искали по сниппетам. В первой версии статьи Qiao и др. написано «participants took 48.2% less time on average to complete Task 1 with Copilot», p=0.001 (18 аспирантов). Прохождение по 43 вопросам исправлено.

**Owner:**

> я не до конца понимаю о чем речь, но пусть будет. 18 кажется вообще не репрезентативной выборкой. они скорее всего еще и с одного универа. это такие себе данные

## W07

**Attached passage:**

> Схема та же. Это mission command из военной теории: «centralized intent with decentralized execution». Доктрина НАТО: автономность определяется доверием, которое «must be earned and sustained». В экономике это принципал–агентская проблема. Всё прочитано на страницах Wikipedia.

**Owner:**

> ок, это интереный поворот. мы вернемся к этим концептам

## W08

**Attached passage:**

> копии одной модели ошибаются согласованно.

**Owner:**

> ок, это тоже довольно важно. как заставить даже одну модель ошщибаться по разному?

## W09

**Attached passage:**

> Точки 1 и 3 и есть твой «район идей»: обе про намерение.

**Owner:**

> не совсем то что я имел в виду ,но ок. еще одно правило, не предполагай, задавай вопросы. чем раньше тем лучше (не считая текущего флоу)

## W10

**Attached passage:**

> реальность как судь

**Owner:**

> что это значит и при чем тут человек? с двумя остальными все очевидно, но тут нет

## W11

**Attached passage:**

> W03. Что такое outbox

**Owner:**

> оставь этот вопрос открытым. все еще сильно расплывчато

## W12

**Attached passage:**

> закон Гудхарта и Кэмпбелла

**Owner:**

> нужно больше подробностей

## W13

**Attached passage:**

> эксперимент Knight–Leveson

**Owner:**

> тоже

## W14

**Attached passage:**

> Что ещё нужно от тебя. Пять открытых мест в definition.md:

**Owner:**

> 1. да, обязательна
> 2. вопрос не понятен до конца. дай больше информации
> 3. человек нужен на решулярных ревью. нужно понять как часто и что он должен ревьювить
> 4. по сути, вся цель приветси определение в то состояние, что бы не было разницы между людьми и агентами. и нужно четко понимать почему ее нет. определение все же не цель, цель инструкция как построить универсальную фабрику
> 5. отавь

(Numbered answers refer to the five open points of `docs/factory/definition.md` v1, in order: 1 feedback layer mandatory, 2 independence as a condition, 3 regular human attention, 4 humans-only organizations, 5 the name.)
