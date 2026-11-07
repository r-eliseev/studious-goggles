> Исходная спецификация владельца (2026-11-07), без правок, справочно для исполнителя задач 238–243. Действуют поправки из ADR 0029 и `docs/specs/ui-e2e.md`: Appium 3.9.0 + uiautomator2 8.7.3 вместо `appium@4`; на Android `MobileBy.Id`, а не `AccessibilityId`; проекты в `ImageGen/ImageGen.E2E.*`; мок по HTTPS; эмулятор API 33. Папку `docs/specs/ui-e2e-original/` удалить при архивировании задач 238–250.

Техническая спецификация • 7 ноября 2026

# 1. Цель и ограничения

Автоматически запускать UI smoke-тесты после сборки APK и
Windows-приложения, сохранять MP4 каждого теста, скриншоты при ошибке,
UI-дерево, логи, NUnit TRX и Allure HTML. Использовать ubuntu-latest для
Android и windows-latest для Windows без self-hosted runner.

Критичный риск: на windows-latest не гарантирована интерактивная
пользовательская сессия. FlaUI и захват desktop должны пройти отдельный
preflight. Если не проходят — Windows UI job помечается как
инфраструктурно недоступный; нельзя выдавать это за успешные тесты.

# 2. Архитектура

Build Android → APK artifact → ubuntu-latest / Android Emulator / Appium
UiAutomator2 → NUnit + видео → artifacts. Build Windows → unpackaged EXE
или MSIX → windows-latest / FlaUI UIA3 / FFmpeg → NUnit + видео →
artifacts. Report job объединяет Allure-results и создаёт HTML.

# 3. Подготовка приложения

Использовать стабильные AutomationId: login.email, login.password,
login.submit, home.dashboard. Проверять Android accessibility tree в
Appium Inspector: MAUI AutomationId не обязательно совпадает с
AccessibilityId. Создать изолированные тестовые аккаунты и backend; не
сохранять пароли в репозитории.

\<Entry AutomationId="login.email" Placeholder="Email" /\>

\<Entry AutomationId="login.password" IsPassword="True" /\>

\<Button AutomationId="login.submit" Text="Sign in" /\>

\<Label AutomationId="home.dashboard" Text="Welcome" /\>

# 4. Структура репозитория

src/MyApp/MyApp.csproj

tests/E2E.Common/{TestIds.cs,TestConfiguration.cs}

tests/E2E.Windows/{Fixtures,Pages,Tests}

tests/E2E.Android/{Fixtures,Pages,Tests}

tests/scripts/{windows-preflight.ps1,android-preflight.sh}

.github/workflows/ui-e2e.yml

# 5. Windows тесты: NUnit + FlaUI

Пакеты: NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, FlaUI.Core,
FlaUI.UIA3, Allure.NUnit. Запуск приложения через Application.Launch;
ожидания через Retry.WhileNull; локаторы по AutomationId; тесты
NonParallelizable.

using var app = FlaUI.Core.Application.Launch(exe);

using var automation = new FlaUI.UIA3.UIA3Automation();

var window = FlaUI.Core.Tools.Retry.WhileNull(

() =\> app.GetMainWindow(automation),

timeout: TimeSpan.FromSeconds(20)).Result;

Assert.That(window, Is.Not.Null);

var submit = window!.FindFirstDescendant(

cf =\> cf.ByAutomationId("login.submit"));

Assert.That(submit, Is.Not.Null);

submit!.AsButton().Invoke();

# 6. Android тесты: NUnit + Appium

Пакеты: NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk,
Appium.WebDriver, Allure.NUnit. На Ubuntu установить Node, Appium 4 и
совместимый UiAutomator2 driver; запускать Android Emulator Runner с
KVM.

var options = new OpenQA.Selenium.Appium.AppiumOptions {

PlatformName = "Android", AutomationName = "UiAutomator2",

DeviceName = "Android Emulator", App = Path.GetFullPath(apk)

};

using var driver = new OpenQA.Selenium.Appium.Android.AndroidDriver(

new Uri("http://127.0.0.1:4723"), options);

driver.FindElement(OpenQA.Selenium.Appium.AppiumBy

.AccessibilityId("login.submit")).Click();

# 7. Per-test видео и диагностика

## Android

В NUnit SetUp создать папку по Test.ID, запустить
Driver.StartRecordingScreen(); в TearDown при ошибке сохранить
screenshot.png и Driver.PageSource в ui.xml, затем
Driver.StopRecordingScreen() (base64 MP4), сохранить recording.mp4 и
закрыть driver. Каждый шаг сохранения защищать отдельным try/catch.
Учитывать ограничение длительности screenrecord, разбивать длинные
тесты.

Driver.StartRecordingScreen();

// ... тест ...

var video = Driver.StopRecordingScreen();

File.WriteAllBytes("recording.mp4", Convert.FromBase64String(video));

## Windows

На каждый тест запускать FFmpeg gdigrab в SetUp и корректно завершать
через stdin командой q в TearDown. После завершения проверять размер
файла и ffprobe; при ошибке сохранять скриншот и дерево UIA. Не
запускать тесты параллельно: desktop, мышь и клавиатура общие.

ffmpeg -y -f gdigrab -framerate 15 -i desktop \\

