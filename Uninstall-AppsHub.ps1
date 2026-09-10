param([string]$TaskName = "Personal-AppsHub")

$ErrorActionPreference = "Stop"
$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($null -ne $task) { Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false }
Write-Host "Démarrage automatique du hub supprimé. Quittez aussi le hub depuis son icône s'il est en cours d'exécution."
