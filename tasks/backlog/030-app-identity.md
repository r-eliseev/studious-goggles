---
id: 030
status: blocked
assignee:
claimed:
created: 2026-10-27
---

# Идентификатор приложения и оформление

## Описание
Заменить шаблонный MAUI ApplicationId на уже выбранный владельцем `com.eliseev.imagegen`, оформить иконку/заставку и Windows publisher. Не придумывать новые идентификаторы и издателя. Нужна: в проекте всё ещё `com.companyname.imagegen.maui`, в manifest — `CN=User Name` и `User Name`.

Зависит от: —
Среда: планирование Linux или Windows; правка Windows manifest и проверка Windows data/secret migration — только Windows, отдельной задачей до занятия реализации.

Статус `blocked` обусловлен реальными недостающими данными владельца, а не ожиданием 016.

## Критерии готовности
- ApplicationId совпадает с утверждённым `com.eliseev.imagegen`; Publisher и PublisherDisplayName даны владельцем в точном виде.
- Владелец предоставил или явно одобрил иконку/заставку и решение о переносе существующих данных/ключей.
- Android/общие assets и Windows-specific manifest/migration разделены по среде, с небольшими исполнимыми планами.
- При выбранном переносе проверены сохранность БД, файлов и настроек, повторный запуск и восстановление после неудачи; секреты не копируются в открытый файл.
- Если переноса не будет, инструкция честно предупреждает о новом профиле и повторном вводе ключей.

## Анализ
Проверено: `MauiAppPaths` берёт `FileSystem.Current.AppDataDirectory`, тогда как Avalonia `DesktopAppPaths` использует отдельный ImageGen/override path. Нельзя обещать, что смена MAUI id переносит/меняет данные Avalonia. Android тоже получает новый package id: существующая установка может остаться отдельным приложением, с другой sandbox и хранилищем ключей. Поведение конкретного Windows unpackaged выпуска нужно проверить на Windows: в csproj стоит WindowsPackageType=None, поэтому нельзя выводить фактическую папку только из Package identity.

Известное решение уже есть: ApplicationId выбран 2026-10-28, повторно спрашивать его не нужно. Реальные вопросы: точные Publisher/PublisherDisplayName; одобренные assets; сохранять ли существующие данные MAUI Windows и Android и допустим ли повторный ввод ключей. Автоматически читать/копировать старое защищённое хранилище без проектирования нельзя.

## План
Подготовка после ответа владельца, код до этого не писать:
1. Записать полученные Publisher/PublisherDisplayName, ссылки на одобренные SVG/assets и политику переноса данных/ключей для Windows и Android. Если значение не получено, сохранить blocked с конкретным вопросом.
2. Выделить независимую Linux/Windows часть (ApplicationId, MauiIcon/MauiSplashScreen, общие инструкции) и задачу только Windows (Package.appxmanifest, фактические пути unpackaged app и миграция). Новые номера назначить отдельно; здесь не выполнять смешанный патч.
3. Для смены id описать поведение update/side-by-side Android установки, путь данных до/после, условия доступности старой sandbox и supported способ экспорта/импорта, если нужен. Не обещать доступ к sandbox старого приложения из нового id.
4. Для Windows получить фактические пути и доступность SecureStorage на тестовом профиле; только после этого планировать атомарный идемпотентный перенос с резервной копией и восстановлением. Не удалять исходные данные до успешной проверки.
5. Каждая часть получает точную строку Файлы, сборку и проверки. Общая часть: MAUI Android build; Windows: Windows build и ручная смена identity на тестовых данных. Завершение: отчёт, актуальные инструкции migration и `bash scripts/check.sh` по docs/tasks.md.

Файлы для анализа: `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, `ImageGen/ImageGen.Maui/Platforms/Windows/Package.appxmanifest`, `ImageGen/ImageGen.Maui/Resources/AppIcon/appicon.svg`, `ImageGen/ImageGen.Maui/Resources/AppIcon/appiconfg.svg`, `ImageGen/ImageGen.Maui/Resources/Splash/splash.svg`, `ImageGen/ImageGen.Maui/Services/MauiAppPaths.cs`, `ImageGen/ImageGen.Maui/Services/MauiSecretStore.cs`, `ImageGen/ImageGen.Avalonia/Platform/DesktopAppPaths.cs`, `docs/knowledge/windows-machine.md`.

## Отчёт

## Комментарии

### 2026-10-28, владелец (ответы в чате, записал ревью)
Значения: `ApplicationId` = `com.eliseev.imagegen`. Издатель в `Package.appxmanifest` — владелец (точное имя и строку `Publisher` уточнить у владельца при планировании). Смена `ApplicationId` меняет папку данных и хранилище секретов на Windows: в плане решить, переносить ли данные. Лучше сделать до появления настоящих данных.

### 2026-10-29, claude-536f
Заблокировано: Значения `ApplicationId`, издатель, иконка задаёт владелец (`docs/status.md`, «Открытые вопросы»). Без них не решить. Смена `ApplicationId` меняет папку данных на Windows: нужно решение о переносе. Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Сохранена настоящая блокировка: нужны точные данные владельца и разделение Windows-части, а не ожидание ручной016. Исторические комментарии сохранены.
