param(
    [string]$Version,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\PersonalAppsHub\FlexHub.csproj"
$publishDir = Join-Path $root "publish\installer\win-x64"
$installerScript = Join-Path $root "installer\FlexHub.iss"
$outputDir = Join-Path $root "artifacts\installer"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectXml = Get-Content -LiteralPath $project
    $Version = [string]$projectXml.Project.PropertyGroup.Version
}

if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "La version doit avoir le format 1.2.3 ou 1.2.3.4."
}

if (-not $SkipPublish) {
    Write-Host "Publication autonome de FlexHub $Version..."
    dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false `
        -o $publishDir --source "https://api.nuget.org/v3/index.json"
    if ($LASTEXITCODE -ne 0) { throw "La publication .NET a échoué." }
}

$isccCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 est requis. Installez-le avec : winget install --id JRSoftware.InnoSetup -e"
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
& $iscc "/DAppVersion=$Version" "/DSourceDir=$publishDir" "/DOutputDir=$outputDir" $installerScript
if ($LASTEXITCODE -ne 0) { throw "La création de l'installeur a échoué." }

$installer = Join-Path $outputDir "FlexHub-Setup-$Version-x64.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw "L'installeur attendu est introuvable." }
Write-Host "Installeur créé : $installer"
