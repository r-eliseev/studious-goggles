---
id: 094
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Avalonia на Android и iOS вместо MAUI: исследование

## Описание
Просьба владельца (2026-10-29): проверить, может ли Avalonia-оболочка обслуживать телефоны (Android, iOS), чтобы убрать MAUI-оболочку и размечать каждый экран один раз. Повод — замечания после проверки MAUI (задачи 088–093): разметка экранов дублируется между оболочками (в MAUI экран моделей ещё и скопирован в боковую панель `ChatPage.xaml`), поведение расходится (кнопка «…» карточки работает в Avalonia и не работает в MAUI).

ADR 0006 рассматривал вариант «Avalonia везде, без MAUI» и отложил его: «мобильная Avalonia требует отдельной проверки зрелости». Эта задача и есть такая проверка. Источник: https://avaloniaui.net/avalonia/mobile (страница производителя). Там заявлены Android и iOS из того же проекта и той же разметки, отрисовка через Skia, .NET 10, доступность через `AutomationProperties`, сборка и подпись обычной .NET-цепочкой. Сами же авторы пишут, что мобильные цели у Avalonia самые молодые и сторонних контролов для телефонов мало. Ничего из этого на нашем коде не проверено.

Что MAUI-оболочка сейчас берёт у платформы (`ImageGen.Maui/Services`, `Platforms/`), и у Avalonia на телефоне готового аналога может не быть:
- хранилище ключей: `MauiSecretStore`/`MauiSecretPersistence` (`SecureStorage`: Android Keystore, iOS Keychain);
- сохранение в галерею: `MauiImageSaver` (`MediaStore` на Android, `PHPhotoLibrary` на iOS, задача 080);
- выбор файлов и буфер обмена: `MauiFilePickerService`, `MauiClipboardService` (у Avalonia есть `IStorageProvider` и буфер обмена — работают ли на телефоне, проверить);
- отправка по Enter с экранной клавиатуры Android: `Platforms/Android/ChatEditorImeHandling.cs` (задача 050);
- раскладка телефона: узкий экран, нижнее меню карточки, долгое нажатие, свайп в списке сессий, системная кнопка «назад».

Результат — документ и решение владельца, а не перенос. Если переход одобрят, его делают отдельными задачами.

Зависит от: —
Среда: Linux или Windows с Android SDK (пробная сборка под Android; эмулятор или устройство — неизвестно, есть ли). iOS и Mac Catalyst — только по документации, с пометкой «не проверено»: macOS-машины нет.

## Критерии готовности
- Пробный проект в `poc/backlog/094-avalonia-mobile-research/`: Avalonia-приложение под `net10.0-android`, которое ссылается на `ImageGen.Presentation` и показывает хотя бы список сессий и чат на наших моделях страниц. Записано, собирается ли оно, запускается ли (эмулятор или устройство) и что пришлось обойти.
- По каждой платформенной службе из «Описания» записано: есть ли готовое в Avalonia или пакете (имя, лицензия, живость), сколько своего кода нужно, можно ли перенести код из MAUI-оболочки.
- Оценены доступность (TalkBack, по возможности на устройстве), экранная клавиатура и поле ввода чата, производительность ленты с картинками, размер APK в сравнении с MAUI (около 35 МБ, Release, 2026-10-29).
- Выводы в `docs/knowledge/avalonia-mobile.md`; черновик ADR в `docs/decisions/` со статусом `proposed`, который в случае перехода заменит ADR 0006 в части MAUI. Варианты: остаться на двух оболочках; Avalonia везде; Avalonia на десктопе и Android, MAUI только на iOS. Для каждого — что выигрываем, что теряем, примерный объём переноса.
- Вопросы к владельцу — в «Комментариях» и в `docs/status.md`, «Открытые вопросы».

## Анализ

