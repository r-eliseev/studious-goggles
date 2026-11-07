---
id: 221
status: approved
assignee:
claimed:
created: 2026-11-07
---

# OpenRouter: генерация аудио — все модели с аудио- и речевым выходом

## Описание
ADR 0027, п. 1; заменяет первый этап 167. Клиент OpenRouter для моделей с аудио на выходе: `POST /api/v1/chat/completions` с `modalities: ["text","audio"]`, `audio: {"format": "mp3"}`, `stream: true`; звук приходит кусками base64 в `choices[0].delta.audio.data` (документация OpenRouter «Audio», `docs/knowledge/media-generation.md`). Сценарий LiveCheck `music` (208) уже собирает такой поток в файл; формат ответа Lyria живым запросом **не проверен** — клиент должен работать при mp3 и wav.

Уточнение владельца 2026-11-07: никакого фиксированного списка моделей — загружать все доступные, как картинки и видео. У OpenRouter два вида (публичный каталог `GET /api/v1/models?output_modalities=all`, 2026-11-07): `audio` — 4 модели (`google/lyria-3-pro-preview`, `google/lyria-3-clip-preview`, `openai/gpt-audio`, `openai/gpt-audio-mini`), генерация через `chat/completions` с потоком; `speech` — 38 моделей (ElevenLabs, OpenAI, Mistral, HeyGen, Fish Audio и др.), генерация через `POST /api/v1/audio/speech` (совместим с OpenAI Audio Speech API; поля `model`, `input` обязательны, `voice`, `response_format` — по документации OpenRouter «Text-to-speech»; ответ — байты аудио с `Content-Type`, например `audio/mpeg`; цена — `pricing.prompt` за символ входного текста; без `voice` запрос уходит только к провайдерам с голосом по умолчанию, остальные отвечают ошибкой проверки).

Зависит от: —
Среда: Linux или Windows; живой запуск — владелец (`--only music`, ~$0.04).

## Критерии готовности
- Каталог: `AudioModel` (record: `ProviderId`, `Id`, `Name`, `Description`, `AudioOutput Output` — `ChatAudio` для `audio`, `Speech` для `speech`, `decimal? PricePerInputCharacterUsd` — из `pricing.prompt` для `Speech`, иначе `null`, `FetchedAt`) и `IAudioModelProvider.ListAudioModelsAsync` в `OpenRouterClient`: **все** модели из `GET /api/v1/models?output_modalities=all`, у которых `architecture.output_modalities` содержит `"audio"` или `"speech"`; модель с обоими — `ChatAudio`. Без списков и фильтров по именам. Кэш — по образцу видео-каталога 200 (таблица `audio_models`, миграция `NNNN_audio_models.sql`, `AudioModelCatalogService`).
- `IAudioGenerationProvider.GenerateAudioAsync(AudioGenerationRequest(AudioModel Model, string Prompt, string? Voice), CancellationToken)` — путь выбирается по `Model.Output`: `Speech` → `POST audio/speech` с `{model, input: Prompt, response_format: "mp3", voice}` (`voice` только если задан), `Content` — тело ответа потоком (`ResponseHeadersRead`), `MediaType` — из `Content-Type` (при отсутствии — по первым байтам), `CostUsd` — `null` (цену по символам считает оценка 222); `ChatAudio` — как ниже. Общий результат — → `AudioGenerationResponse : IAsyncDisposable` с `Stream Content` (только чтение, раскодированные байты по мере прихода потока), `string MediaType` (известен после первых байтов), `decimal? CostUsd` и `string? Transcript` (известны после конца потока).
- Раскодирующий поток `SseAudioStream` (в `ImageGen.Core/OpenRouter/`): читает строки `data: …`, пропускает пустые и комментарии, на `[DONE]` — конец; из JSON берёт `choices[0].delta.audio.data` (base64 → байты), `delta.audio.transcript` (склеивает), `usage.cost` (последнее значение). Нет ни одного аудио-куска до конца → `ProviderException(ErrorKind.EmptyResponse, "audio stream contained no audio")`. Ответ `error` в потоке → `ProviderException` с текстом.
- Формат — по первым байтам: `ID3` или кадр MPEG (`0xFF` и старшие 3 бита второго байта = 1) → `audio/mpeg`; `RIFF....WAVE` → `audio/wav`; `OggS` → `audio/ogg`; `fLaC` → `audio/flac`; иначе — `ProviderException(ErrorKind.Unknown, "unknown audio format")`.
- `ChatAudio`: если сервер отвечает 400 с упоминанием `format` или `voice` в тексте ошибки — один повтор с `audio.format = "wav"` (как сценарий 208). `Speech`: ошибка проверки голоса → `ProviderException(ErrorKind.BadRequest, …)` с текстом провайдера, чтобы пользователь выбрал голос (223).
- Перенос логики разбора: сценарий LiveCheck `MusicScenario` переходит на `SseAudioStream` из Core (одна реализация разбора).
- Тесты без сети: три синтетических SSE-события с base64 кусками mp3-заголовка → поток отдаёт ровно эти байты, `MediaType = audio/mpeg`, `CostUsd` из последнего события; WAV-заголовок → `audio/wav`; поток без аудио → `EmptyResponse`; событие `error` → исключение; 400 про формат → второй запрос с `wav`; каталог из фикстуры `/models` (взять 2 модели `audio`, 2 `speech`, 2 других) → четыре модели с верным `Output` и ценой за символ у `speech`; `Speech`-запрос: тело с `model`/`input`/`response_format`, без `voice`, если не задан; ответ `audio/mpeg` 2 КБ → поток 2 КБ, тип `audio/mpeg`.

