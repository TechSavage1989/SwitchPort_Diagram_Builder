param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Invoke-Native {
    param([string]$Command,[Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE" }
}
$repository = Split-Path $PSScriptRoot -Parent
$project = Join-Path $PSScriptRoot 'SwitchportDiagramBuilder\SwitchportDiagramBuilder.csproj'
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository "artifacts\release-$version" }
$publish = Join-Path $repository 'build\windows'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 8 SDK before building.' }
if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw 'Install Python 3 to create and verify the update package.' }
Push-Location $repository
try {
    Invoke-Native dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publish
    Invoke-Native python (Join-Path $repository 'scripts\package_release.py') --publish-dir $publish --output-dir $OutputDirectory
    Write-Host "BUILD SUCCEEDED: $OutputDirectory"
} finally { Pop-Location }
