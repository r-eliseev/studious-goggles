---
id: 040
status: in-progress
assignee: claude-qnrqyt
claimed: 2026-10-28T00:00:00Z
created: 2026-10-28
---

# Avalonia: хранение ключей в системных хранилищах

## Описание
Реализовать `ISecretStore` для Avalonia-десктопа по ADR 0006: Windows — DPAPI, macOS — Keychain, Linux — Secret Service через D-Bus. Если хранилища нет, ключ живёт только в памяти, и пользователь об этом знает. Открытым текстом ключ на диск не пишется.

Зависит от: 039.
Среда: Linux (Secret Service) и Windows (DPAPI). macOS — не проверено (машины нет, см. «Комментарии»).

## Критерии готовности
- Ключ OpenRouter переживает перезапуск Avalonia-приложения на Windows и на Linux с работающим Secret Service.
- На Linux без Secret Service ключ работает до закрытия приложения, а страница ключа говорит, что его не сохранить.
- Ключа нет ни в одном файле в открытом виде (тест: после сохранения папка данных не содержит строку ключа).
- Выбор способа доступа к Secret Service и Keychain записан ADR.

## Анализ
- Встроенного защищённого хранилища у Avalonia нет (в отличие от `SecureStorage` в MAUI).
- Windows: `System.Security.Cryptography.ProtectedData` (DPAPI, область текущего пользователя) шифрует байты; результат кладётся файлом в папку данных. Пакет только для Windows: вызов под проверкой `OperatingSystem.IsWindows()`.
- Linux: Secret Service — D-Bus-интерфейс `org.freedesktop.secrets` (gnome-keyring, KWallet). Как обращаться из .NET — **неизвестно**: готовая библиотека (какая, лицензия, живая ли) или свои вызовы D-Bus (например, через пакет `Tmds.DBus`). Это настоящая неизвестность — нужен пробный код (`poc/<релиз>/040-avalonia-secret-store/`), правило `docs/tasks.md`.
- macOS: Keychain — через утилиту `security` или нативный API. Проверить негде: macOS-машины у проекта нет (неизвестно, будет ли).
- В облачной сессии и CI Secret Service обычно не запущен: это и есть сценарий «только в памяти»; для теста Secret Service можно поднять `gnome-keyring-daemon` в сессии D-Bus — работает ли это в контейнере, не проверено.

## План
1. Пробный код (до реализации): на Linux записать, прочитать и удалить секрет через Secret Service выбранным способом; зафиксировать, как определить, что службы нет. Результат и вывод — в `poc/…/040-avalonia-secret-store/` и в «Комментарии». По итогам — ADR «Доступ к системным хранилищам ключей на десктопе» (номер — следующий свободный).
2. `ImageGen.Avalonia/Services/Secrets/`:
   - `ISecretBackend` (внутренний): `IsAvailableAsync`, `GetAsync`, `SetAsync`, `RemoveAsync`;
   - `DpapiSecretBackend` (Windows): файл `secrets/<имя>.bin` в папке данных, содержимое — `ProtectedData.Protect` (CurrentUser);
   - `SecretServiceBackend` (Linux): по итогам пробного кода;
   - `KeychainSecretBackend` (macOS): по ADR; без проверки на машине помечается «не проверено»;
   - `InMemorySecretBackend`: словарь в памяти.
   - `DesktopSecretStore : ISecretStore`: при первом обращении выбирает бэкенд по ОС; если он недоступен — память; свойство `IsPersistent`.
3. Presentation: `ISecretStore` не меняется; признак «ключ не сохранится» отдаётся через новый интерфейс `ISecretPersistenceInfo` (`IsPersistent`) в Presentation, реализуется оболочками (MAUI — всегда `true`). `ApiKeyPageModel` показывает предупреждение (`AppResources`, новый ключ `ApiKeyNotPersisted`), если `false`.
4. Тест-кейсы:
   - `DpapiSecretBackend` (только Windows, тест пропускается на других ОС): запись → чтение того же значения; файл не содержит ключ в открытом виде; удаление → `null`.
   - `DesktopSecretStore` с недоступным бэкендом (подмена) → значения в памяти, `IsPersistent = false`.
   - `ApiKeyPageModel` при `IsPersistent = false` → текст предупреждения виден; при `true` → нет.
   - `SecretServiceBackend` — интеграционный тест на Linux, пропускается, если служба не найдена (чтобы CI без Secret Service был зелёным); как минимум один прогон с запущенной службой — в «Отчёте».
