> The owner's original specification (2026-11-07), translated to English on 2026-11-09 without changes in meaning (the unedited original is the `.docx` next to it), for reference for the executor of tasks 238–243. The corrections from ADR 0029 and `docs/specs/ui-e2e.md` apply: Appium 3.9.0 + uiautomator2 8.7.3 instead of `appium@4`; on Android `MobileBy.Id`, not `AccessibilityId`; projects in `ImageGen/ImageGen.E2E.*`; a mock over HTTPS; emulator API 33. Delete the `docs/specs/ui-e2e-original/` folder when archiving tasks 238–250.

Technical specification • November 7, 2026

# 1. Goal and constraints

Automatically run UI smoke tests after building the APK and the
Windows app, save an MP4 of each test, screenshots on error,
the UI tree, logs, NUnit TRX and Allure HTML. Use ubuntu-latest for
Android and windows-latest for Windows without a self-hosted runner.

A critical risk: an interactive user session is not guaranteed on
windows-latest. FlaUI and desktop capture must pass a separate
preflight. If they do not, the Windows UI job is marked as
unavailable for infrastructure reasons; this must not be passed off as successful tests.

# 2. Architecture

Build Android → APK artifact → ubuntu-latest / Android Emulator / Appium
UiAutomator2 → NUnit + video → artifacts. Build Windows → unpackaged EXE
or MSIX → windows-latest / FlaUI UIA3 / FFmpeg → NUnit + video →
artifacts. The report job merges the Allure results and creates HTML.

# 3. Preparing the app

Use stable AutomationIds: login.email, login.password,
login.submit, home.dashboard. Check the Android accessibility tree in
Appium Inspector: a MAUI AutomationId does not necessarily match the
AccessibilityId. Create isolated test accounts and a backend; do not
keep passwords in the repository.

\<Entry AutomationId="login.email" Placeholder="Email" /\>

\<Entry AutomationId="login.password" IsPassword="True" /\>

\<Button AutomationId="login.submit" Text="Sign in" /\>

\<Label AutomationId="home.dashboard" Text="Welcome" /\>

# 4. Repository structure

src/MyApp/MyApp.csproj

tests/E2E.Common/{TestIds.cs,TestConfiguration.cs}

tests/E2E.Windows/{Fixtures,Pages,Tests}

tests/E2E.Android/{Fixtures,Pages,Tests}

tests/scripts/{windows-preflight.ps1,android-preflight.sh}

.github/workflows/ui-e2e.yml

# 5. Windows tests: NUnit + FlaUI

Packages: NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, FlaUI.Core,
FlaUI.UIA3, Allure.NUnit. Launch the app through Application.Launch;
waits through Retry.WhileNull; locators by AutomationId; tests are
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

# 6. Android tests: NUnit + Appium

Packages: NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk,
Appium.WebDriver, Allure.NUnit. On Ubuntu install Node, Appium 4 and
a compatible UiAutomator2 driver; run the Android Emulator Runner with
KVM.

var options = new OpenQA.Selenium.Appium.AppiumOptions {

PlatformName = "Android", AutomationName = "UiAutomator2",

DeviceName = "Android Emulator", App = Path.GetFullPath(apk)

};

using var driver = new OpenQA.Selenium.Appium.Android.AndroidDriver(

new Uri("http://127.0.0.1:4723"), options);

driver.FindElement(OpenQA.Selenium.Appium.AppiumBy

.AccessibilityId("login.submit")).Click();

# 7. Per-test video and diagnostics

## Android

In the NUnit SetUp create a folder by Test.ID, start
Driver.StartRecordingScreen(); in TearDown on error save
screenshot.png and Driver.PageSource to ui.xml, then
Driver.StopRecordingScreen() (base64 MP4), save recording.mp4 and
close the driver. Protect each saving step with a separate try/catch.
Take into account the screenrecord duration limit, split long
tests.

Driver.StartRecordingScreen();

// ... test ...

var video = Driver.StopRecordingScreen();

File.WriteAllBytes("recording.mp4", Convert.FromBase64String(video));

## Windows

For each test start FFmpeg gdigrab in SetUp and stop it properly
through stdin with the q command in TearDown. After it finishes, check the file
size and ffprobe; on error save a screenshot and the UIA tree. Do not
run tests in parallel: the desktop, mouse and keyboard are shared.

ffmpeg -y -f gdigrab -framerate 15 -i desktop \\

-c:v libx264 -preset ultrafast -pix_fmt yuv420p \\

-movflags +frag_keyframe+empty_moov recording.mp4

## Preflight Windows

Check the interactive session, the availability of UIAutomationClient, launching
a test window, finding an element and getting a non-black screenshot.
If any step fails, stop the Windows job with an explicit reason.

# 8. Allure and artifacts

Allure.NUnit in each test project. In TearDown after the video stops,
attach the MP4, PNG, XML and text logs through AllureApi.AddAttachment.
In GitHub Actions save reports/ through actions/upload-artifact with if:
always() and retention-days: 14. For HTML merge the allure-results of the two
platforms and run allure generate --clean.

Allure.Net.Commons.AllureApi.AddAttachment(

"Video", "video/mp4", File.ReadAllBytes(videoPath), ".mp4");

allure generate reports/allure-results --clean -o reports/allure-html

# 9. A GitHub Actions example: test jobs

Below is a fragment to plug into already existing
build-android/build-windows jobs. They must publish android-apk and
windows-app. The real publish commands depend on the csproj, signing and
packaging.

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

Important: the fragment is not a standalone valid workflow without the build
jobs. Add a timeout for waiting for Appium, a check that the APK exists,
setting APP_EXE, compatible package versions and secrets. The HTML report job
must run with if: always() and take into account that one of the
artifacts may be missing.

# 10. Acceptance criteria

☐ The Android APK launches in the emulator and the test taps a button

☐ The Windows preflight proves UI Automation and a real desktop capture

☐ Each test saves a separate playable MP4

☐ On failure a screenshot and the UI tree are saved

☐ There are Appium/Logcat and Windows/FFmpeg logs

☐ The Allure HTML contains results and attachments

☐ Artifacts are saved on a failed test

☐ A PR shows separate Android and Windows statuses

# 11. Risks and plan

Risks: the Windows desktop is unavailable; the MAUI Android locator does not match
the AccessibilityId; screenrecord is limited in duration; a video may
contain secrets; flaky tests because of animations and network dependencies.
Measures: preflight, Appium Inspector, segmentation, test data, explicit
waits and timeouts.

Stages: (1) the infrastructure preflight, 0.5–1 day; (2) 3–5 smoke tests,
1–2 days; (3) video/logs/Allure, 1–2 days; (4) CI integration, 1 day. The timeline
is approximate.

# 12. Official links

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
