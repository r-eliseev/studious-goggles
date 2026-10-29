---
id: 115
status: planned
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
- Source XML tests проверяют manifest, оба modes и полный expected domain set.
- `docs/knowledge/maui.md` фиксирует privacy policy; F7.6 остаётся будущим явным зашифрованным export/backup.

## Анализ
`allowBackup=false` — официальный opt-out из Auto Backup. Android отдельно определяет D2D и Android 12+ `<device-transfer>` rules; документация не доказывает, что OEM игнорирует `allowBackup=false`. Явные deny-all rules используются как defense in depth и проверяемая защита от drift конфигурации. Задача не реализует пользовательский backup.

`docs/knowledge/maui.md` также меняют 088, 095, 098, 100: перед стартом rebase, при конфликте сохранить независимые добавления; это не функциональные зависимости.

## План
Для Haiku/Luna: точная deny-all policy, без UI/iOS. Если Android resource merger отвергает domain, записать exact build error и `blocked`, не удалять rule молча.

Прочитай: полный путь `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, AndroidManifest, Android official page выше, `MauiStylesTests.MauiRoot`, `docs/knowledge/maui.md`.

1. Добавить legacy `Platforms/Android/Resources/xml/backup_rules.xml`: `<full-backup-content>` с excludes для root/file/database/sharedpref/external/device_root/device_file/device_database/device_sharedpref; no includes.
2. Добавить Android 12+ `data_extraction_rules.xml`: cloud-backup и device-transfer; каждый содержит excludes для root/file/database/sharedpref/external и device_root/device_file/device_database/device_sharedpref; no includes.
3. Manifest: `allowBackup=false`, `fullBackupContent=@xml/backup_rules`, `dataExtractionRules=@xml/data_extraction_rules`.
4. Новый `Presentation.Tests/Design/AndroidBackupPolicyTests.cs`: repository root как MauiStylesTests; parse XDocument; exact manifest attrs; parse both resources; assert no include and expected domains in each applicable section.
5. `dotnet build ... -c Release -f net10.0-android`; найти final merged manifest только под `obj/Release/net10.0-android` по build output, записать path, parse attrs. Не считать source/intermediate duplicate final output.
6. Обновить один пункт `docs/knowledge/maui.md`, rebase-сохраняя изменения других задач.

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
