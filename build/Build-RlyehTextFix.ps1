[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,
    [ValidateSet('Release','Debug')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'src\RlyehTextFix\RlyehTextFix.csproj'
$GameDir = [System.IO.Path]::GetFullPath($GameDir)

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $candidate = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return $candidate }
    }
    $command = Get-Command 'MSBuild.exe' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw 'Visual Studio 2022 MSBuild was not found.'
}

if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'rlyehshoujotaix_cl.exe'))) {
    throw "GameDir does not point to a game installation: $GameDir"
}

$MSBuild = Find-MSBuild
& $MSBuild $Project /restore /t:Rebuild /m /v:minimal "/p:Configuration=$Configuration" /p:Platform=AnyCPU "/p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }

$Dll = Join-Path $Root "src\RlyehTextFix\bin\$Configuration\net6.0\RlyehTextFix.dll"
if (-not (Test-Path -LiteralPath $Dll)) { throw "Build output was not found: $Dll" }

$version = [System.Reflection.AssemblyName]::GetAssemblyName($Dll).Version.ToString()
Write-Host "Built: $Dll"
Write-Host "AssemblyVersion: $version"
Write-Host "SHA-256: $((Get-FileHash -LiteralPath $Dll -Algorithm SHA256).Hash.ToLowerInvariant())"