### Что уже известно (документы и код, 2026-10-29)
- **Версии.** В проекте Avalonia 12.1.4 (`ImageGen/Directory.Packages.props`). Пакеты `Avalonia.Android` и `Avalonia.iOS` той же версии 12.1.4 есть на nuget.org (опубликованы 2026-10-09, лицензия MIT; проверено по nuspec): `Avalonia.Android` — цель `net10.0-android36.0`, зависит от `Avalonia.Skia`, `Avalonia.HarfBuzz`, `Xamarin.AndroidX.AppCompat` 1.7.1.3, `Xamarin.AndroidX.Window`, `Xamarin.AndroidX.DocumentFile`; `Avalonia.iOS` — `net10.0-ios26.0`, `net10.0-maccatalyst26.0`, `net10.0-tvos26.0`. Значит, версию Avalonia поднимать не нужно, а Android требует ту же платформу API 36, что и MAUI.
- **Поддержка платформ** (docs.avaloniaui.net, «Supported platforms», 2026-10-29): Android 16 (API 36) — Tier 1, Android 12–15 — Tier 2, 11 и ниже — Tier 3; iOS 26 — Tier 1, iOS 18 — Tier 2. Минимум .NET 10, мобильная поддержка следует политике MAUI. Сборка Android — обычный workload `android` и Android SDK, на Linux поддерживается (там же гайд «Configure Android debugging on Linux»). iOS по документации «must be built on macOS»; с Windows — только через сетевой Mac-хост. Без Mac iOS не собрать ни Avalonia, ни MAUI (MAUI iOS и Mac Catalyst сейчас и так не публикуются, `docs/knowledge/dev-environment.md`).
- **Что есть в Avalonia для телефона** (по списку API документации, на нашем коде не проверено): `ISingleViewApplicationLifetime` (одно представление вместо окна), `AvaloniaMainActivity`, `AvaloniaAndroidApplication<TApp>`, `AvaloniaAppDelegate<TApp>`; `IInputPane` (состояние экранной клавиатуры) и `IInsetsManager` (безопасные области, edge-to-edge); `TextInputOptions` и `TextInputReturnKeyType` (клавиша «Отправить» на экранной клавиатуре); `AndroidBackRequestedEventArgs` (системная кнопка «назад»); распознаватели `Swipe`, `Pull`, `Pinch`, `Scroll` и `RefreshContainer`; `IClipboard`, `ILauncher`; `IStorageProvider` — по таблице документации `OpenFilePickerAsync` и `SaveFilePickerAsync` есть на Android и iOS, а `TryGetFileFromPathAsync` — нет. Доступность Android — пространство имён `Avalonia.Android.Automation` (`INodeInfoProvider`); насколько TalkBack читает наши `AutomationProperties.Name` — неизвестно.
- **Чего в Avalonia нет:** защищённого хранилища (ADR 0008) и сохранения в галерею — это свой код в оболочке.
- **Avalonia-оболочка сейчас только десктопная:** `ImageGen.Avalonia.csproj` — `WinExe`, `net10.0`, пакеты `Avalonia.Desktop`, `Tmds.DBus`, `System.Security.Cryptography.ProtectedData`; `Program` запускает `StartWithClassicDesktopLifetime`; `MainWindow : Window`. Все платформенные службы получают `Func<Window?>` (`AppServices.MainWindowOwner`): `AvaloniaFilePickerService`, `AvaloniaClipboardService`, `AvaloniaImageSaver`, `AvaloniaFolderLauncher`, `AvaloniaDialogService` (последний создаёт `new Window` для каждого диалога — на телефоне окон нет). `DesktopAppPaths`, `DesktopSecretStore` — десктопные. Навигация (`AvaloniaNavigationService`) держит стек в `ContentControl` (`AttachHost`) и от окна не зависит. Разметка экранов — `UserControl` в `Views/` (Sessions, Chat, ApiKey, ModelPicker, SessionModels, SessionParameters, Settings); 71 упоминание `AutomationProperties`.
- **Платформенные интерфейсы, которые понадобятся на телефоне** (Core: `ISecretStore`, `IAppPaths`; Presentation: `ISecretPersistence`, `IFilePickerService`, `IClipboardService`, `IImageSaver`, `IFolderLauncher`, `IDialogService`, `INavigationService`, `IMainThread`; плюс применение стиля и темы `AvaloniaAppearanceApplier`):

