[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\publish'),
    [switch]$BuildInstaller
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'src\SeedDormitoryCorridor.App\SeedDormitoryCorridor.App.csproj'
$output = [System.IO.Path]::GetFullPath($OutputDirectory)

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $output `
    -p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published SeedDormitoryCorridor to $output"

if ($BuildInstaller) {
    $candidates = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    $iscc = $candidates | Select-Object -First 1
    if (-not $iscc) {
        throw 'Inno Setup 6 (ISCC.exe) was not found. Install it or omit -BuildInstaller.'
    }

    & $iscc (Join-Path $repositoryRoot 'installer\DesktopPet.iss')
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }
}
