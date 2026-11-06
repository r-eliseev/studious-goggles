---
id: 197
status: approved
assignee:
claimed:
created: 2026-11-06
---

# Android-голова Avalonia: сборка в CI и APK в релизе

## Описание
ADR 0026, п. 6. Владелец явно поручил менять сборку («с билдами», 2026-11-06), поэтому правка `.github/workflows/` в этой задаче разрешена (AGENTS.md, «Запреты»). Нужно: Android-голова Avalonia собирается на каждом PR и в каждом релизе выходит подписанный APK `ImageGen-Avalonia-<метка>-android.apk` рядом с MAUI APK.

Зависит от: 195 (проект должен существовать). 196 не обязательна.
Среда: Linux или Windows (правка YAML); проверка — запуск workflow в GitHub.

## Критерии готовности
- `build.yml`: новая задача `avalonia-android` — `ubuntu-latest`, `setup-dotnet` 10.0.x, `dotnet workload install android`, `dotnet format` Android-проекта, `dotnet build … -c Release`. Если установка workload на ubuntu падает по месту на диске (как когда-то `maui-android`), перенести задачу на `windows-latest` с тем же набором шагов и записать причину комментарием в YAML и в «Отчёт».
- `release.yml`: новая задача `avalonia-android` по образцу `maui-android`: тот же шаг восстановления ключа из секретов `ANDROID_*` с тем же предупреждением при их отсутствии, `dotnet publish ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release -p:AndroidPackageFormat=apk -p:ApplicationDisplayVersion=<версия> -p:ApplicationVersion=${{ github.run_number }}` с теми же параметрами подписи, удаление ключа в `if: always()`, `cp publish/avalonia-android/*-Signed.apk "$ASSET.apk"`, `upload-artifact`. `ASSET: ImageGen-Avalonia-${{ needs.version.outputs.label }}-android`.
- Задача `publish` ждёт и `avalonia-android` (`needs`). С 2026-11-06 релиз выходит на каждый push в `main` (`version.outputs.release`), поэтому слияние PR этой задачи само выпустит релиз с Avalonia APK — проверить подпись на нём.
- Ручной запуск release (`workflow_dispatch`) выкладывает артефакт; его подпись проверена `apksigner verify --print-certs` (или `openssl` по `META-INF/*.RSA`, как в 140): сертификат `CN=ImageGen, O=eliseev`, SHA-256 `6711f8ba…10277`, `package` в манифесте — `com.eliseev.imagegen.avalonia` (`aapt dump badging` или `apkanalyzer`, если доступны; иначе записать «не проверено»).
- `README.md`: таблица пакетов и абзац о релизах упоминают второй Android APK, что он ставится рядом с MAUI, у него свои данные и ключи. `docs/knowledge/dev-environment.md`, раздел «Пакеты», — то же. `docs/status.md`, строка CI — новая задача.

## Анализ
`release.yml` — задача `maui-android` (строки ~143–200): `windows-latest`, `workload install maui-android`, шаги «Restore signing key», «Publish MAUI Android (APK)», «Remove signing key», «Pack», `upload-artifact`; `publish` с `needs: [avalonia, maui-windows, maui-android]`. `build.yml` — задачи `avalonia` (ubuntu) и `maui` (windows, `workload install maui`). Пробник 094 собирался на Linux с workload `android`, поэтому начать с ubuntu.

Пароли передаются через `env:` и префикс `env:` в MSBuild-свойствах, как в `maui-android`; не писать их в командную строку.

## План
1. `build.yml`: задача `avalonia-android` (см. «Критерии»).
2. `release.yml`: задача `avalonia-android`, копирующая шаги подписи `maui-android` (с заменой проекта, `-f` не нужен — один `TargetFramework`, папки вывода `publish/avalonia-android`, имени артефакта); `publish.needs`.
3. Открыть PR — задача `avalonia-android` в `build.yml` зелёная; после слияния владелец (или исполнитель, если есть права) запускает release вручную; скачать артефакт и проверить подпись и пакет (см. «Критерии»). Результат и SHA-256 — в «Отчёт».
4. Документация из «Критериев».

Файлы: `.github/workflows/build.yml`, `.github/workflows/release.yml`, `README.md`, `docs/knowledge/dev-environment.md`, `docs/status.md`.

### Проверка
```
bash scripts/check.sh
```
Плюс зелёная задача `avalonia-android` на PR и артефакт ручного запуска release (см. «Критерии»).

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй андроид на авалонии, с билдами»; выбрано: второе приложение рядом с MAUI, APK в каждом релизе). ADR 0026. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен («Да» на вопрос об одобрении 191–198 и PR).
