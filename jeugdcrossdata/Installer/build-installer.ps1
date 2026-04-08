param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "jeugdcrossdata.csproj"
$publishDir = Join-Path $projectRoot "artifacts\publish"
$issFile = Join-Path $PSScriptRoot "installer.iss"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

Write-Host "Publishing app ($Configuration, $Runtime)..."
dotnet publish $projectFile -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=false -o $publishDir

$isccCandidates = @(
    $env:ISCC_PATH,
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$isccPath = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $isccPath) {
    throw "Inno Setup Compiler (ISCC.exe) niet gevonden. Installeer Inno Setup 6 of zet ISCC_PATH."
}

Write-Host "Building installer with ISCC..."
& $isccPath "/DMyAppVersion=$Version" $issFile

Write-Host "Klaar. Installer staat in: $projectRoot\artifacts\installer"
