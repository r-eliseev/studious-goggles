# Avalonia на Android и iOS

Исследование задачи 094, проверено 2026-10-30. Пробник находится в `poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/`; это не продуктовый проект и он связывает актуальные исходники `ImageGen.Avalonia` и `ImageGen.Presentation` ссылками.

## Версии и пакеты

- Avalonia 12.1.4, .NET SDK 10.0.403, Android target `net10.0-android36.0`, Android SDK из `/root/android-sdk`; `Avalonia.Android` и `Avalonia.Themes.Fluent` восстановились и собрали пробник на Linux.
- Шаблон `Avalonia.Templates@12.1.4` установлен командой `dotnet new install Avalonia.Templates@12.1.4`. `dotnet new avalonia.xplat -n Probe -o tmp/094/xplat` создал Android, iOS, Desktop и Browser головы. Для выбора MVVM параметр шаблона — `--mvvm CommunityToolkit`; CommunityToolkit также является значением по умолчанию.
- Шаблон Android включает `Application : AvaloniaAndroidApplication<App>`, `MainActivity : AvaloniaMainActivity`, ресурсы темы/заставки и `Xamarin.AndroidX.Core.SplashScreen`. Avalonia 12 задаёт `IActivityApplicationLifetime.MainViewFactory`, потому что Android может создать несколько экземпляров Activity. Проверено сборкой и соответствует [официальному описанию изменения Avalonia 12](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes).

## Шаблон и голова Android

- Пробник использует `MainApplication`, `MainActivity`, `MobileApp` и `MainView`. В `MobileApp` создаются сервисы и устанавливается `MainViewFactory`; навигация использует тот же стек `AvaloniaNavigationService` и страницы `Presentation`, что десктопная оболочка.
- `AssemblyName` установлен в `ImageGen.Avalonia`, чтобы ссылки `avares://ImageGen.Avalonia/...` на темы и ресурсы совпадали с десктопной сборкой.
- Первичный запуск открывает список сессий. В DI зарегистрированы реальные Core, Data и Presentation сервисы, `AppInitializer`, модели страниц и маршруты.
- На старте Android logcat получает только пути `Context.FilesDir/ImageGen` и `Environment.SpecialFolder.LocalApplicationData`; на устройстве фактические значения не проверены.
- Сборка Debug прошла без предупреждений. Настоящее приложение не запускалось: `adb devices` вернул пустой список.

## Перенос разметки

Ссылками подключены все `Views/*.axaml` и code-behind из `ImageGen.Avalonia`, кроме desktop `MainWindow.axaml`; также подключены конвертеры, markup extensions, навигация, `AvaloniaMainThread`, `AvaloniaAppearanceApplier`, общие темы, шрифты и лицензии шрифтов. XAML компилятор собрал экраны Sessions, API key, Chat, Model Picker, Session Models, Session Parameters и Settings без изменения файлов в `ImageGen/`.

В POC добавлены мобильный `MainView`, системный Back, сдвиг контента по `IInputPane`, клавиша Send для чата и Android-реализации платформенных сервисов. POC-specific global aliases разрешают неоднозначности типов `Button` и `Application` от Android и Avalonia.

## Платформенные службы

Число строк получено через `wc -l` для `Platform/*.cs`, `Composition/*.cs`, `Views/*.cs` (427 строк суммарно; сюда входят мобильная голова и DI):

| Служба / файл | Строк | Реализация | Результат на 2026-10-30 |
|---|---:|---|---|
| `AndroidAppPaths.cs` | 13 | `Context.FilesDir/ImageGen` | Собирается; фактический путь на устройстве не проверен |
| `AndroidSecretStore.cs` | 65 | Android KeyStore AES-256-GCM; IV сохраняется перед ciphertext/tag в файле из `SecretFileNames` | Собирается; запись, чтение и сохранность после перезапуска не проверены |
| `TopLevelServices.cs` | 170 | Avalonia clipboard и system file picker; MediaStore для Android 10+ (`Pictures/ImageGen`); Android <10 возвращает Failed; folder launcher unsupported; пять диалогов через overlay | Собирается; clipboard, picker, галерея и Back у диалога не проверены на устройстве |
| `MobileAppServices.cs` | 74 | Реальные сервисы, модели страниц и маршруты приложения | DI проверяется при создании контейнера; запуск и SQLite runtime не проверены |
| `MainView.axaml.cs`, `MobileTweaks.cs` | 105 | Навигация, Back, `IInputPane`, ReturnKeyType Send и multiline input | Собирается; клавиатура и системный Back не проверены на устройстве; отсутствие `InputPane` пишет обезличенную запись в logcat |

Android KeyStore реализация для POC не является доказательством безопасного восстановления данных после backup/restore. На iOS шаблон содержит `Probe.iOS/AppDelegate.cs`, `Main.cs`, `Info.plist`, `Entitlements.plist`; возможные реализации ключа через Keychain (`Security.SecRecord`) и галереи через `PHPhotoLibrary` только описаны, не реализованы.

## Размер APK

Сравнение Release, одинаковые параметры `android-arm64`, `AndroidPackageFormats=apk`, .NET 10 и SDK из текущей Linux-среды; указаны подписанные файлы `*-Signed.apk`:

| Проект | APK | Байты | МиБ |
|---|---|---:|---:|
| Avalonia probe | `com.eliseev.imagegen.avaloniaprobe-Signed.apk` | 29 152 195 | 27.8 |
| MAUI | `com.companyname.imagegen.maui-Signed.apk` | 22 183 516 | 21.2 |
| Разница | Avalonia больше MAUI | 6 968 679 | 6.65 |

Размер проходит критерий задачи: Avalonia не превышает MAUI более чем на 10 МиБ. Крупные записи Avalonia APK: `libSkiaSharp.so` 9.03 МБ, assemblies blob 8.16 МБ, `classes.dex` 7.92 МБ, AOT `System.Private.CoreLib` 5.07 МБ. У MAUI крупнейшие: `classes.dex` 8.90 МБ, assemblies blob 7.49 МБ, `classes2.dex` 6.49 МБ. Число — размер APK на диске; фактический размер установки не измерен.

## Ручные проверки

На 2026-10-30 устройство не подключено (`adb devices` пуст). Пробник имеет диагностические extras `probe-secret-write` и `probe-secret-read`: они пишут только `persisted=true/false` и `file-exists=true/false` в logcat, не значение маркера и не содержимое зашифрованного файла. Сам диагностический запуск не проверен. Не проверены: холодный старт и память в сравнении с MAUI; создание/удаление сессии, переходы и Back; сохранение тестового маркера и восстановление ключа; клавиатура Send и видимость ввода; clipboard; file picker; MediaStore/галерея; overlay подтверждения; узкая раскладка и TalkBack. Эти сценарии переданы в задачу 140. Реальные ключи и платные генерации не использовались.

## iOS

Проверено только по официальной документации Avalonia 2026-10-30: [развёртывание iOS](https://docs.avaloniaui.net/docs/deployment/ios) требует Mac с Xcode для сборки; с Windows можно использовать сетевой Mac build host. Шаблон `avalonia.xplat` сгенерировал iOS голову с `AppDelegate`, `Main.cs`, `Info.plist`, `Entitlements.plist`. На этой Linux-среде iOS workload не устанавливался и iOS-сборка не запускалась. Устройство и Mac отсутствовали.
