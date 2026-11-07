---
id: 238
status: approved
assignee:
claimed:
created: 2026-11-07
---

# UI-тесты: проверка инфраструктуры CI (preflight) Android и Windows

## Описание
Первый этап спецификации UI-тестов (ADR 0029, `docs/specs/ui-e2e.md`): до написания тестов доказать на раннерах GitHub, что всё нужное работает, и записать факты. Неизвестно (2026-11-07): есть ли у `windows-latest` (Windows Server 2025) интерактивная сессия и не чёрный ли захват экрана, разрешение экрана; какой атрибут дерева UI Android несёт `AutomationId` MAUI (документация MAUI: `MobileBy.Id`, то есть `resource-id`; проверить); где лежит папка данных unpackaged MAUI на Windows; сколько длится прогон.

Зависит от: 239 (нужны `AutomationId` в разметке).
Среда: GitHub Actions (запуск workflow через `workflow_dispatch` из ветки задачи); локально — только правка YAML и `scripts/check.sh`.

## Критерии готовности
- Новый `.github/workflows/ui-e2e.yml`, `on: workflow_dispatch` и `pull_request` с `paths: ['.github/workflows/ui-e2e.yml']` (на этом этапе не на каждый PR), `permissions: contents: read`, `concurrency` по ref с отменой.
- Job `build-android` (`windows-latest`): `setup-dotnet` 10.0.x, `dotnet workload install maui-android`, `dotnet publish ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -c Release -f net10.0-android -p:AndroidPackageFormat=apk -o publish/android` (без ключа подписи: подпись отладочным ключом, как в `release.yml` без секретов), загрузка `publish/android/*-Signed.apk` артефактом `android-apk`.
- Job `build-windows` (`windows-latest`): `dotnet workload install maui-windows`, `dotnet publish … -f net10.0-windows10.0.19041.0 -p:ImageGenWindowsRid=win-x64 -p:SelfContained=true -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -o publish/win-x64` (как в `release.yml`), артефакт `windows-app`.
- Job `preflight-android` (`ubuntu-latest`, `needs: build-android`, `timeout-minutes: 45`): включение KVM (udev-правило из README `reactivecircus/android-emulator-runner`), `actions/setup-node@v4` с Node 24, `npm install -g appium@3.9.0`, `appium driver install uiautomator2@8.7.3`, `appium driver doctor uiautomator2` (вывод в лог), затем `reactivecircus/android-emulator-runner@v2` (`api-level: 33`, `target: google_apis`, `arch: x86_64`, `emulator-options: -writable-system -no-window -gpu swiftshader_indirect -noaudio -no-boot-anim`). API 33, потому что на 34+ системные сертификаты в APEX, а задаче 243 нужен свой корневой сертификат. Дополнительно проверить и записать, работают ли `adb root` и `adb remount` на этом образе со скриптом: `adb install -r <apk>`, `adb shell monkey -p com.eliseev.imagegen -c android.intent.category.LAUNCHER 1`, пауза 20 с, `adb shell uiautomator dump /sdcard/ui.xml` и `adb pull` в `reports/preflight-android/ui.xml`, `adb exec-out screencap -p > reports/preflight-android/screen.png`, `adb shell screenrecord --time-limit 10 /sdcard/r.mp4` и `adb pull`, `adb logcat -d --pid=$(adb shell pidof com.eliseev.imagegen) > …/logcat.txt`. Важно: `script` эмулятор-раннера выполняет каждую строку отдельной командой — многострочную логику вынести в файл `ImageGen/ImageGen.E2E.Common/ci/android-preflight.sh` и вызывать одной строкой (`scripts/` не трогать).
- Job `preflight-windows` (`windows-latest`, `needs: build-windows`, `continue-on-error: true`): `choco install ffmpeg -y --no-progress`; PowerShell-скрипт `ImageGen/ImageGen.E2E.Common/ci/windows-preflight.ps1`: печать `whoami`, `query session` (ошибку не считать падением), разрешение основного экрана (`System.Windows.Forms.Screen`); запуск `publish/win-x64/ImageGen.Maui.exe`; ожидание окна до 60 с через UIAutomationClient (`AutomationElement.RootElement.FindFirst` по имени процесса/`ProcessId`); поиск `apikey.entry` по `AutomationIdProperty`; скриншот рабочего стола (`CopyFromScreen`) в `reports/preflight-windows/desktop.png`; проверка «не однотонный» — доля пикселей, отличающихся от среднего цвета больше чем на 16 по любому каналу, > 1 % (по сетке 1 пиксель из 16); FFmpeg 10 с `gdigrab` в `recording.mp4` с логом `ffmpeg.log`; после закрытия приложения — список `$env:LOCALAPPDATA` и найденная папка с `logs\imagegen-*.log` (путь папки данных). Итог — `reports/preflight-windows/summary.json` с полями: session, resolution, windowFound, automationIdFound, nonUniformRatio, ffmpegOk, dataDir.
- Оба preflight-job загружают `reports/` артефактом при любом исходе (`if: always()`, `retention-days: 14`).
- Запустить workflow на ветке задачи (`workflow_dispatch` через GitHub; если нет прав — попросить владельца в «Комментариях»), скачать артефакты и записать факты в новый `docs/knowledge/ui-e2e.md`: время каждого job, атрибут, в котором оказался `apikey.entry` в `ui.xml` Android (`resource-id` вида `com.eliseev.imagegen:id/apikey.entry` или другой — привести строку XML), видно ли приложение на скриншоте эмулятора, всё из `summary.json`, ссылка на прогон. Факт про `resource-id` перенести в ADR 0029 (раздел «Факты» — дописать строкой «Проверено задачей 238»).
- `docs/index.md`: строка про `knowledge/ui-e2e.md`.

## Анализ
`.github/workflows/release.yml` (строки ≈108–125 — публикация Windows, ≈203–232 — APK), `.github/workflows/build.yml` (комментарий про квоту диска на Ubuntu), ADR 0029, `docs/specs/ui-e2e.md`. Версии (npm, 2026-11-07): `appium` latest 3.9.0; `appium-uiautomator2-driver` 8.7.3 — последняя для Appium 3 (9.x требует Appium 4 beta).

## План
1. Скрипты preflight и workflow (сборки, затем preflight-jobs).
2. `scripts/check.sh`; push; запуск workflow; при падении сборки — исправлять только YAML/скрипты preflight; до 3 попыток, потом записать в «Комментарии» и `blocked`.
3. Факты в `docs/knowledge/ui-e2e.md` и ADR 0029.

Файлы: `.github/workflows/ui-e2e.yml` (новый), `ImageGen/ImageGen.E2E.Common/ci/android-preflight.sh` (новый), `ImageGen/ImageGen.E2E.Common/ci/windows-preflight.ps1` (новый), `docs/knowledge/ui-e2e.md` (новый), `docs/decisions/0029-ui-e2e-tests.md`, `docs/index.md`.

### Проверка
```
bash scripts/check.sh
# в GitHub Actions: workflow «ui-e2e» на ветке задачи, оба preflight-job и артефакты
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-07, claude-audit
Создано по спецификации UI-тестов владельца: этап «preflight инфраструктуры». Версии Appium/UiAutomator2 проверены в npm 2026-11-07. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Спецификация UI-тестов прислана владельцем; выбор: NUnit + Allure, сначала smoke, затем мок. План одобрен ранее данным поручением «заапрувь все таски которые будут». Изменение `.github/workflows/` по этой спецификации владелец попросил явно (ADR 0029).
