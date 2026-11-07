---
id: 243
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T12:23:48Z
created: 2026-11-07
---

# UI-тесты: локальный мок провайдера и сценарий «сессия → генерация»

## Описание
Решение владельца 2026-11-07: после smoke — сценарии через локальный мок OpenAI-совместимого провайдера (ADR 0029, `docs/specs/ui-e2e.md`). Без ключа приложение не проходит дальше экрана первого запуска (`SessionsPageModel.OnAppearingAsync` → `ApiKeyPage`, ключ проверяется по сети), поэтому мок — единственный путь дальше без настоящих ключей и денег. Приложение уже поддерживает свои OpenAI-совместимые провайдеры через `providers.json` в папке данных (`ProviderConfigLoader.UserFileName`), и на экране первого запуска есть выбор провайдера (`apikey.provider`).

Неизвестно (2026-11-07), выяснить первым шагом:
- как положить `providers.json` в папку данных Release APK. `AppDataDirectory` — внутреннее хранилище, `adb run-as` работает только для отладочных сборок. Предпочтительный путь (обсуждено с владельцем 2026-11-07): эмулятор с образом `google_apis` (без Play) разрешает `adb root`, поэтому файл кладётся прямо в `/data/data/com.eliseev.imagegen/files/`, владелец и права те же, что у папки приложения, плюс `restorecon`. Сборка не меняется. Работает ли это, проверить;
- примет ли Release-сборка HTTPS мока с тестовым корневым сертификатом, установленным в системное хранилище эмулятора. Решение владельца 2026-11-07: мок работает по HTTPS, а не по HTTP. Так не нужны ни исключение для HTTP без TLS в Android, ни правка приложения. Release-приложение с targetSdk 24+ доверяет только системным сертификатам, пользовательские не принимает, поэтому сертификат кладётся в `/system/etc/security/cacerts`. Начиная с API 34 системные сертификаты лежат в APEX `com.android.conscrypt`, и подменить их сложнее. Поэтому эмулятор берётся с API 33 (`google_apis`, x86_64, `-writable-system`); minSdk приложения 26, это подходит;
- какие запросы приложение делает к провайдеру для проверки ключа, списка моделей, чата с вызовом инструмента генерации и генерации картинки (по коду `OpenAiCompatibleClient` и чат-клиента).

Зависит от: 240 (и 241 — для Windows-варианта сценария).
Среда: Linux (мок и его модульные тесты), GitHub Actions (сценарий).

## Критерии готовности
- Шаг 1, факты: в «Комментарии» и `docs/knowledge/ui-e2e.md` записать:
  - запросы приложения к OpenAI-совместимому провайдеру (метод, путь, нужные поля ответа);
  - способ доставить `providers.json` на Android. Варианты в порядке предпочтения:
    1. `adb root` на эмуляторе `google_apis` и `adb push` в `files/`. Порядок: `adb root`; `adb shell mkdir -p /data/data/com.eliseev.imagegen/files`; `adb push e2e-providers.json /data/data/com.eliseev.imagegen/files/providers.json`; `adb shell chown -R $(adb shell stat -c %u:%g /data/data/com.eliseev.imagegen) /data/data/com.eliseev.imagegen/files`; `adb shell restorecon -R /data/data/com.eliseev.imagegen/files`. Фикстура повторяет это после каждого `mobile: clearApp`, потому что `clearApp` стирает файл;
    2. Release со свойством отладки, если такое есть в .NET Android (проверить по документации), и `run-as`;
    3. отдельная отладочная сборка `-c Debug -p:EmbedAssembliesIntoApk=true`.
  - ключ мока подложить нельзя: `SecureStorage` на Android шифрует его ключом Android Keystore устройства. Ключ вводится через экран первого запуска, и это заодно проверяет сам первый запуск;
  - HTTPS (основной путь):
    1. В начале job создать одноразовый корневой сертификат и сертификат сервера через `openssl` во временной папке runner (`$RUNNER_TEMP`). У сертификата сервера в SAN — `DNS:localhost`. Срок — 2 дня. Закрытые ключи в репозиторий и артефакты не попадают.
    2. Мок слушает `https://0.0.0.0:5443` с этим сертификатом: `Kestrel:Certificates:Default:Path` / `KeyPath` из переменных окружения.
    3. В эмуляторе: `adb root`, `adb remount` (если просит перезагрузку — `adb reboot`, дождаться `sys.boot_completed`, затем снова `adb root` и `adb remount`). Затем `adb push <ca>.pem /system/etc/security/cacerts/<hash>.0`, где `<hash>` = `openssl x509 -subject_hash_old -noout -in <ca>.pem`, `chmod 644`, перезагрузка.
    4. `adb reverse tcp:5443 tcp:5443`, `baseAddress` = `https://localhost:5443/v1`.
    Проверка: `adb shell` + `curl` в эмуляторе недоступен, поэтому проверяет сам первый шаг сценария: ключ принят, ошибки сети нет. Если `remount` на API 33 не работает, записать вывод и попробовать вариант Б.
  - Вариант Б (запасной): HTTP на `http://localhost:5080`. `ProviderRegistry` пускает `http` только для `localhost`. Выяснить, пускает ли Release cleartext к `localhost`. Если нет — `network_security_config` только для `localhost` и только при свойстве сборки `ImageGenE2E=true`, чтобы обычный Release не менялся. Это правка сборки, поэтому сначала `blocked` и вопрос владельцу.
  - Windows (241): корневой сертификат — `Import-Certificate -CertStoreLocation Cert:\LocalMachine\Root` на runner. У runner есть права администратора.
  Выбранный вариант записать в ADR 0029: дописать пункт в «Решение» с датой.
  Если доставка или HTTP требуют правок, которые меняют обычный Release, остановиться: `blocked` и вопрос владельцу.
