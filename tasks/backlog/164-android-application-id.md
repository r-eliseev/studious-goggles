---
id: 164
status: done
assignee:
claimed:
created: 2026-10-31
---

# Идентификатор Android-приложения com.eliseev.imagegen

## Описание
Выделено из 030 по решению владельца 2026-10-31: сменить Android `ApplicationId` с шаблонного `com.companyname.imagegen.maui` на утверждённый `com.eliseev.imagegen` в том же релизе, что и постоянный ключ подписи (163). Оба изменения требуют установки с нуля; вместе — одна переустановка вместо двух.

Windows, iOS и Mac Catalyst не меняются: у Windows смена идентификатора меняет папку данных и хранилище секретов, это остаётся в 030 вместе с Publisher и миграцией.

Зависит от: —
Среда: Linux или Windows с Android SDK.

## Критерии готовности
- APK Android имеет пакет `com.eliseev.imagegen` (`aapt2 dump badging`); у Windows, iOS и Mac Catalyst `ApplicationId` прежний.
- Перенос данных старого пакета не делается: новое приложение — новая установка, ключи вводятся заново (у владельца данные уже стёрты, 161; переход на ключ 163 и так требует переустановки).
- `README.md` и `docs/knowledge/dev-environment.md`, «Релиз»: новый пакет ставится отдельным приложением, старое `com.companyname.imagegen.maui` можно удалить.
- 030 обновлена: Android-часть идентификатора выполнена здесь.

## Анализ
- `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj:34` — общий `ApplicationId` для всех целей. Ссылок на имя пакета в коде, манифесте Android и workflow нет (поиск `com.companyname` на `727dfe9`: только csproj, документы и исторические задачи/POC).
- Android-манифест не задаёт `package`: пакет берётся из `ApplicationId`.
- Условное свойство MSBuild по `TargetPlatformIdentifier == 'android'` после общего меняет только Android, как уже сделано для `SupportedOSPlatformVersion` в этом же csproj.
- Новый пакет получает новую песочницу и хранилище ключей; старое приложение остаётся рядом до удаления пользователем.

## План
1. Тест (сначала красный): `ImageGen.Presentation.Tests/Design/MauiProjectTests.cs` — в csproj есть `ApplicationId` `com.eliseev.imagegen` с условием на платформу `android`, общий `ApplicationId` не изменён.
2. csproj: условный `ApplicationId` для Android.
3. Сборка `-f net10.0-android`, проверка пакета `aapt2 dump badging`.
4. Документы: `README.md`, `docs/knowledge/dev-environment.md` («Релиз»), комментарий в 030.

Файлы: `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiProjectTests.cs` (новый), `README.md`, `docs/knowledge/dev-environment.md`, `tasks/backlog/030-app-identity.md`.

## Отчёт
Изменено:
- `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`: условный `ApplicationId` `com.eliseev.imagegen` для Android после общего; остальные цели прежние.
- `ImageGen/ImageGen.Presentation.Tests/Design/MauiProjectTests.cs` (новый): тест сначала красный, затем зелёный.
- `README.md`, `docs/knowledge/dev-environment.md` («Релиз»): новый пакет — отдельное приложение, данные не переносятся.
Проверено: Presentation.Tests 416/416; `dotnet build -f net10.0-android` — 0 предупреждений; `aapt2 dump badging` — `package: name='com.eliseev.imagegen'`; `scripts/check.sh`. Не проверено: Windows-цель (CI), установка на устройстве (вместе с первым релизом с ключом 163).

## Комментарии

### 2026-10-31, владелец (в чате, записал claude-audit)
Сменить идентификатор в том же релизе, что и постоянный ключ (выбрано в вопросе; путь «выделить задачу, спланировать и сделать» одобрен). Допущение: переноса данных старого пакета не нужно — данные владельца уже стёрты переустановкой.

### 2026-10-31, claude-audit
Сделано; установка проверяется вместе с первым релизом, подписанным ключом 163.