-c:v libx264 -preset ultrafast -pix_fmt yuv420p \\

-movflags +frag_keyframe+empty_moov recording.mp4

## Preflight Windows

Проверить интерактивную сессию, доступность UIAutomationClient, запуск
тестового окна, нахождение элемента и получение не-чёрного screenshot.
Если любой шаг провален, останавливать Windows job с явной причиной.

# 8. Allure и артефакты

В каждом тестовом проекте Allure.NUnit. В TearDown после остановки видео
прикрепить MP4, PNG, XML и текстовые логи через AllureApi.AddAttachment.
В GitHub Actions сохранять reports/ через actions/upload-artifact с if:
always() и retention-days: 14. Для HTML объединить allure-results двух
платформ и выполнить allure generate --clean.

Allure.Net.Commons.AllureApi.AddAttachment(

"Video", "video/mp4", File.ReadAllBytes(videoPath), ".mp4");

allure generate reports/allure-results --clean -o reports/allure-html

# 9. Пример GitHub Actions: тестовые jobs

Ниже фрагмент для подключения к уже существующим
build-android/build-windows jobs. Они должны публиковать android-apk и
windows-app. Реальные команды publish зависят от csproj, подписи и
упаковки.

name: MAUI UI E2E

on: \[workflow_dispatch, push, pull_request\]

permissions:

contents: read

jobs:

test-android:

needs: build-android

runs-on: ubuntu-latest

steps:

\- uses: actions/checkout@v4

\- uses: actions/download-artifact@v4

with: {name: android-apk, path: builds/android}

\- uses: actions/setup-dotnet@v4

with: {dotnet-version: '9.0.x'}

\- uses: actions/setup-node@v4

with: {node-version: '24'}

\- run: \|

npm install -g appium@4

appium driver install uiautomator2

\- name: Enable KVM

run: \|

echo 'KERNEL=="kvm", GROUP="kvm", MODE="0666",
OPTIONS+="static_node=kvm"' \| sudo tee
/etc/udev/rules.d/99-kvm4all.rules

sudo udevadm control --reload-rules

sudo udevadm trigger --name-match=kvm

\- uses: reactivecircus/android-emulator-runner@v2

with:

api-level: 35

arch: x86_64

script: \|

mkdir -p reports/android

appium --log reports/android/appium.log &

until curl -fsS http://127.0.0.1:4723/status; do sleep 2; done

export APP_APK="\$(find builds/android -name '\*.apk' \| head -n 1)"

dotnet test tests/E2E.Android --results-directory reports/android

\- if: always()

uses: actions/upload-artifact@v4

with: {name: android-e2e, path: reports/android/}

test-windows:

needs: build-windows

runs-on: windows-latest

steps:

\- uses: actions/checkout@v4

\- uses: actions/download-artifact@v4

with: {name: windows-app, path: builds/windows}

\- uses: actions/setup-dotnet@v4

with: {dotnet-version: '9.0.x'}

\- shell: pwsh

run: ./tests/scripts/windows-preflight.ps1

\- shell: pwsh

run: dotnet test tests/E2E.Windows --results-directory reports/windows

\- if: always()

uses: actions/upload-artifact@v4

with: {name: windows-e2e, path: reports/windows/}

Важно: фрагмент не является самостоятельным валидным workflow без build
jobs. Добавить timeout на ожидание Appium, контроль наличия APK,
настройку APP_EXE, совместимые версии пакетов и секреты. HTML-report job
должен исполняться с if: always() и учитывать отсутствие одного из
артефактов.

# 10. Acceptance criteria

☐ Android APK запускается в эмуляторе и тест нажимает кнопку

☐ Windows preflight доказывает UI Automation и реальный захват desktop

☐ Каждый тест сохраняет отдельный воспроизводимый MP4

☐ При падении сохраняются screenshot и UI-дерево

☐ Есть Appium/Logcat и Windows/FFmpeg логи

☐ Allure HTML содержит результаты и вложения

☐ Артефакты сохраняются при failed test

☐ PR показывает отдельный статус Android и Windows

# 11. Риски и план

Риски: Windows desktop недоступен; MAUI Android locator не совпадает с
AccessibilityId; screenrecord ограничен по длительности; видео может
содержать секреты; flaky-тесты из-за анимаций и сетевых зависимостей.
Меры: preflight, Appium Inspector, сегментация, тестовые данные, явные
ожидания и timeout.

Этапы: (1) preflight инфраструктуры, 0,5–1 день; (2) 3–5 smoke-тестов,
1–2 дня; (3) видео/логи/Allure, 1–2 дня; (4) CI интеграция, 1 день. Срок
ориентировочный.

# 12. Официальные ссылки

MAUI: https://learn.microsoft.com/en-us/dotnet/maui/

MAUI accessibility:
https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility

FlaUI: https://github.com/FlaUI/FlaUI

Appium: https://appium.io/docs/en/latest/

UiAutomator2:
https://appium.github.io/appium-uiautomator2-driver/latest/

Android Emulator Runner:
https://github.com/ReactiveCircus/android-emulator-runner

NUnit: https://docs.nunit.org/

Allure NUnit: https://allurereport.org/docs/nunit/

FFmpeg: https://ffmpeg.org/ffmpeg.html

GitHub Actions: https://docs.github.com/en/actions
