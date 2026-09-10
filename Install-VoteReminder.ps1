param(
    [string]$TaskName = "TopServeurs-VoteReminder",
    [int]$IntervalMinutes = 130
)

$ErrorActionPreference = "Stop"

if ($IntervalMinutes -lt 1) {
    throw "IntervalMinutes doit être supérieur ou égal à 1."
}

$reminderScript = Join-Path $PSScriptRoot "VoteReminder.ps1"
if (-not (Test-Path -LiteralPath $reminderScript -PathType Leaf)) {
    throw "Fichier introuvable : $reminderScript"
}

$action = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$reminderScript`""

$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes)

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable

$principal = New-ScheduledTaskPrincipal `
    -UserId "$env:USERDOMAIN\$env:USERNAME" `
    -LogonType Interactive `
    -RunLevel Limited

$task = New-ScheduledTask `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "Rappelle d'effectuer manuellement un vote Top-Serveurs toutes les 2 h 10."

Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force | Out-Null

Write-Host "Tâche '$TaskName' installée. Premier rappel dans environ une minute, puis toutes les $IntervalMinutes minutes."
Write-Host "Le vote reste manuel : le rappel demande confirmation avant d'ouvrir la page."
