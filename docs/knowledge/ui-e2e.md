# Знания о MAUI UI E2E

## Preflight 238

Первые два прогона через `workflow_dispatch` на ветке `task/217-embedding-provider-model-name`, commit `fa3187de662ca9fa6a0e25a78466c746245fac79`: [run 42366477520](https://github.com/r-eliseev/studious-goggles/actions/runs/42366477520), [run 42366566469](https://github.com/r-eliseev/studious-goggles/actions/runs/42366566469). Android APK и unpackaged Windows app собраны в обоих запусках. Windows preflight успешен в обоих. Android preflight второго запуска установил Appium 3.9.0 и UiAutomator2 8.7.3, но doctor завершился кодом 1, потому что emulator ещё не был в PATH до emulator-runner. Исправили запуск doctor после старта эмулятора.

### Windows

Проверено в runs 42366477520, 42366566469 и 42367302330. Runner `runneradmin`, интерактивная `console` сессия ID 2 Active; resolution 1024×768. Окно MAUI и AutomationId `apikey.entry` найдены. Кадр не однотонный: доли пикселей вне среднего цвета 0.992126 и 0.994568 в двух сохранённых отчётах (во всех успешных прогонах >1%). FFmpeg gdigrab записал открываемый MP4 длительностью 10 секунд. Каталог данных: `C:\Users\runneradmin\AppData\Local\User Name\com.eliseev.imagegen\Data`.

### Android — успешная проверка

[Run 42367302330](https://github.com/r-eliseev/studious-goggles/actions/runs/42367302330), commit `58a2a0d32a40693808d372ac236b4010635fe753`: APK `com.eliseev.imagegen-Signed.apk` собран, установлен и запущен; скриншот показывает экран Connect OpenRouter. `AutomationId` находится в XML как `resource-id="com.eliseev.imagegen:id/apikey.entry"`. `adb root` и `adb remount` успешны; screenshot и recording получены, длительность записи 19.842 с. Workflow завершился успешно, Windows preflight также успешен. Артефакты лежат в `preflight-android-reports` и `preflight-windows-reports` (14 дней).

## Allure отчёт

Workflow собирает Allure результаты Android и Windows в HTML и загружает `allure-report` на 14 дней. Скачайте артефакт из раздела Artifacts нужного Actions run и распакуйте его; для просмотра запустите `python3 -m http.server 8000 --directory <папка-отчёта>` и откройте `http://localhost:8000`. `file://` не проверен: в локальной среде нет браузера.

## Интегрированный прогон 42374038475

Сборки Android APK и unpackaged Windows app прошли; Android preflight установил и запустил APK на API 33. Android UI test не стартал из-за лишнего `--` в синтаксисе `dotnet test` с MTP. Windows UI test действительно запустил окно через FlaUI и выполнил 3 сценария: один прошёл, два упали из-за проверок доступности/статуса в тестах. Исправления и полный итог приведены ниже.

## Полный UI E2E прогон 42378967997

На коммите `13db79eb50bfa527997703fedad3768f623028a8` прошли Android APK build, Android preflight, три Android smoke-теста, Windows unpackaged build, Windows preflight, два Windows UI smoke-теста, тест `VideoRecorder` и общий report job. TRX показывает все 3 Android теста и все 3 Windows fixture tests со статусом `Passed`; Allure HTML содержит результаты и вложения обеих платформ.

Артефакт `android-e2e`: три per-test MP4 H.264 320×640, проверены `ffprobe`; также logcat, metadata и Allure attachments. Артефакт `windows-e2e`: два per-test MP4 H.264 1024×768, проверены `ffprobe`; также app/FFmpeg логи, metadata и Allure attachments. Артефакт `allure-report` содержит suite summary с пятью прошедшими UI-тестами и прошедшим тестом аргументов FFmpeg. Все артефакты workflow хранятся 14 дней.
