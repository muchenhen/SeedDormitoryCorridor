[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\publish'),
    [string]$Version,
    [switch]$BuildInstaller,
    [string]$InstallerOutputDirectory = (Join-Path $PSScriptRoot '..\installer\Output')
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'src\SeedDormitoryCorridor.App\SeedDormitoryCorridor.App.csproj'
$output = [System.IO.Path]::GetFullPath($OutputDirectory)

if (-not $Version) {
    $evaluatedVersion = & dotnet msbuild $project -nologo -getProperty:Version
    if ($LASTEXITCODE -ne 0) {
        throw "Could not evaluate the project version (exit code $LASTEXITCODE)."
    }

    $Version = ($evaluatedVersion | Where-Object { $_.Trim() } | Select-Object -Last 1).Trim()
}

$semanticVersionPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:alpha|beta|rc)\.(0|[1-9]\d*))?$'
if ($Version -notmatch $semanticVersionPattern) {
    throw "Version '$Version' must use MAJOR.MINOR.PATCH, optionally followed by -alpha.N, -beta.N, or -rc.N."
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}
New-Item -ItemType Directory -Path $output -Force | Out-Null

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $output `
    -p:PublishSingleFile=false `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $output
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $output

Write-Host "Published SeedDormitoryCorridor to $output"

if ($BuildInstaller) {
    $candidates = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    $iscc = $candidates | Select-Object -First 1
    if (-not $iscc) {
        throw 'Inno Setup 6 (ISCC.exe) was not found. Install it or omit -BuildInstaller.'
    }

    $null = $Version -match $semanticVersionPattern
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    $patch = [int]$Matches[3]
    $revision = if ($Version -match '-alpha\.(\d+)$') {
        [int]$Matches[1]
    } elseif ($Version -match '-beta\.(\d+)$') {
        10000 + [int]$Matches[1]
    } elseif ($Version -match '-rc\.(\d+)$') {
        20000 + [int]$Matches[1]
    } else {
        30000
    }

    if ($revision -gt 65535) {
        throw "Version '$Version' cannot be represented as a Windows file version."
    }

    $numericVersion = "$major.$minor.$patch.$revision"
    $installerOutput = [System.IO.Path]::GetFullPath($InstallerOutputDirectory)
    if (Test-Path -LiteralPath $installerOutput) {
        Remove-Item -LiteralPath $installerOutput -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null

    & $iscc `
        "/DMyAppVersion=$Version" `
        "/DMyAppVersionNumeric=$numericVersion" `
        "/DMyAppSourceDir=$output" `
        "/DMyInstallerOutputDir=$installerOutput" `
        (Join-Path $repositoryRoot 'installer\DesktopPet.iss')
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    Write-Host "Built installer in $installerOutput"
}
