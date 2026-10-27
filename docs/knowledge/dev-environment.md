# Среды разработки

Общий обзор: где какой проект собирается и что умеет каждая среда. Записано 2026-10-27. Репозиторий используется из двух сред:

- **Windows-машина** — полная среда, включая цель Windows у MAUI. Описание: `docs/knowledge/windows-machine.md`.
- **Облачная сессия Claude Code** (Linux-контейнер) — без цели Windows у MAUI, с целью Android. Описание: `docs/knowledge/claude-cloud-session.md`.

Пункты с пометкой «не проверено» ещё не проверялись в реальной среде.

## Что где собирается
| Проект | Windows-машина | Облачная сессия (Linux) |
|---|---|---|
| `ImageGen.Core`, `ImageGen.Core.Tests` | да | да, проверено |
| `ImageGen.Data`, `ImageGen.Data.Tests` | да | да, проверено |
| `ImageGen.Maui`, цель Android (`net10.0-android`) | да | да, проверено (APK собран на шаблоне) |
| `ImageGen.Maui`, цель Windows (`net10.0-windows…`) | да | нет, проверено (XAML-компилятор только под Windows) |

- Сборка всего решения (`dotnet build ImageGen/ImageGen.slnx`) и `dotnet format ImageGen/ImageGen.slnx` на Linux не запускаются, потому что решение включает MAUI-проект, а без нагрузки MAUI он не загружается. Форматирование и сборка по отдельным проектам: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`.
- В CI на ubuntu-latest установка нагрузки `maui-android` падала с «Disk quota exceeded», поэтому Android в CI собирается на Windows (`.github/workflows/build.yml`, задача `maui`). В облачной сессии Android-сборка проверена, но CI на ней не настроен.

## Строка «Среда» в задаче
У каждой задачи в «Описании» есть строка «Среда»: `Linux или Windows` или `только Windows ...`. Правило в `docs/tasks.md`: агент, у которого нет нужной среды, задачу не занимает.

Для скелета v0.1 по строке «Среда»: на Linux идут задачи 001–011, на Windows — 034 и 012–016. Android-часть задачи 034 (пустое MAUI-приложение под Android) в облачной сессии собирается, Windows-часть — нет. Менять ли строку «Среда» у 034, решает владелец (`docs/status.md`).
