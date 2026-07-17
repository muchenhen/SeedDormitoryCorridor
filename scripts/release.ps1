[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\release')
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'src\SeedDormitoryCorridor.App\SeedDormitoryCorridor.App.csproj'
$releaseRoot = [System.IO.Path]::GetFullPath($OutputDirectory)

$branch = (& git -C $repositoryRoot branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or $branch -ne 'main') {
    throw "Formal releases must be built from the main branch; current branch is '$branch'."
}

$worktreeChanges = & git -C $repositoryRoot status --porcelain
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect the Git worktree (exit code $LASTEXITCODE)."
}
if ($worktreeChanges) {
    throw 'Formal releases require a clean Git worktree. Commit or stash all changes first.'
}

$evaluatedVersion = & dotnet msbuild $project -nologo -getProperty:Version
if ($LASTEXITCODE -ne 0) {
    throw "Could not evaluate the project version (exit code $LASTEXITCODE)."
}
$projectVersion = ($evaluatedVersion | Where-Object { $_.Trim() } | Select-Object -Last 1).Trim()

if (-not $Version) {
    $Version = $projectVersion
} elseif ($Version -ne $projectVersion) {
    throw "Requested version '$Version' does not match the project version '$projectVersion'. Update Directory.Build.props first."
}

$semanticVersionPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:alpha|beta|rc)\.(0|[1-9]\d*))?$'
if ($Version -notmatch $semanticVersionPattern) {
    throw "Version '$Version' must use MAJOR.MINOR.PATCH, optionally followed by -alpha.N, -beta.N, or -rc.N."
}

$stagingRoot = Join-Path $releaseRoot ".staging-$Version"
$releaseOutput = Join-Path $releaseRoot $Version
$publishOutput = Join-Path $stagingRoot 'publish'
$installerOutput = Join-Path $stagingRoot 'installer'
$portableFolderName = "SeedDormitoryCorridor-$Version-win-x64-portable"
$portableRoot = Join-Path (Join-Path $stagingRoot 'portable') $portableFolderName
$portableArchive = Join-Path $releaseOutput "$portableFolderName.zip"
$installerName = "SeedDormitoryCorridor-$Version-win-x64-setup.exe"
$installerArtifact = Join-Path $releaseOutput $installerName

foreach ($path in @($stagingRoot, $releaseOutput)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseOutput -Force | Out-Null

try {
    & (Join-Path $PSScriptRoot 'publish.ps1') `
        -OutputDirectory $publishOutput `
        -Version $Version `
        -BuildInstaller `
        -InstallerOutputDirectory $installerOutput
    if ($LASTEXITCODE -ne 0) {
        throw "publish.ps1 failed with exit code $LASTEXITCODE."
    }

    Copy-Item -Path (Join-Path $publishOutput '*') -Destination $portableRoot -Recurse
    Compress-Archive -LiteralPath $portableRoot -DestinationPath $portableArchive -CompressionLevel Optimal

    $builtInstaller = Join-Path $installerOutput $installerName
    if (-not (Test-Path -LiteralPath $builtInstaller)) {
        throw "Expected installer was not produced: $builtInstaller"
    }
    Copy-Item -LiteralPath $builtInstaller -Destination $installerArtifact

    $checksumLines = foreach ($artifact in @($installerArtifact, $portableArchive)) {
        $hash = Get-FileHash -LiteralPath $artifact -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), [System.IO.Path]::GetFileName($artifact)
    }
    $checksumPath = Join-Path $releaseOutput 'SHA256SUMS.txt'
    $checksumLines | Set-Content -LiteralPath $checksumPath -Encoding ascii

    Write-Host "Release artifacts for ${Version}:"
    Get-ChildItem -LiteralPath $releaseOutput -File | Select-Object Name, Length, LastWriteTime
} finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