| Служба | MAUI сейчас | Avalonia на телефоне: что ожидаем | Проверить |
|---|---|---|---|
| `ISecretStore`, `ISecretPersistence` | `MauiSecretStore` на `SecureStorage` (Android Keystore, iOS Keychain) | готового нет; варианты: свой ключ AES-GCM в `AndroidKeyStore` + зашифрованный файл в папке данных; пакет AndroidX Security (`EncryptedSharedPreferences`; живость пакета — неизвестно); iOS — Keychain через `Security.SecRecord` (только по документации) | объём кода, сохранность после перезапуска, поведение при `allowBackup` |
| `IAppPaths` | `FileSystem.AppDataDirectory` | `Environment.SpecialFolder.LocalApplicationData` или `Context.FilesDir`; куда указывает первое на Android — неизвестно | путь, логи, БД SQLite открывается |
| `IImageSaver` | `MauiImageSaver`: `MediaStore` (Android 10+), папка Pictures (до API 28), `PHPhotoLibrary` (iOS) | Android-ветку MAUI можно перенести почти дословно, заменив `Platform.AppContext` на контекст активности; iOS — так же по документации | картинка видна в «Галерее» |
| `IFilePickerService` | `FilePicker` | `IStorageProvider.OpenFilePickerAsync` (заявлено для Android) | выбор фото, чтение потока `content://` |
| `IClipboardService` | `Clipboard` | `TopLevel.Clipboard` | копирование промпта |
| `IFolderLauncher` | на телефоне скрыто (`IsSupported=false`) | так же скрыть | — |
| `IDialogService` | `DisplayAlertAsync`, `DisplayPromptAsync` | окна нет: диалог как слой поверх `MainView` | подтверждения, ввод имени |
| `INavigationService` | Shell | тот же `ContentControl`-стек + «назад» системной кнопкой | возврат назад, выход из приложения на корне |
| `IMainThread` | `MainThread` | `Dispatcher.UIThread` (как на десктопе) | — |
| Enter и IME (задача 050) | `ChatEditorImeHandling` (`ImeAction.Send`) | `TextInputOptions.ReturnKeyType=Send`; туннельный `KeyDown` из десктопа | «Отправить» на клавиатуре отправляет |
| Клавиатура закрывает поле (задача 095) | не решено | `IInputPane`, `IInsetsManager` | поле ввода видно над клавиатурой |
| Жизненный цикл | `MauiApplication`, `MainActivity` | `AvaloniaMainActivity`; пауза и возобновление — неизвестно, какие события | генерация идёт после сворачивания (сейчас фоновой службы нет и в MAUI) |

- **Уже проверенное в среде** (`docs/knowledge/dev-environment.md`, `claude-cloud-session.md`, `vps-session.md`): Android-сборка MAUI идёт на Linux (облачная сессия, VPS). Эмулятора нет нигде: нет `/dev/kvm`, x86_64-образ без ускорения не загрузился за 31 минуту. Запуск возможен только на реальном телефоне: с VPS через `adb connect` по сети, или с Windows-машины по USB (Android SDK там «не проверено»). Размер APK MAUI — около 35 МБ (Release, 2026-10-29, из «Описания»; набор ABI и формат не записаны).

