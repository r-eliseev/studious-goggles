---
id: 246
status: done
assignee:
claimed:
created: 2026-11-07
---

# Avalonia iOS: голова приложения для симулятора

## Описание
Дополнение Apple (ADR 0030, п. 3) просит отдельную iOS-голову Avalonia, не переиспользуя desktop-точку входа. Устроить как Android-голову (ADR 0026): исходники `ImageGen.Avalonia` подключаются ссылками, платформенные службы — свои. Цель — сборка для iOS Simulator без подписи и запуск; публикации нет.

Зависит от: 244 (рабочий набор workload `ios` и Xcode; если 244 показал «blocked» — голову можно написать, но сборку проверить нельзя: тогда `blocked` с этим фактом после шага 1).
Среда: GitHub Actions `macos-26` (сборка: локально на Linux workload `ios` не ставится); код — локально, `dotnet format` проекта на Linux невозможен без workload — форматировать в CI шагом `dotnet format … --verify-no-changes` на macOS.

## Критерии готовности
- Проект `ImageGen/ImageGen.Avalonia.iOS/ImageGen.Avalonia.iOS.csproj`: `net10.0-ios`, `SupportedOSPlatformVersion` 15.0, `ApplicationId` `com.eliseev.imagegen.avalonia`, `ApplicationTitle` Omnilite, пакеты `Avalonia.iOS` и `Avalonia.Themes.Fluent` (версия `Avalonia` из `ImageGen/Directory.Packages.props`, сейчас 12.1.4; добавить `Avalonia.iOS` той же версии). Ссылки на исходники — тот же набор `Compile`/`AvaloniaXaml`/`AvaloniaResource`, что в `ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj` (строки 28–33), с теми же исключениями (`MainWindow`). `MobileApp.axaml(.cs)` — копия Android-варианта с iOS-службами (общий код вынести не в этой задаче). В `ImageGen/ImageGen.slnx`.
- Точка входа по шаблону Avalonia iOS 12 (`Main.cs` с `UIApplication.Main`, `AppDelegate : AvaloniaAppDelegate<MobileApp>`) — сверить с официальным шаблоном `dotnet new avalonia.xplat` версии 12.1.x; если шаблон недоступен — с документацией https://docs.avaloniaui.net (раздел iOS) и записать источник.
- Платформенные службы в `ImageGen/ImageGen.Avalonia.iOS/Platform/` (по списку Android-головы, `MobileApp.axaml.cs` ≈42–105):
  - `IosAppPaths : IAppPaths` — папка данных `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` + `ImageGen` (в контейнере приложения), логи `<данные>/logs`.
  - `IosSecretStore : ISecretStore, ISecretPersistence` — Keychain через `Security.SecRecord` (`SecKind.GenericPassword`, `Service` = `com.eliseev.imagegen.avalonia`, `Account` = имя секрета из `SecretFileNames`), `SecAccessible.AfterFirstUnlockThisDeviceOnly`; поведение как у `AndroidSecretStore` (тот же интерфейс, те же ошибки при сбое → предупреждение о сохранении). На симуляторе без подписи Keychain может вернуть `-34018` (нет entitlements): тогда добавить `Entitlements.plist` с `keychain-access-groups` для симулятора (`CodesignEntitlements` только для `iossimulator`) и записать факт в `docs/knowledge/apple-ci.md`; если не помогает — `blocked`.
  - `IImageSaver` — `AvaloniaImageSaver` (системный диалог сохранения через `StorageProvider`) как запасной путь Android до API 29 (задача 233); сохранение в «Фото» — не в этой задаче.
  - `IFolderLauncher` — `UnsupportedFolderLauncher` (скопировать из Android-головы).
  - `ILogSharer` — `IosLogSharer` через `UIActivityViewController` с файлом лога; если сложно для симулятора — заглушка с сообщением «Not supported on iOS yet» (ресурс) и запись в «Комментарии».
  - `IMediaPlayerService` (если к началу задачи существует по 219/224) — заглушка «Not supported on iOS yet».
- Ресурс значка: из `ImageGen/ImageGen.Avalonia.Android/Icon.png` (или итог задачи 234) — `Assets.xcassets/AppIcon` с одним изображением 1024×1024, если у исходника хватает размера; иначе — без значка, записать.
- Сборка в CI: `dotnet build ImageGen/ImageGen.Avalonia.iOS/ImageGen.Avalonia.iOS.csproj -c Debug -r iossimulator-arm64` — временный шаг в job `apple-preflight` (до 245) с теми же правилами `blocked`; затем запуск в симуляторе вручную шагом: `xcrun simctl install` + `launch` + скриншот через 15 с → артефакт; на скриншоте — экран первого запуска (проверить глазами и записать в «Отчёт»).
- Документация: `docs/knowledge/avalonia-mobile.md` — раздел «iOS-голова» (как устроена, что не поддерживается); `docs/architecture.md` — строка о третьей голове Avalonia, если там перечислены головы; `docs/product.md` — в строке «Платформы и оболочки» добавить «Avalonia на iOS и macOS — только CI-сборки и тесты, без публикации (ADR 0030)».

