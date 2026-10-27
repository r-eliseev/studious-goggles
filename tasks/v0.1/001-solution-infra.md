---
id: 001
status: todo
assignee:
claimed:
created: 2026-10-27
---

# Инфраструктура решения и тестов

## Описание
Подготовить решение `ImageGen/ImageGen.slnx` к нескольким проектам по `docs/architecture.md`: общие свойства, единый список версий пакетов, правила форматирования, первый тестовый проект и архитектурный тест. Заменить пустые заготовки в `ImageGen.Core/Integration/` раскладкой каталогов из архитектуры (снимает известные проблемы: интерфейсы `internal` и опечатка `IImangeToText`).

Зависит от: нет.

## Критерии готовности
- `ImageGen/Directory.Build.props` (net10.0, `Nullable`, `ImplicitUsings`, предупреждения как ошибки) и `ImageGen/Directory.Packages.props`; проект-пример `ImageGen/ImageGen/` не изменён и собирается.
- `.editorconfig` в корне, `dotnet format ImageGen/ImageGen.slnx --verify-no-changes` проходит.
- `ImageGen.Core.Tests` в решении; выбор тестового фреймворка оформлен ADR.
- Архитектурный тест: `ImageGen.Core` не ссылается на сборки `Microsoft.Maui*`, `Microsoft.AspNetCore*`, `Microsoft.Data.Sqlite*`.
- Заготовки `Integration/` удалены, каталоги `Platform/`, `Config/`, `Providers/`, `Catalog/`, `Generation/`, `Results/` заведены.
- CI (`build.yml`) запускает тесты; `dotnet test` проходит.

## Анализ

## План

## Отчёт

## Комментарии
