---
id: 105
status: todo
assignee:
claimed:
created: 2026-10-29
---

# Семантический поиск только через OpenRouter

## Описание
Реализовать ADR 0016: text embeddings через OpenRouter, модель `baai/bge-m3`. Индексируются названия сессий, промпты и текстовые описания картинок. Пиксели, image URLs и файлы embedding provider не отправляются.

Решение владельца 2026-10-29: offline mode не будет. Не добавлять ONNX Runtime, локальные embedding-модели, веса, downloader или fallback без сети. При отсутствии сети/ключа semantic search явно недоступен; существующий локальный fuzzy search 079 продолжает работать.

Зависит от: 079, 081.
Среда: Linux или Windows.

## Критерии готовности
- Embeddings текста вычисляются `baai/bge-m3` через OpenRouter `POST /embeddings`.
- Ни один embedding request не содержит image bytes/data URL/file URL.
- Векторы хранятся в SQLite BLOB по ADR 0016; поиск не требует sqlite-vec alpha.
- Нет ONNX/local-model package, weights, download/cache/fallback.
- Нет сети/ключа/provider failure → понятное состояние semantic search, fuzzy search остаётся доступен.
- Стратегия переиндексации старых данных отдельно решена владельцем до перевода задачи в `planned`.
- Privacy, storage, ranking и provider error paths покрыты tests.

## Анализ

## План

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал kiro)
Офлайн-режима semantic search не будет.
