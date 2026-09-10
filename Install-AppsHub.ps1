param([string]$TaskName = "Personal-AppsHub")

$ErrorActionPreference = "Stop"
$hubScript = Join-Path $PSScriptRoot "AppsHub.ps1"
if (-not (Test-Path -LiteralPath $hubScript -PathType Leaf)) { throw "Fichier introuvable : $hubScript" }

$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$hubScript`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
$task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description "Hub personnel accessible depuis la zone de notification Windows."
Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force | Out-Null
Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$hubScript`" -Open"
Write-Host "Hub installé et démarré. Il se lancera automatiquement à l'ouverture de session."
