# Среды разработки

Где какую задачу можно выполнять. Записано 2026-10-27. Пункты с пометкой «не проверено» ещё не проверялись в реальной облачной сессии.

## Что где собирается
| Проект | Linux (в том числе облачная сессия Claude Code) | Windows с нагрузками MAUI |
|---|---|---|
| `ImageGen.Core`, `ImageGen.Core.Tests` | да | да |
| `ImageGen.Data`, `ImageGen.Data.Tests` | да | да |
| `ImageGen.Maui` | нет (см. ниже) | да: Windows и Android |

- У .NET MAUI нет цели Linux. На Linux `ImageGen.Maui.csproj` оставляет только цель `net10.0-android` (условия в csproj), а для неё нужны нагрузка `maui-android`, Android SDK и JDK. В CI на ubuntu-latest установка `maui-android` падала с «Disk quota exceeded», поэтому MAUI-проект на Linux не собираем.
- Без нагрузки MAUI-проект не загружается, поэтому на Linux не работают `dotnet build ImageGen/ImageGen.slnx` и `dotnet format ImageGen/ImageGen.slnx`. Форматирование и сборку запускать по отдельным проектам: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`.
- Windows-цель MAUI собирается только на Windows. Проверка интерфейса скелета (задача 016) тоже только на Windows.

## Строка «Среда» в задаче
У каждой задачи в «Описании» есть строка «Среда»: `Linux или Windows` или `только Windows ...`. Правило в `docs/tasks.md`: агент без нужной среды задачу не занимает.

Для скелета v0.1: на Linux идут задачи 001–011, на Windows — 034 и 012–016.

## Облачная сессия Claude Code
- Нужен .NET 10 SDK (`dotnet --list-sdks` показывает `10.0.x`) и доступ к `nuget.org` для восстановления пакетов. Есть ли SDK в облачной среде по умолчанию — неизвестно (не проверено).
- Если SDK нет, его можно поставить в скрипте настройки окружения (не проверено в облаке; скрипт официальный, https://dot.net/v1/dotnet-install.sh):
  ```bash
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
  ```
  Если сеть облачной среды ограничена списком адресов, в нём должны быть `dot.net`, `builds.dotnet.microsoft.com` (загрузка SDK) и `api.nuget.org` (пакеты) — неизвестно, нужно ли это настраивать (не проверено).
- Тесты не ходят в сеть: OpenRouter подменяется `StubHttpMessageHandler`, ключ не нужен.
- Скрипты задач (`scripts/claim-task.sh`, `scripts/new-task.sh --publish`) пушат прямо в `origin/main`. Если облачной сессии запрещён пуш в `main`, занятие задачи не сработает: тогда задачу занимает человек или агент на локальной машине.
