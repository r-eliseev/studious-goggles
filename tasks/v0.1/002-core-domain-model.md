---
id: 002
status: todo
assignee:
claimed:
created: 2026-10-27
---

# Доменная модель ядра

## Описание
Описать типы, общие для всех сервисов ядра: `ProviderId`, `ModelDescriptor`, `ModelCapabilities` (параметры и допустимые значения, лимит референсов, число картинок, seed, вид генерации), `PriceInfo` (сумма и единица: картинка, мегапиксель, токены, секунды), `CommonParameters` (F4.2), `GenerationKind` (Text2Img, Img2Img, Img2Text), `JobState` (единая модель из ADR 0003), `ProviderErrorKind`, `CostEstimate` (сумма и признак «≈»), сущности `RunGroup`, `Job`, `Result`. JSON через source generation.

Зависит от: 001.

## Критерии готовности
- Типы лежат в каталогах из `docs/architecture.md`, без UI-типов и без рефлексии.
- Деньги хранятся в `decimal` (USD); сложение и сравнение покрыты тестами.
- Состояния задания и переходы между ними описаны и покрыты тестами (запрещённые переходы отвергаются).
- `JsonSerializerContext` для сущностей, которые сохраняются или уходят в API.

## Анализ

## План

## Отчёт

## Комментарии
