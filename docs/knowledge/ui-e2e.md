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

## Как смотреть видео и скриншоты прогонов

Агент видео не воспроизводит, но видит картинки. Поэтому видео разбирается на кадры через `ffmpeg`/`ffprobe`: в облачной сессии они есть в `/usr/bin`. Временные файлы — только в `tmp/`.

1. **Найти прогон и артефакты.** MCP GitHub: `actions_list` → `list_workflow_runs` (workflow `ui-e2e.yml` или `apple-ci.yml`, ветка `main`), затем `list_workflow_run_artifacts`. Нужные артефакты:
   - Android — `android-e2e`;
   - Windows — `windows-e2e`;
   - iOS — `ios-smoke-maui`, `ios-smoke-avalonia`;
   - macOS — `macos-e2e-smoke`;
   - отчёт — `allure-report`.
2. **Скачать.** `actions_get` → `download_workflow_run_artifact` даёт временную ссылку (около 10 минут). Дальше `curl -sS -o tmp/runs/<имя>.zip '<ссылка>'` и `unzip -q tmp/runs/<имя>.zip -d tmp/runs/<имя>`. Человеку проще так: страница прогона → Artifacts → скачать.
3. **Сводка по ролику:**
   ```
   ffprobe -v error -show_entries format=duration:stream=width,height -of csv=p=0 <файл>.mp4
   ```
   Затем сравнить с `durationMs` из `metadata.json` той же папки.
4. **Лист кадров (один PNG на ролик, 8 кадров с отметкой времени):**
   ```
   d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 rec.mp4)
   ffmpeg -v error -y -i rec.mp4 -vf "fps=$(python3 -c "print(8/max($d,0.5))"),scale=240:-1,drawtext=text='%{pts\:hms}':x=5:y=5:fontcolor=yellow:fontsize=18:box=1:boxcolor=black,tile=8x1" -frames:v 1 sheet.png
   ```
   Для Windows (1024×768) удобнее `scale=480:-1,tile=4x2`.
5. **Конец ролика** — там состояние, которое проверял тест:
   ```
   ffmpeg -v error -y -sseof -1.2 -i rec.mp4 -vf "fps=4,scale=320:-1,tile=5x1" -frames:v 1 last.png
   ```
6. **Увеличить участок** (например, текст статуса):
   ```
   ffmpeg -ss <начало> -t <длина> -i rec.mp4 -vf "crop=W:H:X:Y,fps=10,tile=4x2" -frames:v 1 zoom.png
   ```
   Координаты — в пикселях исходного кадра.
7. **Посмотреть PNG** (инструментом чтения файлов) и записать вывод в задачу. Скриншоты `screenshot.png` из артефактов смотреть так же.

### Что видно на записях (прогон [42398374909](https://github.com/r-eliseev/studious-goggles/actions/runs/42398374909), 2026-11-07)

- **Android: запись обрывается раньше конца теста.**
  - `InvalidKey_ShowsError`: тест шёл 24,9 с, ролик — 6,4 с, logcat — 7 с. Последний кадр — «Checking the key...», а текста ошибки, который проверял тест, на видео нет.
  - `Rotate_KeepsKeyScreen` заканчивается на списке сессий: экрана ключа на записи нет.
  - Видео с эмулятора имеет переменную частоту кадров и низкое разрешение (320×640). Последние доли секунды перед `StopRecordingScreen` в файл не попадают.
  - Вывод: конечное состояние теста надо фиксировать отдельно — пауза перед остановкой записи и скриншот всегда, а не только при падении. Задача 251.
- **Android: `Rotate_KeepsKeyScreen` поворачивает не тот экран.** Поворот выполняется сразу после запуска, пока открыт список сессий. Только потом приложение переходит на экран ключа, и `Find` его находит. Сценарий «поворот не ломает экран ключа» так не проверяется. Задача 251.
- **Android, первый запуск.** Около 1,6 с виден список сессий (с примерами), потом приложение переходит на «Connect OpenRouter».
- **Windows.**
  - Записывается весь рабочий стол 1024×768. За окном приложения видна консоль раннера с его JSON-логами; секретов в них не видно.
  - Ошибка «OpenRouter rejected this key.» на записи есть.
  - Поле ключа MAUI на Windows примерно 0,2 с показывает введённый ключ открытым текстом (`sk-or-e2e-invalid`) и только потом маскирует. Ключ вводился быстрым набором FlaUI. Как поле ведёт себя при вставке, не проверено. Задача 251.
