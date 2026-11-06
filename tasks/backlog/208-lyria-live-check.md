---
id: 208
status: done
assignee:
claimed:
created: 2026-11-06
---

# Музыка: живая проверка Lyria 3 через OpenRouter

## Описание
ADR 0027, п. 1. Музыка на OpenRouter — только `google/lyria-3-clip-preview` ($0.04 за 30-секундный клип по описанию модели) и `google/lyria-3-pro-preview` ($0.08 за песню), через `chat/completions` с `modalities: ["text","audio"]` и `stream: true`. Документация не говорит, какие параметры `audio` (`format`, `voice`) принимает Lyria, в каком формате приходит звук и как отражается цена. Нужен сценарий LiveCheck и один живой запрос с ключом владельца, затем — план музыки (167).

Зависит от: —
Среда: Linux или Windows; живой запуск — с ключом OpenRouter владельца (платно, ~$0.04).

## Критерии готовности
- Сценарий LiveCheck `music` (в `ImageGen/ImageGen.LiveCheck/Scenarios/`, по образцу существующих сценариев и бюджета `BudgetGuard`): `POST chat/completions` с `model = google/lyria-3-clip-preview`, `modalities = ["text","audio"]`, `audio = {"format": "mp3"}` (без `voice`; если провайдер отвечает ошибкой про голос или формат — второй запрос с `wav`, записать ответ), `stream = true`, коротким промптом («calm piano loop, 10 seconds»); собирает `delta.audio.data` (base64) в файл `tmp/live-check/<дата>/music.<ext>`, сохраняет в trace первые и последний события потока без содержимого `data` (только длины), `usage` из последнего события.
- Оценка бюджета сценария — $0.10; без ключа или при нехватке бюджета сценарий пропускается, как остальные.
- Тесты LiveCheck без сети: разбор потока из записанных строк SSE (фикстура — синтетическая, 3 события с base64 коротких байтов) → файл с ожидаемыми байтами; поток без аудио → понятная ошибка.
- После запуска владельцем (команда — в «Проверке»): в «Отчёт» и `docs/knowledge/media-generation.md` («OpenRouter: аудио») записаны: какие параметры приняты, формат и размер файла, длительность, поля `usage` и цена, есть ли текст/транскрипт, сколько длился запрос.

## Анализ
LiveCheck — `ImageGen/ImageGen.LiveCheck/` (сценарии, `BudgetGuard`, `LiveCheckEstimates`, отчёт и trace в `tmp/live-check/`), инструкции — `docs/knowledge/dev-environment.md` («LiveCheck»). Как сценарии получают ключ и пишут trace — по существующим сценариям чата OpenRouter.

## План
1. Тест разбора потока (падающий), разбор SSE и сборка base64.
2. Сценарий `music` с бюджетом и пропуском.
3. Передать владельцу команду запуска; записать результаты после запуска (если исполнитель не может запустить сам — задача остаётся `in-progress` только до записи результата владельцем; код считается готовым по `docs/tasks.md`, «Завершение», п. 4, а живой запуск переносится комментарием).

Файлы: `ImageGen/ImageGen.LiveCheck/Scenarios/MusicScenario.cs` (новый; имя — по образцу соседних), `ImageGen/ImageGen.LiveCheck/LiveCheckEstimates.cs`, `ImageGen/ImageGen.LiveCheck/Scenarios/LiveCheckRunner.cs`, `ImageGen/ImageGen.LiveCheck.Tests/` (новый тест), `docs/knowledge/media-generation.md`, `docs/knowledge/dev-environment.md` (сценарий в списке).

### Проверка
```
dotnet test --project ImageGen/ImageGen.LiveCheck.Tests/ImageGen.LiveCheck.Tests.csproj
dotnet format ImageGen/ImageGen.LiveCheck/ImageGen.LiveCheck.csproj --verify-no-changes
bash scripts/check.sh
```
Живой запуск (владелец): `dotnet run --project ImageGen/ImageGen.LiveCheck -- --budget-usd 0.2 --only music`; задать `IMAGEGEN_LIVE_OPENROUTER_KEY` в окружении. Повтор в WAV автоматически выполняется только после ошибки о формате/голосе и при наличии места в лимите запросов.

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

- Добавлен изолированный `--only music` сценарий для `google/lyria-3-clip-preview`: потоковый POST без поля `voice`, mp3 по умолчанию, повтор с wav только после ошибки формата/голоса и при свободном лимите запроса. Резервирует $0.10, decodes `delta.audio.data` в файл под `tmp/live-check/<UTC-дата>/`, собирает usage и признак транскрипта. Trace содержит status/formats и первые/последние stream events с длиной аудио, но не base64.
- Offline-тесты: три синтетических SSE-события дают ожидаемые байты; отсутствие аудио даёт понятную ошибку; проверены тело запроса, Bearer auth и повтор mp3 → wav. Все 11 тестов LiveCheck passed.
- Режим `--only music` без ключа проверен без сети: отчёт показывает `Key not supplied`, запрос не отправляется. Для запуска владельцем: задать `IMAGEGEN_LIVE_OPENROUTER_KEY`, выполнить `dotnet run --project ImageGen/ImageGen.LiveCheck -- --budget-usd 0.2 --only music`.
- Живой запрос не выполнял: он требует ключ владельца и может тарифицироваться. После запуска владельцу нужно дополнить этот отчёт и `docs/knowledge/media-generation.md` фактическими параметрами, форматом/размером/длительностью, usage, ценой и временем.
- Проверки: LiveCheck.Tests — 11/11; `dotnet format` для LiveCheck и LiveCheck.Tests — успешно; `bash scripts/check.sh` — успешно. Команда из первоначальной карточки `--scenario music` заменена на текущий поддерживаемый фильтр `--only music`.

## Комментарии

### 2026-11-06, codex-gpt6
Кодовая часть готова; ручной платный запрос оставлен владельцу согласно строке «Среда» и критериям задачи. Команда запуска приведена выше.

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй звук и видео»; выбрано: всё через OpenRouter, LibVLC в Avalonia сразу). ADR 0027, исследование — `docs/knowledge/media-generation.md`. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен: «заапрувь все таски которые будут».
