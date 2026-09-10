$ErrorActionPreference = "Stop"
Get-Process PersonalAppsHub -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process FlexHub -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "PersonalAppsHub" -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "FlexHub" -ErrorAction SilentlyContinue
Write-Host "Démarrage automatique du Hub C# supprimé. Les préférences utilisateur sont conservées."