### Вопросы, которые снимает исследование
1. Собирается ли Android-голова Avalonia 12.1.4 на Linux рядом с `ImageGen.Presentation` и `ImageGen.Data` (SQLite на Android через `Microsoft.Data.Sqlite` 10.0.12 под Avalonia, без MAUI).
2. Переносятся ли наши `Views/*.axaml` без правок: сколько разметки годится как есть и что ломает узкий экран (боковая панель, меню «…», `ContextMenu` против нижнего меню, долгое нажатие, свайп).
3. Как оболочку разделить для общего кода: стандартная раскладка шаблона `avalonia.xplat` (общая библиотека + головы Desktop, Android, iOS) — так ли она устроена в версии 12; что из `ImageGen.Avalonia` уезжает в головы (`Tmds.DBus`, DPAPI, `MainWindow`, `Program`).
4. Секреты: какой способ на Android (свой `AndroidKeyStore` или пакет), сколько кода, есть ли живой пакет с MIT-лицензией; iOS — только по документации.
5. Клавиатура: `ReturnKeyType`, перевод строки Shift+Enter с аппаратной клавиатуры, сдвиг поля над клавиатурой.
6. Доступность: читает ли TalkBack имена из `AutomationProperties` (только на устройстве).
7. Производительность: прокрутка ленты с картинками, холодный старт, память.
8. Размер APK Release при одинаковых условиях (ABI, обрезка) в сравнении с MAUI.
9. iOS без Mac: по документации невозможно; проверить, ставится ли workload `ios` на Linux хотя бы для компиляции C# (ожидание — нет).
10. Есть ли в мобильной Avalonia платные части (Avalonia Accelerate и подобное), нужные нам, — неизвестно.

### Пересечения с другими задачами
Пробный код лежит в `poc/` и `ImageGen/` не меняет, поэтому с 088–093, 095, 097, 098 по файлам не конфликтует. Если пробная голова подключает разметку из `ImageGen.Avalonia/Views` ссылками, правки этих задач могут её сломать; пробный код не поддерживается, в отчёте фиксируется коммит, на котором проверяли. 095 (клавиатура закрывает поле), 097 (автопрокрутка чата) и 098 (узкая шапка чата) — готовые сценарии сравнения «как это в MAUI и как в Avalonia»; результат 094 их не блокирует (решение владельца 2026-10-29).

## План

Шаги исследования. Только `poc/`, `docs/` и эта задача; `ImageGen/` не меняется.

1. **Среда.** Linux (облачная сессия или VPS): .NET 10 SDK, workload `android`, Android SDK API 36 по `docs/knowledge/claude-cloud-session.md`. Записать версии SDK и workload. Для запуска — телефон владельца через `adb connect` с VPS (`docs/knowledge/vps-session.md`), иначе запуск помечается «не проверено».
2. **Пробная голова** `poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/` (имя папки — как в «Критериях готовности»): `net10.0-android`, `Avalonia.Android` 12.1.4, `Avalonia.Themes.Fluent`, ссылки на `ImageGen.Presentation`, `ImageGen.Data`, `ImageGen.Core`. `MainActivity` (`AvaloniaMainActivity`), приложение с `ISingleViewApplicationLifetime` и `MainView` (`UserControl` с `ContentControl` для стека страниц). Разметку экранов сначала подключить из `ImageGen/ImageGen.Avalonia/Views` ссылками на файлы, без копий; если ссылка не работает (зависимость от `MainWindow`, `Window`, десктопных служб), скопировать минимум в `poc/` и записать, что мешало. Экраны минимум: список сессий и чат; по возможности ключ API и модели сессии.
3. **Мобильные службы в пробной голове** (по одной на интерфейс, тест-кейсы проверяются руками на устройстве):
   - `AndroidAppPaths`: папка данных → БД создаётся, лог пишется;
   - `AndroidSecretStore`: ключ записан → после перезапуска прочитан; в файлах папки данных ключа открытым текстом нет;
   - `AndroidImageSaver`: перенос Android-ветки `MauiImageSaver` → картинка в «Галерее», папка `Pictures/ImageGen`;
   - файлы и буфер через `TopLevel` вместо `Window` → фото выбрано, текст скопирован;
   - диалог слоем поверх `MainView` → подтверждение удаления сессии работает;
   - «назад» → возврат по стеку, на корне — выход.
   Для каждой записать: готовое API или свой код, примерный объём строк, что переносится из MAUI, что из десктопной Avalonia.
