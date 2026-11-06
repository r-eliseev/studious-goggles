# Avalonia Android

`ImageGen/ImageGen.Avalonia.Android` — Android-оболочка для общих страниц `ImageGen.Avalonia` и моделей `ImageGen.Presentation`. Целевая платформа — `net10.0-android` (SDK API 36), минимальная версия Android API 23. Для сборки на Linux нужны Android SDK и .NET workload `android`:

```bash
dotnet workload install android
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Debug
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
```

Идентификатор пакета — `com.eliseev.imagegen.avalonia`, название — «Omnilite Avalonia». Навигацию показывает `Views/MainView`: `PageHost` для страниц и `OverlayHost` для диалогов, просмотра и сравнения. Android-композиция вызывает `AppServices.CreateShared` и регистрирует overlay-службы; страницы и ресурсы Avalonia подключены исходниками из desktop-проекта, отдельная desktop-оболочка и полноэкранный контроллер не включаются.

Данные хранятся в `<FilesDir>/ImageGen`. Секреты зашифрованы AES-256-GCM ключом Android Keystore; IV хранится перед шифротекстом, запись идёт во временный файл с атомарным переименованием. Папка журналов — `<FilesDir>/ImageGen/logs`; формат, retention и уровни общие с остальными оболочками. Ручная проверка установки и навигации на устройстве описана задачей 198 и пока не выполнена.

Проверка VPS от 2026-11-06: Debug build и Release build прошли без предупреждений и ошибок. Подписанный Release APK размером 60,556,659 байт (примерно 57.7 MiB) собран с локальным Android debug certificate; он не предназначен для обновления или публикации поверх официального релиза. Сборка использовала API 36, min API 23. Запуск на устройстве не проверялся.
