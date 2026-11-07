# ImageGen

Клиент для генерации изображений из одного промпта в нескольких моделях. Результаты и история сессий хранятся локально. Источник написан на C# / .NET 10; доступны оболочки MAUI, Avalonia и Web с Vue-интерфейсом.

## Сейчас в исходном коде

- Встроенные image providers: OpenRouter, fal.ai и Together AI. Дополнительные OpenAI-совместимые providers можно задавать через конфиг.
- Ключ OpenRouter нужен для ассистента и запрашивается при первом запуске. Ключи fal.ai и Together AI опциональны; их можно добавить в настройках. Together доступен только для изображений и не предлагается как модель ассистента.
- Каталог Together содержит FLUX.1.1 Pro, FLUX.2 Dev, FLUX.2 Flex и FLUX.2 Pro. Живая генерация и фактическое списание Together пока не проверялись; фактическая стоимость остаётся неизвестной.
- fal.ai встроен как провайдер генерации, но его настройка не является обязательным шагом первого запуска.

Эти возможности описывают текущую ветку исходного кода; релиз выходит автоматически после каждого изменения в `main`.

## Скачать

Последний релиз — на странице [Releases](https://github.com/r-eliseev/studious-goggles/releases/latest); новый выходит автоматически после каждого изменения в `main` (следующий patch-номер). Имена пакетов (`<версия>` — например, `v0.1.13`):

| Оболочка | ОС / архитектура | Пакет | Формат | Проверка запуска |
|---|---|---|---|---|
| Avalonia | Linux x64 / arm64 | `ImageGen-Avalonia-<версия>-linux-x64.tar.gz`, `…-linux-arm64.tar.gz` | `.tar.gz` | Пакет собран CI; запуск на целевом устройстве — ручная задача 046 |
| Avalonia | Windows x64 / arm64 | `ImageGen-Avalonia-<версия>-win-x64.zip`, `…-win-arm64.zip` | `.zip` | Пакет собран CI; ручной запуск — 046 |
| Avalonia | macOS x64 / arm64 | `ImageGen-Avalonia-<версия>-osx-x64.tar.gz`, `…-osx-arm64.tar.gz` | `.tar.gz` | Пакет собран CI; запуск на Mac не проверен |
| Avalonia | Android | `ImageGen-Avalonia-<версия>-android.apk` | `.apk` | Устанавливается рядом с MAUI; свои данные и ключи; проверка на устройстве — 198 |
| MAUI | Android | `ImageGen-Maui-<версия>-android.apk` | `.apk` | Подписан постоянным ключом; проверка на устройстве — 140 |
| MAUI | Windows x64 / arm64 | `ImageGen-Maui-<версия>-win-x64.zip`, `…-win-arm64.zip` | `.zip` | Архитектура PE проверяется в release workflow; ручной запуск — 016 |

Release workflow публикует Avalonia self-contained archives, Windows MAUI unpackaged ZIP и Android APK для MAUI и Avalonia. Avalonia Android APK устанавливается рядом с MAUI и использует собственные application ID, каталог данных и ключи провайдеров. Android APK предназначен для ручной установки; публикации в магазинах, MSIX и автообновления нет. APK до v0.1.5 включительно подписаны разными отладочными ключами: обновление не ставится поверх и требует удалить приложение вместе с данными. Со следующего релиза пакет Android — `com.eliseev.imagegen`: он ставится отдельным приложением (старое ImageGen удалите вручную, ключи провайдеров введите заново). С первого релиза после v0.1.5 APK подписан постоянным ключом (сертификат `CN=ImageGen, O=eliseev`, SHA-256 `6711f8ba…10277`): переход на него с v0.1.5 и раньше требует один раз удалить приложение, дальше обновления ставятся поверх. MAUI для iOS и Mac Catalyst не выпускается.

Сборка пакетов через CI подтверждает сборку и состав архитектуры там, где workflow проверяет PE. Она сама по себе не подтверждает работу приложения на устройстве. Текущие ручные проверки платформ перечислены в [`docs/knowledge/dev-environment.md`](docs/knowledge/dev-environment.md) и задачах [`016`](tasks/v0.1/016-skeleton-manual-check.md), [`046`](tasks/backlog/046-desktop-manual-check.md) и [`140`](tasks/backlog/140-android-manual-check.md).

## Первый запуск и ключи

Ассистент использует выбранный chat-провайдер; при первом запуске можно сохранить ключ другого настроенного chat-провайдера и выбрать его модель. Ключи fal.ai и Together AI для генерации изображений настраиваются позже в Settings. Если выбрать модель провайдера без его ключа, запуск завершится обычной ошибкой ключа. Для Together список возможностей и ограничений описан в [`docs/knowledge/providers.md`](docs/knowledge/providers.md).

## Данные и приватность

- Сессии, SQLite-база и файлы изображений хранятся локально в папке данных приложения. Приложение не шифрует всю базу и все файлы изображений.
- API keys хранятся в системном secret store оболочки. В Avalonia, если системное хранилище недоступно, ключ остаётся только в памяти до закрытия приложения.
- Android исключён из автоматического backup; также настроены deny-all правила для облачного backup и переноса устройства. Поведение OEM-инструментов переноса на реальном устройстве не проверялось.
- Debug-журналы и отдельно включаемый debug trace могут содержать пользовательские промпты и данные провайдера. Не публикуйте их без проверки и очистки. Подробнее: [`debug trace`](docs/specs/debug-trace.md).

## Разработка

Нужен .NET 10 SDK. На Linux MAUI Windows target и всё решение целиком не собираются; подробности по средам и платформам — в [`docs/knowledge/dev-environment.md`](docs/knowledge/dev-environment.md).

Для Web-клиента также нужен Node.js 22.12+ и npm. Из `ImageGen/ImageGen.Web/ClientApp` выполните `npm ci`, `npm run typecheck`, `npm run lint`, `npm test` и `npm run build`. Сборка записывает Vue assets в `ImageGen/ImageGen.Web/wwwroot`. Для разработки задайте `IMAGEGEN_WEB_PASSWORD` и `IMAGEGEN_DATA_DIR`, запустите `ASPNETCORE_URLS=http://localhost:5080 dotnet run --project ImageGen/ImageGen.Web/ImageGen.Web.csproj`, а в другой консоли — `npm run dev` из `ClientApp`; Vite проксирует `/api` на Web-хост.

Основная сборка и тесты отдельных проектов:

```sh
dotnet build ImageGen/ImageGen.Core/ImageGen.Core.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
```

### Запуск Web-хоста в Docker

С установленным Docker Compose задайте `IMAGEGEN_WEB_PASSWORD` и запустите `docker compose up -d --build`. Сервис доступен на `http://127.0.0.1:8080`; данные сохраняются в именованном томе `imagegen-data`. Инструкции по резервному копированию, bind mounts и смене пароля — в [`docs/knowledge/docker.md`](docs/knowledge/docker.md).

Для запуска Avalonia нужен графический сеанс:

```sh
dotnet run --project ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
```

## Документация и планы

- [Архитектура](docs/architecture.md), [цели продукта](docs/product.md) и [спецификация](docs/specs/image-client.md).
- [Сессии с ассистентом](docs/specs/chat-sessions.md) и [отладочный trace](docs/specs/debug-trace.md).
- [Индекс документации](docs/index.md), [статус проекта](docs/status.md) и [архив завершённых задач](tasks/DONE.md).

Web API и Vue-клиент входят в текущий исходный код. Docker-образ Web-хоста пока не включён в опубликованный релиз.

## Лицензия

[MIT](LICENSE).
