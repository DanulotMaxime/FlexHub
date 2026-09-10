param(
    [string]$TaskName = "TopServeurs-VoteReminder"
)

$ErrorActionPreference = "Stop"

$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($null -eq $task) {
    Write-Host "La tâche '$TaskName' n'est pas installée."
    exit 0
}

Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
Write-Host "Tâche '$TaskName' supprimée."
