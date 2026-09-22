<#
.SYNOPSIS
    Stamps the version from a release branch name into the matching mod before CI builds it.

.DESCRIPTION
    Release branches are named release/<mod>/<version>, e.g. release/ProfitCalculator/2.1.0 or
    release/cp-sweet-mines/v1.0.1. <mod> is matched against the .csproj names ignoring case and anything that
    isn't a letter or digit, so "cp-sweet-mines" matches "[CP] Sweet Mines.csproj".

    For a release branch this sets <Version> in the mod's .csproj. Its manifest.json uses "%ProjectVersion%", so
    ModBuildConfig carries the version into the release zip. Any other branch is left untouched.

    When run in GitHub Actions it writes these step outputs:
        is-release  'true' or 'false'
        mod-name    the .csproj name, which is also the zip's folder/file prefix
        mod-tag     the <mod> segment of the branch name, used for the release tag
        version     the version without a 'v' prefix
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

if ($Branch -notmatch '^release/(?<mod>[^/]+)/v?(?<version>\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?)$') {
    Write-Output "'$Branch' isn't a release/<mod>/<version> branch - not changing any versions."
    Set-Output 'is-release' 'false'
    return
}
$modSegment = $Matches.mod
$version = $Matches.version

$projects = @(
    Get-ChildItem -Path $repoRoot -Recurse -Filter *.csproj |
        Where-Object { $_.FullName -notmatch '[\\/](Others|bin|obj)[\\/]' -and (Test-Path -LiteralPath (Join-Path $_.DirectoryName 'manifest.json')) } |
        Where-Object { (Get-Slug $_.BaseName) -eq (Get-Slug $modSegment) }
)
if ($projects.Count -ne 1) {
    throw "Release branch '$Branch' must match exactly one mod project, but matched $($projects.Count)."
}
$project = $projects[0]

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
Set-Output 'is-release' 'true'
Set-Output 'mod-name' $project.BaseName
Set-Output 'mod-tag' $modSegment
Set-Output 'version' $version
