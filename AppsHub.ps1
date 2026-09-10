param([switch]$Open)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$createdNew = $false
$mutex = [Threading.Mutex]::new($true, "Local\PersonalAppsHub", [ref]$createdNew)
if (-not $createdNew) { exit 0 }

$configPath = Join-Path $PSScriptRoot "hub-config.json"
$script:config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$script:selectedApp = $null
$script:allowExit = $false

function Save-Config {
    $script:config | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $configPath -Encoding UTF8
}

function Install-AppTask([object]$app) {
    $scriptPath = Join-Path $PSScriptRoot $app.script
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Script introuvable : $scriptPath"
    }

    $escapedUrl = ([string]$app.url).Replace('"', '\"')
    $arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`" -Url `"$escapedUrl`" -DisplaySeconds $($app.displaySeconds)"
    $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $arguments
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes $app.intervalMinutes)
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
    $task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description $app.description
    Register-ScheduledTask -TaskName $app.taskName -InputObject $task -Force | Out-Null
    if (-not $app.enabled) { Disable-ScheduledTask -TaskName $app.taskName | Out-Null }
}

$xamlPath = Join-Path $PSScriptRoot "HubWindow.xaml"
[xml]$xaml = Get-Content -LiteralPath $xamlPath -Raw

$reader = [System.Xml.XmlNodeReader]::new($xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)
$appList = $window.FindName("AppList")
$appTitle = $window.FindName("AppTitle")
$appDescription = $window.FindName("AppDescription")
$enabledCheck = $window.FindName("EnabledCheck")
$intervalBox = $window.FindName("IntervalBox")
$durationBox = $window.FindName("DurationBox")
$urlBox = $window.FindName("UrlBox")
$statusText = $window.FindName("StatusText")
$statusPanel = $window.FindName("StatusPanel")
$saveButton = $window.FindName("SaveButton")
$runButton = $window.FindName("RunButton")
$titleBar = $window.FindName("TitleBar")
$minimizeButton = $window.FindName("MinimizeButton")
$closeWindowButton = $window.FindName("CloseWindowButton")
$logoImage = $window.FindName("LogoImage")

$logoPath = Join-Path $PSScriptRoot "assets\app-logo.png"
$logoBitmap = [System.Windows.Media.Imaging.BitmapImage]::new()
$logoBitmap.BeginInit()
$logoBitmap.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
$logoBitmap.UriSource = [Uri]::new($logoPath)
$logoBitmap.EndInit()
$logoBitmap.Freeze()
$logoImage.Source = $logoBitmap
$window.Icon = $logoBitmap

foreach ($app in $script:config.applications) { $null = $appList.Items.Add($app.name) }

function Show-SelectedApp {
    $index = $appList.SelectedIndex
    if ($index -lt 0) { return }
    $script:selectedApp = $script:config.applications[$index]
    $appTitle.Text = $script:selectedApp.name
    $appDescription.Text = $script:selectedApp.description
    $enabledCheck.IsChecked = [bool]$script:selectedApp.enabled
    $intervalBox.Text = [string]$script:selectedApp.intervalMinutes
    $durationBox.Text = [string]$script:selectedApp.displaySeconds
    $urlBox.Text = [string]$script:selectedApp.url
    $statusText.Text = ""
    $statusPanel.Visibility = "Collapsed"
}

function Show-Hub {
    $window.Show()
    $window.WindowState = "Normal"
    $window.Activate()
}

$appList.Add_SelectionChanged({ Show-SelectedApp })
$titleBar.Add_MouseLeftButtonDown({
    if ($_.ChangedButton -eq [System.Windows.Input.MouseButton]::Left) { $window.DragMove() }
})
$minimizeButton.Add_Click({ $window.WindowState = "Minimized" })
$closeWindowButton.Add_Click({ $window.Close() })

$saveButton.Add_Click({
    try {
        $interval = 0; $duration = 0
        if (-not [int]::TryParse($intervalBox.Text, [ref]$interval) -or $interval -lt 1) { throw "L'intervalle doit être un nombre supérieur à 0." }
        if (-not [int]::TryParse($durationBox.Text, [ref]$duration) -or $duration -lt 1) { throw "La durée doit être un nombre supérieur à 0." }
        if (-not [Uri]::IsWellFormedUriString($urlBox.Text, [UriKind]::Absolute)) { throw "L'adresse de la page n'est pas valide." }

        $script:selectedApp.enabled = [bool]$enabledCheck.IsChecked
        $script:selectedApp.intervalMinutes = $interval
        $script:selectedApp.displaySeconds = $duration
        $script:selectedApp.url = $urlBox.Text.Trim()
        Install-AppTask $script:selectedApp
        Save-Config
        $statusPanel.Visibility = "Visible"
        $statusPanel.Background = "#FF123B36"
        $statusPanel.BorderBrush = "#FF247F6A"
        $statusText.Foreground = "#FF7CE4C2"
        $statusText.Text = "Configuration enregistrée. Prochaine exécution dans environ $interval minutes."
    } catch {
        $statusPanel.Visibility = "Visible"
        $statusPanel.Background = "#FF3A2020"
        $statusPanel.BorderBrush = "#FF985050"
        $statusText.Foreground = "#FFFFB4A8"
        $statusText.Text = $_.Exception.Message
    }
})

$runButton.Add_Click({
    try {
        $scriptPath = Join-Path $PSScriptRoot $script:selectedApp.script
        $arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`" -Url `"$($urlBox.Text)`" -DisplaySeconds $($durationBox.Text)"
        Start-Process -FilePath "powershell.exe" -ArgumentList $arguments
        $statusPanel.Visibility = "Visible"
        $statusPanel.Background = "#FF123B36"
        $statusPanel.BorderBrush = "#FF247F6A"
        $statusText.Foreground = "#FF7CE4C2"
        $statusText.Text = "Notification de test lancée."
    } catch {
        $statusPanel.Visibility = "Visible"
        $statusPanel.Background = "#FF3A2020"
        $statusPanel.BorderBrush = "#FF985050"
        $statusText.Foreground = "#FFFFB4A8"
        $statusText.Text = $_.Exception.Message
    }
})

$window.Add_Closing({ param($sender, $eventArgs)
    if (-not $script:allowExit) {
        $eventArgs.Cancel = $true
        $window.Hide()
    }
})

$trayIcon = [System.Windows.Forms.NotifyIcon]::new()
$trayBitmap = [System.Drawing.Bitmap]::FromFile($logoPath)
$trayIconHandle = $trayBitmap.GetHicon()
$trayAppIcon = [System.Drawing.Icon]::FromHandle($trayIconHandle).Clone()
$trayBitmap.Dispose()
$trayIcon.Icon = $trayAppIcon
$trayIcon.Text = "Mes applications"
$trayIcon.Visible = $true
$menu = [System.Windows.Forms.ContextMenuStrip]::new()
$openItem = $menu.Items.Add("Ouvrir le hub")
$exitItem = $menu.Items.Add("Quitter")
$openItem.Add_Click({ Show-Hub })
$exitItem.Add_Click({
    $script:allowExit = $true
    $trayIcon.Visible = $false
    $window.Close()
})
$trayIcon.ContextMenuStrip = $menu
$trayIcon.Add_DoubleClick({ Show-Hub })

$appList.SelectedIndex = 0
if ($Open) { Show-Hub } else { $window.Hide() }

$app = [System.Windows.Application]::new()
$app.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
$window.Add_Closed({ $app.Shutdown() })
try { $null = $app.Run() } finally {
    $trayIcon.Dispose()
    $trayAppIcon.Dispose()
    $menu.Dispose()
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
