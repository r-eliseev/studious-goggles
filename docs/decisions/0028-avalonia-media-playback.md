# 0028. Воспроизведение видео и аудио в Avalonia

Статус: принято
Допустимые статусы: предложено, принято, заменено на NNNN, отклонено
Дата: 2026-11-07

Уточняет п. 7 ADR 0027 для Avalonia.

## Контекст
ADR 0027 выбрал LibVLC для Avalonia. Пробник 206: `LibVLCSharp.Avalonia` 3.10.2 с `VideoView` собирается на Avalonia 12.1.4 и проигрывает файл на desktop; на Android `VideoLAN.LibVLC.Android` 3.6.5 добавляет ~19 МБ к APK и даёт XA0141 (нативные библиотеки без выравнивания 16 КБ — на устройствах с 16-КБ страницами не загрузятся). README `LibVLCSharp.Avalonia` перечисляет только Windows, macOS, Linux (задача 207 заблокирована). На телефоне владельца MAUI `MediaElement` (ExoPlayer) в полноэкранном окне работает, а внутри прокручиваемой ленты — чёрное видео и мигание (задача 219). Решение владельца 2026-11-07: на Android — встроенный плеер Android.

## Решение
1. Общий контракт `IMediaPlayerService.ShowAsync(filePath, accessibleLabel, ct)` (Presentation, задача 219): карточка видео и аудио показывает Play, воспроизведение идёт в отдельном полноэкранном окне/экране, не в ленте — во всех оболочках.
2. **Avalonia desktop** (Linux, Windows, macOS): LibVLC (`LibVLCSharp` + `LibVLCSharp.Avalonia` `VideoView`) в отдельном окне плеера (по образцу окна просмотра картинки), не внутри ленты: `VideoView` — нативное окно поверх содержимого и в прокручиваемом списке может обрезаться и мигать. Нативная libvlc — `VideoLAN.LibVLC.Windows`/`.Mac`; на Linux — системная (`libvlc5`, `vlc-plugin-base`).
3. **Avalonia Android**: отдельная Android-активность с `androidx.media3` (`Xamarin.AndroidX.Media3.ExoPlayer` и `.Ui` 1.11.1, MIT AND Apache-2.0; `PlayerView` с управлением) для видео и аудио. LibVLC в Android-голову не подключается.

## Альтернативы
- Только desktop, на Android — «Open with» во внешнем приложении: без новых пакетов, но смотреть приходится вне приложения.
- LibVLC на Android с выводом кадров колбэками в `WriteableBitmap`: +19 МБ, проблема 16 КБ, стоимость копирования кадров неизвестна.
- Нативный `VideoView` LibVLC на Android через `NativeControlHost`: +19 МБ, 16 КБ, проблемы наложения поверх Avalonia.
- Встроенный `VideoView` в карточке desktop: возможны обрезка и мигание в прокрутке; отказ в пользу окна.

## Последствия
- Поведение одинаковое во всех оболочках: в ленте обложка/значок и Play, просмотр — отдельно.
- Android-голова растёт на Media3 (порядка нескольких МБ; точный размер — в отчёте 224), без LibVLC.
- Desktop-пакеты растут на libvlc (Windows: ~112 МБ для win-x64 по отчёту 206).
