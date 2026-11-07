$ErrorActionPreference = 'Stop'
$reportDir = Join-Path (Get-Location) 'reports/preflight-windows'
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null

$session = ''
try { $session = (& query session 2>&1 | Out-String).Trim() } catch { $session = "query session failed: $($_.Exception.GetType().Name)" }
$identity = (& whoami 2>&1 | Out-String).Trim()
$session = "$identity`n$session"
$screen = $null
$resolution = 'unknown'
$windowFound = $false
$automationIdFound = $false
$nonUniformRatio = 0.0
$ffmpegOk = $false
$dataDir = ''
$app = $null
$failure = $null

try {
    Add-Type -AssemblyName System.Windows.Forms
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $resolution = "$($screen.Width)x$($screen.Height)"
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $exe = Join-Path (Get-Location) 'publish/win-x64/ImageGen.Maui.exe'
    $app = Start-Process -FilePath $exe -PassThru -WorkingDirectory (Split-Path $exe)
    $deadline = (Get-Date).AddSeconds(60)
    $window = $null
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$app.Id)
    while ((Get-Date) -lt $deadline -and $null -eq $window) {
        $app.Refresh()
        if ($app.MainWindowHandle -ne [IntPtr]::Zero) {
            $window = [System.Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle)
        }
        if ($null -eq $window) {
            $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
                [System.Windows.Automation.TreeScope]::Children, $processCondition)
        }
        if ($null -eq $window) { Start-Sleep -Seconds 1 }
    }
    $windowFound = $null -ne $window

    if ($windowFound) {
        $idCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'apikey.entry')
        $entry = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCondition)
        $automationIdFound = $null -ne $entry
    }

    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new($screen.Width, $screen.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($screen.Location, [System.Drawing.Point]::Empty, $screen.Size)
        $bitmap.Save((Join-Path $reportDir 'desktop.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $samples = [System.Collections.Generic.List[System.Drawing.Color]]::new()
        for ($y = 0; $y -lt $bitmap.Height; $y += 4) {
            for ($x = 0; $x -lt $bitmap.Width; $x += 4) { $samples.Add($bitmap.GetPixel($x, $y)) }
        }
        if ($samples.Count -gt 0) {
            $meanR = ($samples | Measure-Object -Property R -Average).Average
            $meanG = ($samples | Measure-Object -Property G -Average).Average
            $meanB = ($samples | Measure-Object -Property B -Average).Average
            $different = 0
            foreach ($pixel in $samples) {
                if ([Math]::Abs($pixel.R - $meanR) -gt 16 -or [Math]::Abs($pixel.G - $meanG) -gt 16 -or [Math]::Abs($pixel.B - $meanB) -gt 16) { $different++ }
            }
            $nonUniformRatio = [Math]::Round($different / $samples.Count, 6)
        }
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    $ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($null -ne $ffmpeg) {
        $recording = Join-Path $reportDir 'recording.mp4'
        & $ffmpeg.Source -y -f gdigrab -framerate 10 -i desktop -t 10 -pix_fmt yuv420p $recording 2> (Join-Path $reportDir 'ffmpeg.log')
        $ffmpegOk = $LASTEXITCODE -eq 0 -and (Test-Path $recording)
    } else {
        'ffmpeg executable not found' | Set-Content (Join-Path $reportDir 'ffmpeg.log')
    }
} catch {
    $failure = $_.Exception.GetType().Name
    $_ | Out-String | Set-Content (Join-Path $reportDir 'error.txt')
} finally {
    if ($null -ne $app) {
        try { if (-not $app.HasExited) { $app.CloseMainWindow() | Out-Null; if (-not $app.WaitForExit(5000)) { $app.Kill() } } } catch { }
    }
    $log = Get-ChildItem -Path $env:LOCALAPPDATA -Filter 'imagegen-*.log' -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -ne $log) { $dataDir = Split-Path (Split-Path $log.FullName -Parent) -Parent }
    [ordered]@{
        session = $session
        resolution = $resolution
        windowFound = $windowFound
        automationIdFound = $automationIdFound
        nonUniformRatio = $nonUniformRatio
        ffmpegOk = $ffmpegOk
        dataDir = $dataDir
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $reportDir 'summary.json')
}

if ($null -ne $failure) { throw "Windows preflight failed: $failure" }
