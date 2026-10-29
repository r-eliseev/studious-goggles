---
id: 100
status: todo
assignee:
claimed:
created: 2026-10-29
---

# Релиз: проверить, что MAUI для Windows arm64 собран под arm64

## Описание
Замечание по релизу v0.1.4 (2026-10-29): архивы `ImageGen-Maui-v0.1.4-win-x64.zip` (109 647 779 байт) и `ImageGen-Maui-v0.1.4-win-arm64.zip` (109 647 794 байт) отличаются на 15 байт, тогда как Avalonia x64 и arm64 отличаются примерно на 2,6 МБ. Похоже, оба MAUI-архива собраны под одну архитектуру.

Что известно: `.github/workflows/release.yml`, задание `maui-windows`, публикует с `-p:RuntimeIdentifierOverride=<rid>` вместо `-r` (с `-r` restore просит Mono для Android). Доходит ли `RuntimeIdentifierOverride` до `dotnet publish` для цели `net10.0-windows10.0.19041.0` при `-o`, неизвестно. Проверить: скачать оба архива и посмотреть машину PE-заголовка `ImageGen.Maui.exe` и архитектуру нативных DLL (Windows App SDK), например `file` или `objdump -f`. Если архитектура неверна — найти рабочий способ (например, `RuntimeIdentifier` вместе с `-f`, отдельный restore для одного TFM).

Правка `.github/workflows/` — по явной просьбе владельца (эта задача и есть просьба, если план одобрен).

Зависит от: —
Среда: Linux или Windows (проверка архивов — на Linux; запуск arm64-сборки — только на Windows on ARM, если такая машина есть).

## Критерии готовности
- Выяснено и записано в «Отчёте», под какую архитектуру собраны оба архива v0.1.4.
- Если arm64-архив не arm64: workflow исправлен, пробный ручной запуск (`workflow_dispatch`) даёт архив, где `ImageGen.Maui.exe` и нативные DLL — ARM64, а x64-архив — x64.
- Неочевидный факт о публикации MAUI с RID — в `docs/knowledge/maui.md`.

## Анализ

## План

## Отчёт

## Комментарии