## Анализ
Справочно: дополнение владельца — `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; при расхождении действуют ADR 0030 и `docs/specs/apple-ci-e2e.md`.
`ImageGen/ImageGen.Avalonia.Android/` (csproj, `MobileApp.axaml.cs`, `Platform/*`), `ImageGen/ImageGen.Avalonia/Services/Secrets/SecretFileNames.cs`, `ImageGen.Core` интерфейсы `IAppPaths`, `ISecretStore`, `ISecretPersistence` (`rg -n "interface ISecretStore|interface ISecretPersistence|interface IAppPaths" ImageGen`), `docs/knowledge/avalonia.md` (iOS: только по документации), ADR 0008 (Keychain на macOS).

## План
1. Каркас проекта и точка входа; сборка в CI (шаг preflight).
2. `IosAppPaths`, `IosSecretStore`, остальные службы — по одной, каждая со сборкой в CI.
3. Запуск в симуляторе и скриншот.
4. Документация.

Файлы: `ImageGen/ImageGen.Avalonia.iOS/` (новый: csproj, `Main.cs`, `AppDelegate.cs`, `MobileApp.axaml`, `MobileApp.axaml.cs`, `Info.plist`, `Entitlements.plist` при необходимости, `Platform/*.cs`, `Assets.xcassets`), `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx` и `AppResources.cs` (если нужен текст «Not supported on iOS yet»), `.github/workflows/apple-ci.yml`, `docs/knowledge/avalonia-mobile.md`, `docs/knowledge/apple-ci.md`, `docs/architecture.md`, `docs/product.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
bash scripts/check.sh
# в GitHub Actions: сборка ImageGen.Avalonia.iOS для iossimulator-arm64, dotnet format на macOS, скриншот запуска
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

### 2026-11-10, codex-gpt6
- Job `build-avalonia-ios` прошёл в scheduled run [42794177774](https://github.com/r-eliseev/studious-goggles/actions/runs/42794177774).
- Симуляторный запуск подтвердил экран первого запуска «Connect OpenRouter» в run [42405774295](https://github.com/r-eliseev/studious-goggles/actions/runs/42405774295). Визуальная проверка безопасных отступов на реальном устройстве не выполнялась; известные чёрные полосы описаны ранее и не мешают запуску.

### 2026-11-07, codex-gpt6 (в работе)
- Первый CI build выявил сигнатуру `SecKeyChain.QueryAsRecord` (`SecRecord` возвращается, `SecStatusCode` идёт через `out`), nullable UIKit view и устаревший `UIApplication.KeyWindow`; исправлены.
- Предупреждение IL2026 из динамической загрузки Avalonia theme resources подавлено локально только в iOS-проекте, где тема выбирается по URI во время выполнения.
- Второй CI build прошёл компиляцию C#, затем выявил, что XAML-ресурсы ожидают assembly `ImageGen.Avalonia`, как и Android-хост. Исправлено имя сборки iOS-проекта; ожидается повторная сборка и запуск симулятора.
- Логи повторного прогона: [42371977271](https://github.com/r-eliseev/studious-goggles/actions/runs/42371977271), job `build-avalonia-ios`.
- Run [42373330934](https://github.com/r-eliseev/studious-goggles/actions/runs/42373330934): host C# compilation succeeds, but the iOS linker fails with `IL2104`/`IL2121` and `NETSDK1144`; package step then failed again because the app was absent. For the CI-only simulator build, switch to `TrimMode=copy` (no size reduction is needed for this smoke artifact), remove the stale broad IL2026 suppression, and avoid labeling arbitrary build errors as a toolchain block. New-run verification pending.
- Run [42374038441](https://github.com/r-eliseev/studious-goggles/actions/runs/42374038441): `TrimMode=copy` reduced the failure to the single known IL2026 warning for runtime-selected bundled theme XAML. Reinstated its iOS-project-only suppression; CI verification pending.

## Комментарии

### 2026-11-07, claude-audit
Создано по дополнению Apple (A2, A5): головы iOS у Avalonia в репозитории нет; Avalonia.iOS 12.1.4 есть на nuget.org. Службы — по списку Android-головы. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Дополнение владельца «MAUI + Avalonia — Apple CI/E2E» (оригинал — `docs/specs/ui-e2e-original/`). План одобрен ранее данным поручением «заапрувь все таски которые будут». Новый workflow `apple-ci.yml` по этому дополнению владелец попросил явно (ADR 0030).
