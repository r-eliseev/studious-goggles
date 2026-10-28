---
id: 054
status: planned
assignee:
claimed:
created: 2026-10-28
---

# MAUI на Windows: картинка на карточке не показывается

## Описание
Дефект (ручная проверка 016, владелец, 2026-10-28, скриншот). В MAUI под Windows карточка генерации в статусе «Done» с моделью, сидом и ценой показывает пустую область вместо картинки. (Кнопки Repeat / New seed / Edit / Describe / Prompt не обрезаны: они видны при прокрутке ленты — уточнение владельца.)

Журнал (`…\com.companyname.imagegen.maui\Data\logs\imagegen-20261028.log`, 16:41:05) показывает, что всё до показа работает: `POST /api/v1/images -> 200`, `Image saved: images/cb3a6267…/f364f868….jpg (232697 bytes, image/jpeg)`, `Generation f364f868… is Succeeded`. Файл на диске есть и не пустой.

Зависит от: —.
Среда: только Windows (дефект WinUI-оболочки MAUI; диагностика — запуск под Windows).

## Критерии готовности
- На Windows картинка готовой генерации видна на карточке сразу после «Done» и после перезапуска приложения; JPEG и PNG.
- Причина записана в «Отчёте» и в `docs/knowledge/maui.md`; если это особенность MAUI, — со ссылкой на источник.
- Тест в Presentation (или Avalonia), который ловит причину, если она не только в WinUI (например, формат пути).

## Анализ
- Путь на карточке: `GenerationItemViewModel.ImageFilePath = IImageFileStore.GetFullPath(generation.ImagePath)`; `FileImageStore.GetFullPath` = `Path.Combine(DataDirectory, relative)`, а относительный путь хранится с `/` (`images/<session>/<generation>.jpg`). На Windows получается смешанный путь `C:\Users\elise\AppData\Local\User Name\…\Data\images/…/….jpg` (с пробелом в «User Name»).
- Показ: `Converters/FilePathToImageSourceConverter` → `ImageSource.FromFile(path)`, разметка `Pages/ChatPage.xaml` (строка 43: `Image` с `HeightRequest="256"`, `IsVisible="{Binding HasImage}"`).
- Гипотезы (проверить по очереди, не чинить вслепую): (1) смешанные разделители или пробел в пути ломают загрузку `FileImageSource` в WinUI; (2) неупакованное WinUI-приложение не грузит `FileImageSource` вне папки приложения; (3) `HasImage` или `ImageFilePath` не обновляются у карточки, созданной до события (привязка); (4) область картинки 256 px есть, но `Image` получает нулевой размер (разметка). На Android и в Avalonia показ не проверялся вживую.

## План
1. Воспроизвести на Windows: сессия с готовой генерацией (у владельца уже есть, `cb3a6267…`). Включить журнал Debug, добавить временную диагностику в конвертер (путь, `File.Exists`) — не коммитить.
2. Проверить гипотезы 1–3: нормализовать путь (`Path.GetFullPath`, разделители ОС); попробовать `ImageSource.FromStream(() => File.OpenRead(path))`; проверить, что свойства карточки меняются (журнал или точка останова).
3. Исправить причину минимально: если путь — нормализация в `FileImageStore.GetFullPath` (тест в `Data.Tests`: на Windows разделители `\`); если загрузчик WinUI — конвертер на поток (`FromStream`) в MAUI.
4. Пункт в 016: «картинка видна на карточке и после перезапуска».

### Команды проверки
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
scripts/check.sh
```
Ручная проверка на Windows: запуск, сессия с картинкой, картинка видна; новая генерация — картинка появляется без перезагрузки.

Файлы: `ImageGen/ImageGen.Maui/Converters/FilePathToImageSourceConverter.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Data/FileImageStore.cs` (если причина в пути), `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `docs/knowledge/maui.md`, `tasks/v0.1/016-skeleton-manual-check.md`

## Отчёт

## Комментарии
