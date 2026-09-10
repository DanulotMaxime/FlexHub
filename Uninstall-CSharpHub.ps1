$ErrorActionPreference = "Stop"
Get-Process PersonalAppsHub -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "PersonalAppsHub" -ErrorAction SilentlyContinue
Write-Host "Démarrage automatique du Hub C# supprimé. Les préférences utilisateur sont conservées."
