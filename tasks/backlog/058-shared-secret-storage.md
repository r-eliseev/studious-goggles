---
id: 058
status: todo
assignee:
claimed:
created: 2026-10-28
---

# Подумать: общее хранилище ключей для оболочек

## Описание
Владелец (2026-10-28): хранилище ключей выглядит как то, что должно быть общим. Решение отложено («сложный выбор, подумать, как лучше»). Задача — сначала разобрать варианты и предложить решение владельцу; код — только после выбора и отдельного плана.

Зависит от: —.
Среда: любая (разбор и документ); реализация — по итогам.

## Критерии готовности
- Сравнение вариантов (ниже и, если найдутся, другие): что выносится, кто пользуется, что меняется для пользователя (общий ли ключ у MAUI и Avalonia на одной машине), стоимость, риски, тесты.
- Рекомендация и вопросы владельцу в «Комментариях»; после выбора — ADR (уточняет или заменяет ADR 0008) и план реализации в этой задаче или новой.

## Анализ
- Сейчас: `ISecretStore` — в ядре (`ImageGen.Core/Platform`). Реализации:
  - MAUI — `MauiSecretStore` поверх `SecureStorage` (около 30 строк; Android Keystore, iOS и macOS Keychain, Windows — шифрование MAUI);
  - Avalonia — `ImageGen.Avalonia/Services/Secrets/`: `DesktopSecretStore`, `DpapiSecretBackend` (Windows), `SecretServiceBackend` и `SecretServiceDBus` (Linux, `Tmds.DBus`), `KeychainSecretBackend` (macOS, утилита `security`, не проверен), `SecretFileNames`, запасной вариант в памяти (ADR 0008); около 400 строк, тесты в `ImageGen.Avalonia.Tests/Services/Secrets/`.
- Код Avalonia-хранилища не использует типы Avalonia: только DPAPI, D-Bus, `security`, `IAppPaths` и `ISecretPersistenceInfo` (Presentation). Вынести его технически просто.
- У MAUI и Avalonia разные папки данных и разные ключи на одной машине (решение владельца 2026-10-28, задача 039). Общий ключ потребовал бы и общего места хранения.
- Будущая Web-оболочка хранит секреты в файле под ASP.NET Core Data Protection (`docs/status.md`, решения владельца) — третий способ.

Варианты, заданные владельцу 2026-10-28 (ответа нет):
1. Новый проект `ImageGen.Desktop` с этим кодом; им пользуется Avalonia везде и MAUI на Windows и macOS (на Android и iOS остаётся `SecureStorage`).
2. Тот же проект `ImageGen.Desktop`, но MAUI остаётся на `SecureStorage` везде; выигрыш — код не привязан к UI-фреймворку и тестируется без Avalonia.
3. Один слой ключей с выбором реализации по платформе для всех оболочек (десктоп — DPAPI/Secret Service/Keychain, телефоны — `SecureStorage`, Web — Data Protection) за тем же `ISecretStore`.

## План

## Отчёт

## Комментарии
