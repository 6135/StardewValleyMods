<#
.SYNOPSIS
    Stamps the versions from a release branch name into the matching mods before CI builds them.

.DESCRIPTION
    Release branches are named release/<mod>/<version>, e.g. release/ProfitCalculator/2.1.0 or
    release/cp-sweet-mines/v1.0.1. To publish several mods in one GitHub release, join them with '+':
    release/UIFramework/1.2.0+ProfitCalculator/2.1.0.

    <mod> is matched against the .csproj names ignoring case and anything that isn't a letter or digit, so
    "cp-sweet-mines" matches "[CP] Sweet Mines.csproj".

    For a release branch this sets <Version> in each mod's .csproj. Their manifest.json files use
    "%ProjectVersion%", so ModBuildConfig carries the version into the release zips. Any other branch is left
    untouched.

    When run in GitHub Actions it writes these step outputs:
        is-release  'true' or 'false'
        release     (release only) compact JSON describing the release:
                    { tag, title, prerelease, mods: [ { name, version, zip } ] }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Branch
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

function Set-Output([string]$Name, [string]$Value) {
    if ($env:GITHUB_OUTPUT) { "$Name=$Value" | Add-Content -Encoding utf8 $env:GITHUB_OUTPUT }
}

function Get-Slug([string]$Text) {
    return ($Text -replace '[^A-Za-z0-9]', '').ToLowerInvariant()
}

$modPattern = '(?<mod>[^/+]+)/v?(?<version>\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?)'
if ($Branch -notmatch "^release/$modPattern(\+$modPattern)*$") {
    Write-Output "'$Branch' isn't a release/<mod>/<version>[+<mod>/<version>...] branch - not changing any versions."
    Set-Output 'is-release' 'false'
    return
}

$allProjects = @(
    Get-ChildItem -Path $repoRoot -Recurse -Filter *.csproj |
        Where-Object { $_.FullName -notmatch '[\\/](Others|bin|obj)[\\/]' -and (Test-Path -LiteralPath (Join-Path $_.DirectoryName 'manifest.json')) }
)

$mods = @()
$tagParts = @()
foreach ($entry in $Branch.Substring('release/'.Length).Split('+')) {
    $null = $entry -match "^$modPattern$"
    $modSegment = $Matches.mod
    $version = $Matches.version

    $projects = @($allProjects | Where-Object { (Get-Slug $_.BaseName) -eq (Get-Slug $modSegment) })
    if ($projects.Count -ne 1) {
        throw "'$modSegment' in release branch '$Branch' must match exactly one mod project, but matched $($projects.Count)."
    }
    $project = $projects[0]
    if ($mods.name -contains $project.BaseName) {
        throw "Release branch '$Branch' lists $($project.BaseName) more than once."
    }

    $manifestPath = Join-Path $project.DirectoryName 'manifest.json'
    if ([System.IO.File]::ReadAllText($manifestPath) -notmatch '"Version"\s*:\s*"%ProjectVersion%"') {
        throw "'$manifestPath' must set `"Version`": `"%ProjectVersion%`" so the release zip gets the project version."
    }

    $xml = New-Object XML
    $xml.PreserveWhitespace = $true
    $xml.Load($project.FullName)
    $versionElement = $xml.SelectSingleNode('//Version')
    if (-not $versionElement) { throw "'$($project.FullName)' has no <Version> element." }
    $versionElement.InnerText = $version
    $xml.Save($project.FullName)
    Write-Output "Set $($project.BaseName) to version $version."

    $mods += [ordered]@{ name = $project.BaseName; version = $version; zip = "$($project.BaseName) $version.zip" }
    $tagParts += "$modSegment/v$version"
}

$release = [ordered]@{
    tag = $tagParts -join '+'
    title = ($mods | ForEach-Object { "$($_.name) $($_.version)" }) -join ' + '
    prerelease = [bool]($mods | Where-Object { $_.version -like '*-*' })
    mods = $mods
}

Set-Output 'is-release' 'true'
Set-Output 'release' ($release | ConvertTo-Json -Compress -Depth 5)
