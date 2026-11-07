# 0029. UI-тесты собранного MAUI-приложения в GitHub Actions

Статус: принято
Дата: 2026-11-07

## Контекст
Владелец прислал спецификацию «Автоматизация UI-тестов .NET MAUI в GitHub Actions» (NUnit, FlaUI, Appium, Allure, видео каждого теста) и попросил её перепроверить и распланировать. Сейчас интерфейс проверяется так:
- модели страниц — в `ImageGen.Presentation.Tests`;
- Avalonia — без экрана, в `ImageGen.Avalonia.Tests`;
- MAUI — только вручную (задачи 140 и 046).

Ограничения:
- Только раннеры GitHub (`windows-latest` = Windows Server 2025, `ubuntu-latest`).
- Нагрузка `maui-android` на `ubuntu-latest` не ставится: «Disk quota exceeded» (комментарий в `.github/workflows/build.yml`). Поэтому APK собирается на Windows.
- Ключей провайдеров в CI нет (`docs/conventions.md`). Без ключа приложение показывает только экран первого запуска, потому что `SessionsPageModel.OnAppearingAsync` переходит на `ApiKeyPage`, а ключ проверяется по сети.

Факты, проверенные 2026-11-07:
- npm: `appium` latest — 3.9.0. Версия 4 есть только как `4.0.0-beta.7`.
- `appium-uiautomator2-driver` 9.x требует `appium ^4.0.0-beta.6`. Для Appium 3 последний — 8.7.3. Команда `npm install -g appium@4` из спецификации не сработает.
- Документация MAUI «UI testing with Appium» (net-maui-10.0): `AutomationId` на Android ищется через `MobileBy.Id` (resource-id), на Windows — через `AccessibilityId`. `content-desc` на Android берётся из `SemanticProperties.Description`, а оно в проекте стоит на всех элементах. Поэтому `AppiumBy.AccessibilityId("…")` из спецификации на Android найдёт не то.
- В MAUI-разметке проекта `AutomationId` нет нигде (0 вхождений). `SemanticProperties.Description` встречается 103 раза.
- Allure: `Allure.NUnit` 2.15.0 стабилен. Для xUnit v3 есть только `Allure.Xunit.v3` 3.0.0-preview.1, и ему нужен `xunit.v3.mtp-v2`.
- В образе Windows 2025 есть WinAppDriver 1.2.2009.02003 и Android SDK. FFmpeg в списке ПО образа нет. Разрешение экрана и наличие интерактивной сессии в описании образа не указаны — неизвестно.
- FlaUI.UIA3 5.0.0 (2025-02-25) собран под `net8.0-windows`, `net6.0-windows` и `net48`.

## Решение
1. **Тесты собранных приложений** — отдельные проекты `ImageGen/ImageGen.E2E.Android` и `ImageGen/ImageGen.E2E.Windows` и общий код в `ImageGen/ImageGen.E2E.Common`. Так проекты лежат рядом с остальными в `ImageGen/`, а не в `tests/`, как в спецификации.
2. **Фреймворк E2E — NUnit с `Allure.NUnit`** (решение владельца 2026-11-07). Это исключение из ADR 0004 только для E2E-проектов. Остальные тесты остаются на xUnit v3.
3. **Android — основной путь.**
   - Сборка: Release APK на `windows-latest`, как в `release.yml`.
   - Тесты: на `ubuntu-latest` с эмулятором `reactivecircus/android-emulator-runner@v2` (API 33, `google_apis`, `-writable-system`: на API 34+ системные сертификаты лежат в APEX, а моку по HTTPS нужен свой корневой сертификат — задача 243), KVM, Appium 3.9.0 и `uiautomator2@8.7.3`. Версии закреплены.
   - Поиск элементов — по `AutomationId` через `MobileBy.Id`. Какой атрибут дерева UI его несёт, проверяется в задаче 238.
4. **Windows — экспериментальный путь.**
   - Драйвер: FlaUI UIA3, без Appium и WinAppDriver.
   - Видео: FFmpeg `gdigrab`, установка шагом workflow.
   - Задача job — сделать предварительную проверку (preflight): есть ли сессия, отвечает ли UI Automation и не чёрный ли кадр.
   - Пока стабильность не доказана, job помечен `continue-on-error: true`.
5. **Сценарии.**
   - Сначала smoke без ключей: экран первого запуска, неверный ключ и ошибка, доступность элементов.
   - Затем локальный мок OpenAI-совместимого провайдера по HTTPS с одноразовым тестовым корневым сертификатом, подключённый через `providers.json` (задача 243; HTTPS — решение владельца 2026-11-07). Это настоящий путь кода без ключей и без денег.
   - Живой OpenRouter в CI не используется.
6. **Доказательства по каждому тесту** — в папке `reports/<платформа>/<тест>/`:
   - всегда: видео;
   - при падении: скриншот и дерево UI;
   - логи: logcat по PID приложения, лог Appium, лог FFmpeg и лог приложения.
   Всё это прикладывается к Allure. Артефакты загружаются при любом исходе (`if: always()`).
7. **Workflow** — новый `.github/workflows/ui-e2e.yml`, отдельный от `build.yml`.
   - Запуск: `workflow_dispatch`, `pull_request`, `push` в `main`.
   - Jobs: `build-android`, `build-windows`, `test-android`, `test-windows`, `report`. Jobs тестов — отдельные проверки в PR.
   - Владелец явно попросил этот workflow (запрет менять `.github/workflows/` без просьбы снят для него).
- Проверено задачей 238: Windows runner имеет активную console-сессию и UI Automation видит окно/`apikey.entry`; см. `docs/knowledge/ui-e2e.md`. Android resource-id ещё ожидает фактической проверки после исправления preflight.

## Альтернативы
- **Appium Windows driver вместо FlaUI.** Нужен WinAppDriver (на образе он есть, но документация MAUI требует именно 1.2.1) и ещё один процесс. FlaUI проще и работает напрямую через UIA3.
- **xUnit v3 и Allure-превью.** Один фреймворк на весь проект, но адаптер в превью. Владелец выбрал NUnit.
- **xUnit v3 без Allure** (TRX и сводка). Тогда не будет HTML-отчёта с видео, а спецификация его требует.
- **Сборка APK на Ubuntu.** Падает по квоте диска.
- **Живой OpenRouter.** Это деньги на каждый прогон и зависимость от сети и провайдера.
- **Avalonia Android теми же тестами.** Отложено. Проект Android-тестов не завязан на MAUI, его можно направить на второй APK отдельной задачей после 240.

## Последствия
- В репозитории появляется второй тестовый фреймворк. В `docs/conventions.md` нужно указать, что NUnit — только для E2E.
- В MAUI-разметке появляются `AutomationId` с постоянными смысловыми именами (задача 239). Они не локализуются.
- CI становится дольше: насколько — неизвестно, измерит задача 238.
- Видео может показать ввод ключа. В тестах — только фиктивные ключи (`sk-or-e2e-invalid`, ключ мока). Настоящие ключи E2E не получают.