4. **Замеры** (Release, на устройстве; одинаковые условия для MAUI и Avalonia):
   - размер APK и AAB, набор ABI, `PublishTrimmed`; пересобрать MAUI-APK в тех же условиях, если 35 МБ получены иначе;
   - холодный старт (`adb shell am start -W`);
   - лента чата с 50 картинками: плавность прокрутки (способ замера для Skia-отрисовки выбрать в ходе работы: встроенная диагностика Avalonia или запись экрана), память (`dumpsys meminfo`);
   - TalkBack: список сессий, поле ввода, кнопка «Отправить», карточка — что озвучено;
   - клавиатура: клавиша «Отправить», Shift+Enter, поле над клавиатурой (сценарий 095), автопрокрутка (сценарий 097);
   - узкий экран: шапка чата (098), меню «…» карточки (088), боковая панель моделей.
5. **iOS** — только по документации, пометка «не проверено»: шаблон головы, `AvaloniaAppDelegate`, Keychain, `PHPhotoLibrary`; попытка `dotnet workload install ios` на Linux и её результат.
6. **Сравнение вариантов** по критериям: одна разметка или две; совпадение поведения; объём своего платформенного кода; доступность; клавиатура; размер APK, старт, прокрутка; где собирается и тестируется (headless-тесты Avalonia покрывают те же view); iOS; зрелость и сторонние контролы; лицензии. Варианты: (A) две оболочки как сейчас; (B) Avalonia везде, MAUI убирается; (C) Avalonia на десктопе и Android, MAUI только на iOS. Для каждого — выигрыш, потери, примерный объём переноса (список задач-кандидатов, без создания задач до решения владельца).
7. **Документы:**
   - `docs/knowledge/avalonia-mobile.md` — факты и замеры с датой и версиями, «не проверено» для iOS и всего, что не запускалось;
   - `docs/decisions/NNNN-mobile-shell.md` (следующий свободный номер в момент записи), статус «предложено»; при переходе заменит ADR 0006 в части MAUI (ADR 0006 не правится до решения владельца);
   - `docs/knowledge/dev-environment.md` — строка «Avalonia, цель Android» в таблице «Что где собирается»;
   - `docs/index.md` — новый файл в строке `knowledge/`;
   - `docs/status.md`, «Открытые вопросы» — вопрос о выборе варианта; вопросы — и в «Комментарии».
8. **Проверка:** `dotnet build` пробной головы с `-p:AndroidSdkDirectory=$ANDROID_HOME`, Release-публикация для замера; существующие тесты не затрагиваются; `scripts/check.sh`.

Файлы: `poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/**` (новые), `docs/knowledge/avalonia-mobile.md` (новый), `docs/decisions/NNNN-mobile-shell.md` (новый), `docs/knowledge/dev-environment.md`, `docs/index.md`, `docs/status.md`, `tasks/backlog/094-avalonia-mobile-research.md`.

## Отчёт

## Комментарии

### 2026-10-29, claude-desktop
Решение владельца в чате: пока поддерживаем обе оболочки (MAUI и Avalonia), пока не станет понятно, какая лучше. Задача не срочная и не блокирует 088–093: они делаются в обеих оболочках. Её результат — материал для этого выбора.

### 2026-10-29, claude-plan
Вопросы владельцу:
1. Есть ли Android-телефон, который можно подключить к VPS по `adb connect` (или к Windows-машине по USB)? Без него запуск, TalkBack, клавиатура и производительность останутся «не проверено»: эмулятора нет ни в одной среде.
2. Размер MAUI-APK около 35 МБ: какой это файл (APK или AAB, какие ABI)? Если неизвестно, план пересобирает MAUI-APK в тех же условиях, что Avalonia.
3. iOS: без Mac ни Avalonia, ни MAUI под iOS не собираются. Достаточно ли сравнения по документации, или вариант «Avalonia везде» нужно отложить до появления Mac?
4. Допущение: пробная голова подключает разметку из `ImageGen.Avalonia/Views` ссылками и не меняет `ImageGen/`; разделение оболочки на общую библиотеку и головы — только как вывод в ADR, не в этой задаче.
