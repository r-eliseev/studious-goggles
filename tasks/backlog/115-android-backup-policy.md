---
id: 115
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Исключить приватные данные из Android backup и transfer

## Описание
Manifest разрешает backup без rules; app-private SQLite, images, logs и debug trace могут попасть в cloud backup/device transfer. Политика: приложение не участвует в автоматическом backup, а rules явно deny-all для Android versions/modes, где OEM transfer может учитывать отдельную конфигурацию.

Источник платформенного поведения: [Android Auto Backup](https://developer.android.com/identity/data/autobackup). Сведения пересказаны, а не процитированы дословно.

Зависит от: —
Среда: Linux или Windows с Android SDK.

## Критерии готовности
- Source manifest: `allowBackup=false`, legacy `fullBackupContent` указывает deny-all rules, Android 12+ `dataExtractionRules` указывает deny-all cloud/device-transfer rules.
- Rules исключают все поддержанные app data domains, включая credential/device protected storage; ни одного include.
- Release Android build и final merged manifest содержат эти references/false.
- Source XML tests проверяют manifest, оба modes и полный expected domain set документированной схемы установленного SDK; адаптация имени domain сохраняет deny-all и фиксируется в отчёте.
- `docs/knowledge/maui.md` фиксирует privacy policy; F7.6 остаётся будущим явным зашифрованным export/backup.

## Анализ
`allowBackup=false` — официальный opt-out из Auto Backup. Android отдельно определяет D2D и Android 12+ `<device-transfer>` rules; документация не доказывает, что OEM игнорирует `allowBackup=false`. Явные deny-all rules используются как defense in depth и проверяемая защита от drift конфигурации. Задача не реализует пользовательский backup.

`docs/knowledge/maui.md` также меняют 088, 095, 098, 100: перед стартом rebase, при конфликте сохранить независимые добавления; это не функциональные зависимости.

## План
Для Haiku/Luna: точная deny-all policy, без UI/iOS. Рутинные ошибки resource path/build исправлять по Android SDK без ослабления deny-all. Набор domains сверять с официальной схемой для legacy и Android12+ ресурсов отдельно. Если SDK отклоняет имя из плана, исправить resource/schema/API-адаптацию и ожидаемый набор теста по документации, сохранив deny-all для всех поддержанных app-data domains; не исключать режим cloud/device-transfer, не добавлять include и не разрешать backup. В отчёте дать ссылку, фактические supported domains и результат merged-manifest проверки. Это техническая адаптация внутри уже принятой privacy-политики, нового решения владельца не требует. Ошибку сборки исправлять, не объявлять такой режим защищённым без проверки. Отсутствие Android SDK — неподходящая среда, задачу не занимать.

Прочитай: полный путь `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, AndroidManifest, Android official page выше, `MauiStylesTests.MauiRoot`, `docs/knowledge/maui.md`.

1. Для каждого exclude задать `path="."`: domain без пути не задаёт deny-all. Добавить legacy `Platforms/Android/Resources/xml/backup_rules.xml`: `<full-backup-content>` с excludes для root/file/database/sharedpref/external/device_root/device_file/device_database/device_sharedpref; no includes.
2. Добавить Android 12+ `data_extraction_rules.xml`: cloud-backup и device-transfer; каждый содержит excludes для root/file/database/sharedpref/external и device_root/device_file/device_database/device_sharedpref; no includes.
3. Manifest: `allowBackup=false`, `fullBackupContent=@xml/backup_rules`, `dataExtractionRules=@xml/data_extraction_rules`.
4. Новый `Presentation.Tests/Design/AndroidBackupPolicyTests.cs`: repository root как MauiStylesTests; parse XDocument; exact manifest attrs; parse both resources; assert no include, exact expected domain set и path="." у каждого exclude в каждой applicable section. Source tests подтверждают конфигурацию, но не доказывают поведение OEM transfer на устройстве.
5. `dotnet build ... -c Release -f net10.0-android`; найти final merged manifest только под `obj/Release/net10.0-android` по build output, записать path, parse attrs. Не считать source/intermediate duplicate final output.
6. Обновить один пункт `docs/knowledge/maui.md`, rebase-сохраняя изменения других задач: app data не участвуют в Auto Backup, explicit rules для transfer, пользовательский F7.6 ещё не реализован. Не утверждать проверку на OEM-устройстве без фактического опыта.
7. После проверок заполнить «Отчёт» (изменения, команды, ограничения), выполнить `scripts/release-task.sh 115` и `bash scripts/check.sh`; обновить PR по `docs/tasks.md`. Код менять маленькими шагами TDD.

### Проверка
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -c Release -f net10.0-android
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Maui/Platforms/Android/AndroidManifest.xml`, `ImageGen/ImageGen.Maui/Platforms/Android/Resources/xml/backup_rules.xml` (новый), `ImageGen/ImageGen.Maui/Platforms/Android/Resources/xml/data_extraction_rules.xml` (новый), `ImageGen/ImageGen.Presentation.Tests/Design/AndroidBackupPolicyTests.cs` (новый), `docs/knowledge/maui.md`

## Отчёт

## Комментарии
