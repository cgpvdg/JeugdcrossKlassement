param([string]$Version = '1.0.10')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $projectRoot 'artifacts/publish'
dotnet publish (Join-Path $projectRoot 'Jeugdcross.Klassment.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "-p:Version=$Version" -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Publiceren mislukt.' }
foreach ($legacyFile in @('Jeugdcross.Klassment.exe', 'Jeugdcross.Klassment.dll', 'Jeugdcross.Klassment.pdb', 'Jeugdcross.Klassment.deps.json', 'Jeugdcross.Klassment.runtimeconfig.json')) {
 $legacyPath = Join-Path $publishDir $legacyFile
 if (Test-Path -LiteralPath $legacyPath) { Remove-Item -LiteralPath $legacyPath }
}
$compiler = @($env:ISCC_PATH, 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe', 'C:/Program Files/Inno Setup 6/ISCC.exe') | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (!$compiler) { throw 'Inno Setup 6 ontbreekt. Stel ISCC_PATH in of installeer Inno Setup.' }
& $compiler "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer bouwen mislukt.' }
