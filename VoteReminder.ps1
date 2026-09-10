param(
    [string]$Url = "https://top-serveurs.net/project-zomboid/fr-nextgen-france-project-zomboid-nouveau-serveur",
    [int]$DisplaySeconds = 20
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="Rappel de vote Top-Serveurs"
        Width="360" Height="150"
        WindowStyle="None" ResizeMode="NoResize"
        ShowInTaskbar="False" ShowActivated="False" Topmost="True"
        AllowsTransparency="True" Background="Transparent">
    <Border Background="#FF202124" BorderBrush="#FF3C4043"
            BorderThickness="1" CornerRadius="10" Padding="16">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>
            <TextBlock Grid.Row="0" Text="Rappel Top-Serveurs"
                       Foreground="White" FontWeight="SemiBold" FontSize="16" />
            <TextBlock Grid.Row="1" Margin="0,9,0,10"
                       Text="Le délai de 2 h 10 est écoulé. Vous pouvez maintenant voter manuellement."
                       Foreground="#FFE8EAED" FontSize="13" TextWrapping="Wrap" />
            <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right">
                <Button Name="CloseButton" Content="Fermer" MinWidth="72"
                        Padding="10,5" Margin="0,0,8,0" />
                <Button Name="OpenButton" Content="Ouvrir la page" MinWidth="110"
                        Padding="10,5" Background="#FF8AB4F8" />
            </StackPanel>
        </Grid>
    </Border>
</Window>
"@

$reader = [System.Xml.XmlNodeReader]::new($xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)
$openButton = $window.FindName("OpenButton")
$closeButton = $window.FindName("CloseButton")

$window.Add_SourceInitialized({
    $workArea = [System.Windows.SystemParameters]::WorkArea
    $window.Left = $workArea.Right - $window.Width - 16
    $window.Top = $workArea.Bottom - $window.Height - 16
})

$openButton.Add_Click({
    Start-Process $Url
    $window.Close()
})

$closeButton.Add_Click({ $window.Close() })

$timer = [System.Windows.Threading.DispatcherTimer]::new()
$timer.Interval = [TimeSpan]::FromSeconds([Math]::Max(1, $DisplaySeconds))
$timer.Add_Tick({
    $timer.Stop()
    $window.Close()
})

$window.Add_Loaded({ $timer.Start() })
$window.Add_Closed({ $timer.Stop() })

$null = $window.ShowDialog()
