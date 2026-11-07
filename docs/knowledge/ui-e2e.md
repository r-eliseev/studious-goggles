# Знания о MAUI UI E2E

## Preflight 238

Первые два прогона через `workflow_dispatch` на ветке `task/217-embedding-provider-model-name`, commit `fa3187de662ca9fa6a0e25a78466c746245fac79`: [run 42366477520](https://github.com/r-eliseev/studious-goggles/actions/runs/42366477520), [run 42366566469](https://github.com/r-eliseev/studious-goggles/actions/runs/42366566469). Android APK и unpackaged Windows app собраны в обоих запусках. Windows preflight успешен в обоих. В Android preflight второго запуска драйвер Appium/UiAutomator2 установился, но job остановился на `appium driver doctor uiautomator2`: doctor завершился кодом 1, так как emulator ещё не был установлен в PATH до запуска emulator-runner. Следующий шаг — переставить диагностику в контекст эмулятора.

### Windows (оба запуска)

- Runner: `runneradmin`, интерактивная сессия `console`, ID 2, состояние Active. Экран: 1024×768.
- Окно MAUI найдено; AutomationId `apikey.entry` найден.
- Кадр рабочего стола виден и не однотонный: доля пикселей за порогом — 0.992126 в run 42366477520 и 0.994568 в run 42366566469 (критерий >1% пройден).
- FFmpeg gdigrab записал открываемый MP4 длительностью 10 секунд.
- Каталог данных: `C:\Users\runneradmin\AppData\Local\User Name\com.eliseev.imagegen\Data`.
- Полный сводный отчёт и видео приложены в артефакте `preflight-windows-reports` соответствующего прогона.

### Android (пока частично)

В run 42366566469 APK `ImageGen.Maui-Signed.apk` успешно собран, но Android preflight завершился до запуска эмулятора. Поэтому `resource-id` для `apikey.entry`, скриншот, `adb root`/`remount` и время Android job ещё не проверены. Appium 3.9.0 и UiAutomator2 8.7.3 совместимы и установились; текущая ошибка относится к слишком раннему вызову `driver doctor`, а не установке пакетов.