5. Документация: `docs/knowledge/avalonia.md` (что работает на какой ОС), ADR, `docs/architecture.md` (платформенные сервисы Avalonia).

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
scripts/check.sh
```
Ручная проверка: Windows и Linux-десктоп с gnome-keyring — сохранить ключ, перезапустить, ключ на месте.

Файлы: `ImageGen/ImageGen.Avalonia/Services/Secrets/**` (новые), `ImageGen/ImageGen.Avalonia/App.axaml.cs` (регистрация), `ImageGen/ImageGen.Presentation/Services/ISecretPersistenceInfo.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.*`, `ImageGen/ImageGen.Maui/Services/` (реализация `ISecretPersistenceInfo`), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/Directory.Packages.props`, тесты в `ImageGen.Avalonia.Tests` и `ImageGen.Presentation.Tests`, `docs/decisions/NNNN-desktop-secret-storage.md` (новый), `docs/knowledge/avalonia.md`, `docs/architecture.md`, `poc/…/040-avalonia-secret-store/**`

## Отчёт
- Пробный код (шаг 1): `poc/backlog/040-avalonia-secret-store/` — Secret Service через Tmds.DBus 0.95.1 (записать, найти, прочитать, удалить). Итог проверки в изолированной сессии D-Bus с gnome-keyring 46: коллекция `session` работает без промпта, `default` (login) не создаётся без дисплея. Вывод записан в ADR 0008.
- ADR 0008 «Хранение ключей в десктопе Avalonia» (`docs/decisions/0008-desktop-secret-storage.md`): Secret Service (Linux), DPAPI (Windows), Keychain через `security -i` (macOS, не проверен), память как запасной вариант.
- Avalonia (`ImageGen.Avalonia`):
  - `Services/Secrets/ISecretBackend`, `DesktopSecretStore` (выбор бэкенда по ОС при первой проверке, результат кешируется; без бэкенда или при сбое — память; `GetAsync` при ошибке чтения даёт `null`, как MAUI).
  - `SecretServiceBackend` (Linux, алиас `default` по умолчанию, интерфейсы D-Bus в `SecretServiceDBus.cs`; запросы с промптом считаются ошибкой); `DpapiSecretBackend` (Windows, `ProtectedData`, файл `secrets/<имя>.bin`; имя проверяется через `SecretFileNames.PathFor`); `KeychainSecretBackend` (macOS, не проверен).
  - `App` использует `DesktopSecretStore.ForCurrentPlatform(paths)`; `AppServices.Create(paths, secrets, persistence)` регистрирует оба интерфейса.
- Presentation: `ISecretPersistence` (`Task<bool> IsPersistentAsync`), `ApiKeyPageModel.OnAppearingAsync()` выставляет `PersistenceWarning` (ресурс `ApiKeyNotPersisted`, `AppResources.resx` и аксессор).
- MAUI: `MauiSecretPersistence` (всегда `true`), регистрация в `MauiProgram`.
- Тесты (TDD: сначала красные, затем код):
  - Presentation: `ApiKeyPageModelTests` +2 (предупреждение при `false`, без предупреждения при `true`); всего 114/114.
  - Avalonia: `DesktopSecretStoreTests` (8), `SecretFileNamesTests` (2), `DpapiSecretBackendTests` (Windows, на Linux пропущен), `SecretServiceBackendTests` (интеграционный, пропущен без сессии D-Bus); вне сессии 14/16 (2 пропущены), внутри `dbus-run-session` с gnome-keyring 15/16 (пропущен DPAPI).
- Результаты: Core 94/94, Data 37/37, Presentation 114/114, Avalonia 15/16 (пропуск DPAPI) внутри D-Bus; Release без предупреждений; `dotnet format --verify-no-changes` для `ImageGen.Avalonia`, `ImageGen.Avalonia.Tests`, `ImageGen.Presentation`, `ImageGen.Presentation.Tests`, `ImageGen.Maui` — код 0; MAUI `net10.0-android` — 0 ошибок, 0 предупреждений; `scripts/check.sh` — пройден.
- Отклонения от плана:
  - `ISecretPersistence.IsPersistentAsync` асинхронный (не свойство `IsPersistent`): проверка службы — вызов D-Bus, в конструкторе и на UI-потоке её делать нельзя.
  - `InMemorySecretStore` оставлен как запасной вариант внутри `DesktopSecretStore`, отдельного `InMemorySecretBackend` нет.
  - Страница ключа в Avalonia не создана (это задача 041); MAUI-страница предупреждение не показывает, т. к. `MauiSecretPersistence` всегда `true`.
  - Пакеты: `Tmds.DBus` 0.95.1 (MIT, опубликован 2026-09-04; 0.95.2 не берём, вышел 2026-10-27), `System.Security.Cryptography.ProtectedData` 10.0.12 (MIT).
- Не проверено: DPAPI на Windows (тест написан, пропускается на Linux); macOS Keychain (нет машины); полный перезапуск приложения с сохранённым ключом (проверено на уровне бэкенда: новый экземпляр читает значение из службы); в login-коллекции `default` (в контейнере не создаётся); показ предупреждения на странице ключа (задача 041); «ключа нет в файлах» — проверено по коду (Linux и память файлов не пишут) и тестом DPAPI (Windows, не запускался).
- Статус `in-progress`: нужны проверки на Windows (DPAPI, перезапуск) и на Linux-рабочем столе с gnome-keyring (login), плюс страница ключа в 041.
- Среда: Linux (облачная сессия) с gnome-keyring 46 (установлен через apt) в изолированной сессии D-Bus.

## Комментарии

### 2026-10-28, планирование (claude)
Вопрос владельцу: macOS-машины для проверки сборки и Keychain нет. Делать macOS-часть без проверки (с пометкой «не проверено») или отложить её до появления машины? Допущение плана: реализовать по документации, пометить «не проверено».

### 2026-10-28, владелец (в чате, записал ревью)
macOS-часть делать по документации с пометкой «не проверено» — подтверждено.

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.

### 2026-10-28, claude-qnrqyt (результат пробного кода и выбор библиотеки)
- Secret Service работает через D-Bus и Tmds.DBus 0.95.1: запись, поиск, чтение и удаление через коллекцию `session` (`poc/backlog/040-avalonia-secret-store/`).
- Коллекция `default` (login) в контейнере не создаётся без дисплея (промпт `SystemPrompter`). На рабочем столе она есть; проверить на Linux-десктопе нужно владельцу или отдельно.
- Записано в ADR 0008. Отклонение: `ISecretPersistence` асинхронный (`IsPersistentAsync`), не свойство.
- Вопрос владельцу: нужна ли проверка на Linux-рабочем столе с gnome-keyring перед закрытием задачи (сейчас статус in-progress)?
