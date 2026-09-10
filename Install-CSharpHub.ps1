param([switch]$SelfContained)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\PersonalAppsHub\PersonalAppsHub.csproj"
$publish = Join-Path $PSScriptRoot "publish\PersonalAppsHub"
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

Get-Process PersonalAppsHub -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

dotnet publish $project -c Release -r win-x64 --self-contained $selfContainedValue -o $publish
if ($LASTEXITCODE -ne 0) { throw "La compilation du Hub a échoué." }

$exe = Join-Path $publish "PersonalAppsHub.exe"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
try {
    $startWithWindows = $true
    $settingsPath = Join-Path $env:APPDATA "PersonalAppsHub\settings.json"
    if (Test-Path -LiteralPath $settingsPath) {
        $savedSettings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
        if ($null -ne $savedSettings.StartWithWindows) {
            $startWithWindows = [bool]$savedSettings.StartWithWindows
        }
    }

    if ($startWithWindows) {
        New-ItemProperty -Path $runKey -Name "PersonalAppsHub" -Value "`"$exe`"" -PropertyType String -Force | Out-Null
    }
    else {
        Remove-ItemProperty -Path $runKey -Name "PersonalAppsHub" -ErrorAction SilentlyContinue
    }
}
catch {
    Write-Warning "L'entrée de démarrage automatique n'a pas pu être mise à jour. La publication continue."
}

# La version C# remplace les anciennes tâches sans les supprimer.
foreach ($legacyTask in @("Personal-AppsHub", "TopServeurs-VoteReminder")) {
    if (Get-ScheduledTask -TaskName $legacyTask -ErrorAction SilentlyContinue) {
        Disable-ScheduledTask -TaskName $legacyTask | Out-Null
    }
}

try {
    Get-CimInstance Win32_Process | Where-Object { $_.Name -eq "powershell.exe" -and $_.CommandLine -match "AppsHub\.ps1" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
}
catch {
    Write-Warning "La recherche de l'ancien Hub PowerShell n'est pas autorisée."
}
$shell = New-Object -ComObject Shell.Application
$shell.ShellExecute($exe)
[void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
Write-Host "Hub C# compilé, installé et démarré : $exe"
