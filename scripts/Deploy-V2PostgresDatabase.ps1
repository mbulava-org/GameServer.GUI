param(
    [Parameter(Mandatory = $false)]
    [string]$TargetConnectionString,

    [Parameter(Mandatory = $false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot '..\src\GameServer.DB.PostgreSql\GameServer.DB.PostgreSql.csproj'
$projectPath = [System.IO.Path]::GetFullPath($projectPath)

if (-not (Test-Path $projectPath)) {
    throw "PostgreSQL database project was not found at '$projectPath'."
}

Write-Host 'Restoring local tools...'
dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to restore local .NET tools.'
}

Write-Host 'Building PostgreSQL database project...'
dotnet build $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to build PostgreSQL database project.'
}

$packageName = 'GameServerV2.pgpac'
$packagePath = Join-Path (Split-Path $projectPath -Parent) "bin\$Configuration\net10.0\$packageName"

if (-not (Test-Path $packagePath)) {
    throw "Expected pgpac package was not found at '$packagePath'."
}

Write-Host "Built package: $packagePath"

if ([string]::IsNullOrWhiteSpace($TargetConnectionString)) {
    Write-Host 'No target connection string was provided. Build-only mode complete.'
    return
}

Write-Host 'Publishing PostgreSQL schema with pgpac...'
dotnet tool run pgpac publish -sf $packagePath -tcs $TargetConnectionString
if ($LASTEXITCODE -ne 0) {
    throw 'pgpac publish failed.'
}

Write-Host 'PostgreSQL schema deployment completed successfully.'

