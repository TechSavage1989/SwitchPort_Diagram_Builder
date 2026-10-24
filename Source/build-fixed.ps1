param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
