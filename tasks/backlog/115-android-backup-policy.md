---
id: 115
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Запретить Android backup приватных данных приложения

## Описание
Найдено privacy-review 2026-10-29. Android manifest задаёт `android:allowBackup="true"` без `dataExtractionRules`/`fullBackupContent`. В app-private data приложение хранит SQLite-чаты и промпты, сгенерированные изображения, логи и opt-in debug traces. Эти данные не должны автоматически уходить в cloud backup или device transfer без отдельной продуктовой функции.

Зависит от: —
Среда: Linux или Windows с Android SDK.

## Критерии готовности
- `android:allowBackup="false"` в production manifest.
- Для Android 12+ также явно запрещён data extraction, если MAUI/manifest merger требует отдельный атрибут; итоговый merged manifest подтверждает запрет.
- Сборка Android проходит.
- В `docs/knowledge/maui.md` записано принятое правило: автоматический backup закрыт; будущий экспорт/backup из F7.6 должен быть явным и шифрованным.
- Проверка manifest не зависит от ручного просмотра APK.

## Анализ
- Самый узкий безопасный вариант — полностью отключить backup. Исключения по каталогам сложнее и всё равно могут пропустить новый приватный файл.
- Эта задача не реализует F7.6 и не меняет iOS.
- В документации уже есть предупреждение о SecureStorage и `allowBackup=true`, но нет продуктового решения.

## План
Для исполнителя (Haiku/Luna): не добавляй backup/export UI. Не создавай XML rules, если `allowBackup=false` достаточно в merged manifest. Если Android tooling автоматически меняет значение, зафиксируй merged manifest и поставь `blocked` вместо обхода.

Прочитай: `ImageGen/ImageGen.Maui/Platforms/Android/AndroidManifest.xml`, `docs/knowledge/maui.md`, `ImageGen.Maui.csproj`.

1. В manifest заменить `android:allowBackup="true"` на `false`.
2. Собрать `net10.0-android` и найти merged manifest в `obj/`. Проверить скриптом/grep, что итоговое application имеет `allowBackup=false`.
3. Если Android 12+ tooling предупреждает о `dataExtractionRules`, добавить минимальную конфигурацию deny-all только по официальному формату Android; сначала добавить проверку, которая падает без неё. Если предупреждения нет, не создавать лишние XML-файлы.
4. Создать `ImageGen/ImageGen.Presentation.Tests/Design/AndroidManifestTests.cs`. Корень репозитория находить тем же подъёмом от `AppContext.BaseDirectory`, что `Design/MauiStylesTests.MauiRoot` (маркер `docs/design/tokens.json`). Прочитать source manifest через `XDocument` и проверить атрибут application `{http://schemas.android.com/apk/res/android}allowBackup == "false"`. Не проверять generated `obj` из unit-теста.
5. Обновить один абзац `docs/knowledge/maui.md`; не менять F7.6.

### Проверка
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Maui/Platforms/Android/AndroidManifest.xml`, `docs/knowledge/maui.md`, `ImageGen/ImageGen.Presentation.Tests/Design/AndroidManifestTests.cs` (новый)

## Отчёт

## Комментарии
