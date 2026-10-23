param(
    [string]$FrameViewSdk = "C:\Program Files\NVIDIA Corporation\FrameViewSDK\SDK\Public_Release"
)

$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$installation = & $vswhere -latest -products Microsoft.VisualStudio.Product.BuildTools -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $installation) { throw "Microsoft C++ Build Tools est introuvable." }
if (-not (Test-Path -LiteralPath "$FrameViewSdk\FvSDK.h")) { throw "Le SDK NVIDIA FrameView est introuvable dans $FrameViewSdk." }

$source = Join-Path $PSScriptRoot "FlexHub.FrameViewBridge.cpp"
$output = Join-Path $PSScriptRoot "FlexHub.FrameViewBridge.dll"
$temporaryBase = Join-Path ([IO.Path]::GetTempPath()) ("FlexHub.FrameViewBridge-" + [Guid]::NewGuid().ToString("N"))
$command = 'call "{0}\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64 && cl.exe /nologo /std:c++17 /O2 /EHsc /LD /I"{1}" /Fo"{4}.obj" "{2}" /link /OUT:"{3}" /IMPLIB:"{4}.lib" /LIBPATH:"{1}" FvSDK_x64.lib' -f $installation, $FrameViewSdk, $source, $output, $temporaryBase
try {
    & "$env:ComSpec" /d /s /c $command
    if ($LASTEXITCODE -ne 0) { throw "La compilation du pont FrameView a échoué ($LASTEXITCODE)." }
}
finally {
    foreach ($extension in ".obj", ".lib", ".exp") { [IO.File]::Delete($temporaryBase + $extension) }
}
Write-Host "Pont FrameView compilé : $output"
