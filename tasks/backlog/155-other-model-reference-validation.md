---
id: 155
status: cancelled
assignee:
claimed:
created: 2026-10-31
---

# other-model-reference-validation

## Описание

## Критерии готовности

## Анализ

## План

## Отчёт

## Комментарии

### 2026-10-31, claude-audit
Отменена при аудите завершённых задач: пустая заготовка (создана `scripts/new-task.sh --publish` в коммите 6a27422), по имени дублирует выполненную задачу 145 `other-model-reference-validation`. Проверка референсов при запуске на другой модели уже есть в `GenerationService.RunOnModelAsync`, тесты проходят. Номер 155 повторно не используется.
