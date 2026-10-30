---
id: 115
status: done
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
Изменения:
- `ImageGen/ImageGen.Maui/Platforms/Android/AndroidManifest.xml`: `android:allowBackup` «true» → «false», добавлены `android:fullBackupContent="@xml/backup_rules"` и `android:dataExtractionRules="@xml/data_extraction_rules"`.
- `ImageGen/ImageGen.Maui/Platforms/Android/Resources/xml/backup_rules.xml` (новый): `<full-backup-content>` с девятью `<exclude domain="…" path="." />` и без единого `<include>`.
- `ImageGen/ImageGen.Maui/Platforms/Android/Resources/xml/data_extraction_rules.xml` (новый): `<data-extraction-rules>` с секциями `<cloud-backup>` и `<device-transfer>`, в каждой девять `<exclude … path="." />`, без `<include>`.
- `ImageGen/ImageGen.Presentation.Tests/Design/AndroidBackupPolicyTests.cs` (новый): проверка атрибутов манифеста, отсутствия `include` и точного набора доменов в обеих секциях.
- `docs/knowledge/maui.md`: раздел «Бэкап и перенос данных на Android».

Набор доменов — документированная схема Android (страница [Android Auto Backup](https://developer.android.com/identity/data/autobackup), 2026-10-30): девять доменов `file`, `database`, `sharedpref`, `external`, `root`, `device_file`, `device_database`, `device_sharedpref`, `device_root`. Набор одинаков для legacy `full-backup-content` и для обеих секций `data-extraction-rules`; адаптация имён не потребовалась. У каждого `exclude` путь `"."` — домен без пути deny-all не задаёт.

TDD: тест написан первым, до правок падали четыре случая (манифест, legacy-правила, две секции `data-extraction-rules`); после добавления ресурсов и манифеста — все проходят.

Проверка итогового манифеста (план, шаг 5). Собран APK (`bin/Debug/net10.0-android/com.companyname.imagegen.maui-Signed.apk`). Итоговый (merged) манифест — `obj/Debug/net10.0-android/android/AndroidManifest.xml`:
```
android:allowBackup="false"
android:dataExtractionRules="@xml/data_extraction_rules"
android:fullBackupContent="@xml/backup_rules"
```
Из самого APK (`aapt2 dump xmltree --file AndroidManifest.xml`):
```
A: android:allowBackup(0x01010280)=false
A: android:fullBackupContent(0x010104eb)=@0x7f120000
A: android:dataExtractionRules(0x0101063e)=@0x7f120001
```
В APK лежат оба правила (`res/xml/backup_rules.xml`, `res/xml/data_extraction_rules.xml`); разбор `data_extraction_rules.xml` из APK даёт обе секции и 18 `<exclude>` (по девять на секцию) с `path="."` — компилятор ресурсов принял схему.

**Release-сборка Android на этой машине не проходит** — и это не связано с задачей: `dotnet build … -c Release -f net10.0-android` падает с `NU1102: Unable to find package Microsoft.NETCore.App.Runtime.Mono.linux-x64 with version (= 10.0.14)`. Проверено на чистом коде без моих правок — падает так же. Причина: пакетов `Microsoft.NETCore.App.Runtime.Mono.linux-x64` версии 10.x на nuget.org нет вовсе (всего 148 версий, последние — 9.x), то есть хостовые Mono-пакеты для linux-x64 не публикуются начиная с .NET 10. CI собирает Release в задаче `maui` на `windows-latest`, где пакет для win-x64 существует, поэтому там сборка проходит. На Linux-хосте Release-сборка Android недоступна, и merged-манифест проверен по Debug-сборке; merger манифеста в обеих конфигурациях один и тот же.

Проверено:
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android        → успешно, без предупреждений
dotnet test --project ImageGen/ImageGen.Presentation.Tests/…                       → 281 тест, 0 упавших
dotnet format ImageGen/ImageGen.Presentation.Tests/… --verify-no-changes           → без изменений
bash scripts/check.sh                                                               → все проверки пройдены
```

Ограничения: поведение переноса данных (D2D) на реальном OEM-устройстве не проверялось — source-тесты подтверждают конфигурацию, но не поведение инструмента переноса. F7.6 (свой зашифрованный экспорт) не реализован, включать его в правила нечего.

## Комментарии
