# ImageGen

Клиент для генерации изображений из одного промпта в нескольких моделях. Результаты и история сессий хранятся на устройстве. Источник написан на C# / .NET 10; доступны оболочки MAUI и Avalonia.

## Сейчас в исходном коде

- Встроенные image providers: OpenRouter, fal.ai и Together AI. Дополнительные OpenAI-совместимые providers можно задавать через конфиг.
- Ключ OpenRouter нужен для ассистента и запрашивается при первом запуске. Ключи fal.ai и Together AI опциональны; их можно добавить в настройках. Together доступен только для изображений и не предлагается как модель ассистента.
- Каталог Together содержит FLUX.1.1 Pro, FLUX.2 Dev, FLUX.2 Flex и FLUX.2 Pro. Живая генерация и фактическое списание Together пока не проверялись; фактическая стоимость остаётся неизвестной.
- fal.ai встроен как провайдер генерации, но его настройка не является обязательным шагом первого запуска.

Эти возможности описывают текущую ветку исходного кода. Последний опубликованный релиз v0.1.4 предшествует поддержке Together и общему экрану ключей провайдеров.

## Скачать

Последний опубликованный релиз: [ImageGen v0.1.4](https://github.com/r-eliseev/studious-goggles/releases/tag/v0.1.4). Новые версии появятся на странице [Releases](https://github.com/r-eliseev/studious-goggles/releases).

| Оболочка | ОС / архитектура | Пакет v0.1.4 | Формат | Проверка запуска |
|---|---|---|---|---|
| Avalonia | Linux x64 | [Linux x64](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Avalonia-v0.1.4-linux-x64.tar.gz) | `.tar.gz` | Пакет собран CI; запуск на целевом устройстве не проверен |
| Avalonia | Linux arm64 | [Linux arm64](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Avalonia-v0.1.4-linux-arm64.tar.gz) | `.tar.gz` | Пакет собран CI; запуск на ARM-устройстве не проверен |
| Avalonia | Windows x64 | [Windows x64](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Avalonia-v0.1.4-win-x64.zip) | `.zip` | Пакет собран CI; ручной запуск из релиза не проверен |
| Avalonia | Windows arm64 | [Windows arm64](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Avalonia-v0.1.4-win-arm64.zip) | `.zip` | Пакет собран CI; запуск на ARM-устройстве не проверен |
| Avalonia | macOS x64 | [macOS x64](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Avalonia-v0.1.4-osx-x64.tar.gz) | `.tar.gz` | Пакет собран CI; запуск на Mac не проверен |
| Avalonia | macOS arm64 | [macOS arm64](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Avalonia-v0.1.4-osx-arm64.tar.gz) | `.tar.gz` | Пакет собран CI; запуск на Mac не проверен |
| MAUI | Android | [Android APK](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Maui-v0.1.4-android.apk) | `.apk` | Подписан отладочным ключом сборки; установка и работа на устройстве не проверены |
| MAUI | Windows x64 | [Windows x64](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Maui-v0.1.4-win-x64.zip) | `.zip` | Архитектура проверена по PE; запуск собранной MAUI-программы на Windows проверялся отдельно |
| MAUI | Windows arm64 | [Windows arm64 — содержит x64-файлы](https://github.com/r-eliseev/studious-goggles/releases/download/v0.1.4/ImageGen-Maui-v0.1.4-win-arm64.zip) | `.zip` | Не устанавливайте на Windows ARM: пакет v0.1.4 неверно помечен как arm64 и содержит x64 PE-файлы |

Release workflow публикует Avalonia self-contained archives, Windows MAUI unpackaged ZIP и Android APK. Android APK предназначен для ручной установки; публикации в магазинах, MSIX и автообновления нет. APK до v0.1.5 включительно подписаны разными отладочными ключами: обновление не ставится поверх и требует удалить приложение вместе с данными. Со следующего релиза пакет Android — `com.eliseev.imagegen`: он ставится отдельным приложением (старое ImageGen удалите вручную, ключи провайдеров введите заново). Когда в репозитории появится постоянный ключ подписи, обновления будут ставиться поверх; до этого каждый релиз подписан своим отладочным ключом. MAUI для iOS и Mac Catalyst не выпускается. Для Windows ARM используйте Avalonia пакет, пока исправленный MAUI arm64 пакет не появится в новом релизе.

Сборка пакетов через CI подтверждает сборку и состав архитектуры там, где workflow проверяет PE. Она сама по себе не подтверждает работу приложения на устройстве. Текущие ручные проверки платформ перечислены в [`docs/knowledge/dev-environment.md`](docs/knowledge/dev-environment.md) и задачах [`016`](tasks/v0.1/016-skeleton-manual-check.md), [`046`](tasks/backlog/046-desktop-manual-check.md) и [`140`](tasks/backlog/140-android-manual-check.md).

## Первый запуск и ключи

Ассистент использует OpenRouter, поэтому сначала добавьте его API key. Ключи fal.ai и Together AI можно настроить позже в Settings; приложение не требует их для старта. Если выбрать модель провайдера без его ключа, запуск завершится обычной ошибкой ключа. Для Together список возможностей и ограничений описан в [`docs/knowledge/together-ai.md`](docs/knowledge/together-ai.md).

## Данные и приватность

- Сессии, SQLite-база и файлы изображений хранятся локально в папке данных приложения. Приложение не шифрует всю базу и все файлы изображений.
- API keys хранятся в системном secret store оболочки. В Avalonia, если системное хранилище недоступно, ключ остаётся только в памяти до закрытия приложения.
- Android исключён из автоматического backup; также настроены deny-all правила для облачного backup и переноса устройства. Поведение OEM-инструментов переноса на реальном устройстве не проверялось.
- Debug-журналы и отдельно включаемый debug trace могут содержать пользовательские промпты и данные провайдера. Не публикуйте их без проверки и очистки. Подробнее: [`debug trace`](docs/specs/debug-trace.md).

## Разработка

Нужен .NET 10 SDK. На Linux MAUI Windows target и всё решение целиком не собираются; подробности по средам и платформам — в [`docs/knowledge/dev-environment.md`](docs/knowledge/dev-environment.md).

Основная сборка и тесты отдельных проектов:

```sh
dotnet build ImageGen/ImageGen.Core/ImageGen.Core.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
```

Для запуска Avalonia нужен графический сеанс:

```sh
dotnet run --project ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
```

## Документация и планы

- [Архитектура](docs/architecture.md), [цели продукта](docs/product.md) и [спецификация](docs/specs/image-client.md).
- [Сессии с ассистентом](docs/specs/chat-sessions.md) и [отладочный trace](docs/specs/debug-trace.md).
- [Индекс документации](docs/index.md), [статус проекта](docs/status.md) и [архив завершённых задач](tasks/DONE.md).

Web API и Vue, Docker, галерея результатов, сравнение изображений и учёт расходов остаются будущими направлениями и не включены в текущий код.

## Лицензия

[MIT](LICENSE).
