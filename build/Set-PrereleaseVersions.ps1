<#
.SYNOPSIS
    Changes each mod's <Version> to a timestamped prerelease version for preview builds.

.DESCRIPTION
    Same convention as Pathoschild's SMAPI-ModBuildWorkflow set-prerelease-versions action:
    a stable version has its patch number incremented, then the prerelease tag is appended or replaced.

        1.0.5        -> 1.0.6-alpha.202609222359
        1.0.6-test   -> 1.0.6-alpha.202609222359

    Projects that set <EnableModZip>false</EnableModZip> or have no <Version> are skipped.
#>
[CmdletBinding()]
param(
    [string]$Tag = "alpha.$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmm'))"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

$projects = Get-ChildItem -Path $repoRoot -Recurse -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '[\\/](Others|bin|obj)[\\/]' }

foreach ($project in $projects) {
    $xml = New-Object XML
    $xml.PreserveWhitespace = $true
    $xml.Load($project.FullName)

    $zipElement = $xml.SelectSingleNode('//EnableModZip')
    if ($zipElement -and $zipElement.InnerText -eq 'false') {
        Write-Output "Skipped $($project.Name): <EnableModZip> is false."
        continue
    }

    $versionElement = $xml.SelectSingleNode('//Version')
    if (-not $versionElement) {
        Write-Output "Skipped $($project.Name): no <Version> element."
        continue
    }

    $oldVersion = $versionElement.InnerText
    if ($oldVersion -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(-(?<prerelease>[0-9A-Za-z.-]+))?$') {
        Write-Warning "Skipped $($project.Name): '$oldVersion' isn't a semantic version."
        continue
    }
    $patch = [int]$Matches.patch
    if (-not $Matches.prerelease) { $patch++ }
    $newVersion = "$($Matches.major).$($Matches.minor).$patch-$Tag"

    $versionElement.InnerText = $newVersion
    $xml.Save($project.FullName)
    Write-Output "Updated $($project.Name): $oldVersion -> $newVersion."
}