- Мок — проект `ImageGen/ImageGen.E2E.MockProvider`:
  - ASP.NET Core minimal API, `net10.0`, адрес и порт из `--urls`, сертификат HTTPS из переменных окружения (шаг 1);
  - отвечает фиксированными данными на запросы из шага 1: ключ `e2e-mock-key` верный, остальные дают 401;
  - одна чат-модель `e2e/assistant` и одна модель картинок `e2e/image`;
  - на первое сообщение отвечает вызовом инструмента генерации с промптом из сообщения, на результат инструмента — текстом «Done»;
  - картинка — PNG 64×64 из ресурса проекта;
  - промпт не переписывается и модель не подменяется: мок отвечает только своими моделями.
  Модульные тесты мока (xUnit v3, как остальные не-E2E тесты): `WebApplicationFactory` и проверка каждого маршрута.
- `providers.json` для E2E — `ImageGen/ImageGen.E2E.Common/e2e-providers.json`: провайдер `e2e-mock`, `baseAddress` на мок, модели по формату `builtin-providers.json`; проходит валидацию `ProviderRegistry` (тест в `ImageGen.Core.Tests`: загрузить файл через `ProviderConfigLoader` и проверить, что ошибок нет).
- Android-тест `MockSessionTests.FirstRunWithMock_GeneratesImage` (в `ImageGen.E2E.Android`):
  1. доставить `providers.json` (способ из шага 1), выбрать провайдера `e2e-mock`, ввести ключ, выбрать модель ассистента;
  2. на экране сессий нажать `sessions.new`, в чате ввести «a red square» в `chat.input` и нажать `chat.send`;
  3. в `chat.feed` в течение 60 с появляется карточка картинки и текст «Done».
  Если для карточки нет id, добавить его отдельным шагом с константой в `TestIds` и проверкой в `AutomationIdsTests` (как в 239).
- Workflow: мок запускается в job `test-android` до тестов (`dotnet run --project ImageGen/ImageGen.E2E.MockProvider -- --urls https://0.0.0.0:5443 &` с сертификатом из шага 1, ожидание готовности через `curl --cacert`), установка корневого сертификата в эмулятор, `adb reverse tcp:5443 tcp:5443`; эмулятор этого job — API 33 с `emulator-options: -writable-system -no-window -gpu swiftshader_indirect -noaudio -no-boot-anim`; лог мока — в артефакт.
- Windows — тот же сценарий в `ImageGen.E2E.Windows`, если 241 работает; иначе пропустить и записать.

## Анализ
Справочно: исходная спецификация владельца — `docs/specs/ui-e2e-original/` (docx и md); при расхождении действуют ADR 0029 и `docs/specs/ui-e2e.md`.
- Ядро: `ImageGen/ImageGen.Core/Providers/ProviderConfigLoader.cs`, `ProviderRegistry.cs`, `builtin-providers.json` (формат), `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, чат-клиент OpenAI-совместимого провайдера (`rg -n "chat/completions" ImageGen/ImageGen.Core`).
- Presentation: `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs` (`SaveAsync`, выбор провайдера и модели при первом запуске), `SessionsPageModel.cs` (`OnAppearingAsync`).
- MAUI: `ImageGen/ImageGen.Maui/Services/MauiAppPaths.cs` (`FileSystem.Current.AppDataDirectory`), `ImageGen/ImageGen.Maui/Platforms/Android/AndroidManifest.xml`.

## План
1. Факты (шаг 1) и решение о доставке и HTTP; при необходимости — `blocked`.
2. Мок по TDD: маршрут за маршрутом.
3. `e2e-providers.json` и тест валидации.
4. Android-сценарий и workflow; прогон в CI.
5. Windows-сценарий (если 241 зелёный).

Файлы:
- проект мока: `ImageGen/ImageGen.E2E.MockProvider/` (новый) и `ImageGen/ImageGen.E2E.MockProvider.Tests/` (новый);
- `ImageGen/ImageGen.E2E.Common/e2e-providers.json` (новый), `ImageGen/ImageGen.E2E.Common/TestIds.cs`;
- тесты: `ImageGen/ImageGen.Core.Tests/Providers/E2eProvidersConfigTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/E2E/AutomationIdsTests.cs`;
- E2E: `ImageGen/ImageGen.E2E.Android/MockSessionTests.cs` (новый), `ImageGen/ImageGen.E2E.Windows/MockSessionTests.cs` (новый);
- при варианте с конфигом сети: `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, `ImageGen/ImageGen.Maui/Platforms/Android/Resources/xml/e2e_network_security_config.xml` (новый);
- `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` (id карточки);
- общие: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `.github/workflows/ui-e2e.yml`;
- документация: `docs/knowledge/ui-e2e.md`, `docs/decisions/0029-ui-e2e-tests.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.E2E.MockProvider.Tests/ImageGen.E2E.MockProvider.Tests.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.E2E.MockProvider/ImageGen.E2E.MockProvider.csproj --verify-no-changes
bash scripts/check.sh
# в GitHub Actions: job test-android с MockSessionTests
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
Создано по решению владельца «Smoke, затем мок». Проверено по коду: без ключа дальше экрана первого запуска не пройти; OpenAI-совместимые провайдеры уже подключаются через providers.json. Доставка конфига и HTTP на Android не выяснены — первый шаг задачи. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Спецификация UI-тестов прислана владельцем; выбор: NUnit + Allure, сначала smoke, затем мок. План одобрен ранее данным поручением «заапрувь все таски которые будут». Изменение `.github/workflows/` по этой спецификации владелец попросил явно (ADR 0029).