## Анализ
`ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs` (картинки, чат, видео — способы отправки, проверки ответа, лимиты `ProviderResponseLimits`; для потока — `HttpCompletionOption.ResponseHeadersRead`, тело не логируется). `ImageGen/ImageGen.LiveCheck/Scenarios/MusicScenario.cs` — существующий разбор SSE, его тесты — `ImageGen/ImageGen.LiveCheck.Tests/`. Видео-каталог 200 — образец кэша и службы каталога. `OpenRouterClient` обёрнут в `InMemoryQueuedImageConnection` — пробросить новые интерфейсы, как для видео (`ProviderRouter.GetAudioModelProvider`, `GetAudioGenerationProvider`).

## План
1. Падающие тесты потока и формата.
2. `SseAudioStream`, `AudioGenerationResponse`, метод клиента, повтор с wav.
3. Каталог: тип, провайдер, миграция, репозиторий, служба; проброс через обёртку и роутер.
4. Перевести `MusicScenario` на общий разбор; тесты LiveCheck зелёные.
5. Документация: `docs/knowledge/media-generation.md`, «OpenRouter: аудио» — два вида моделей и `audio/speech` (факты из «Описания», источник — документация OpenRouter «Text-to-speech», 2026-11-07); после живого запуска (владелец) — фактические форматы.

Файлы: `ImageGen/ImageGen.Core/Domain/Models/AudioModel.cs`, `ImageGen/ImageGen.Core/Providers/IAudioModelProvider.cs`, `ImageGen/ImageGen.Core/Providers/IAudioGenerationProvider.cs`, `ImageGen/ImageGen.Core/Providers/AudioGenerationRequest.cs`, `ImageGen/ImageGen.Core/Providers/AudioGenerationResponse.cs`, `ImageGen/ImageGen.Core/OpenRouter/SseAudioStream.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageConnection.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs`, `ImageGen/ImageGen.Core/Providers/IProviderRouter.cs`, `ImageGen/ImageGen.Core/Services/AudioModelCatalogService.cs`, `ImageGen/ImageGen.Core/Storage/IAudioModelRepository.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_audio_models.sql`, `ImageGen/ImageGen.Data/Repositories/AudioModelRepository.cs`, `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, `ImageGen/ImageGen.LiveCheck/Scenarios/MusicScenario.cs`, тесты Core, Data, LiveCheck.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.LiveCheck.Tests/ImageGen.LiveCheck.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.LiveCheck/ImageGen.LiveCheck.csproj --verify-no-changes
bash scripts/check.sh
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
Создано по просьбе владельца 2026-11-07 («генерация аудио нужна и отдельно от видео»); заменяет 167. Провайдер — OpenRouter (ADR 0027). Живой запуск LiveCheck `--only music` (208) владельцем ещё не сделан; клиент рассчитан на оба формата. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-07, claude-audit
По уточнению владельца: каталог — все модели с выходом `audio` и `speech`, без списков; добавлен путь `audio/speech` для речевых моделей.
