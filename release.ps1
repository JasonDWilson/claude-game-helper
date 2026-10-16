<#
.SYNOPSIS
  Builds the Claude Game Helper installer and, with -Publish, releases it on GitHub.

.EXAMPLE
  ./release.ps1 -Version 0.2.1            # build releases\ClaudeGameHelper-win-Setup.exe locally
  ./release.ps1 -Version 0.2.1 -Publish   # build and publish GitHub release v0.2.1 (needs `gh auth login`)
#>
param(
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+$')] [string] $Version,
    [switch] $Publish
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Invoke-Step([string] $Name, [scriptblock] $Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit $LASTEXITCODE)." }
}

if ($Publish) {
    # The GitHub release tag is created on the remote's current commit, so everything must be pushed first.
    git fetch -q origin
    if (git status --porcelain) { throw 'Uncommitted changes. Commit them before publishing.' }
    if (git rev-list '@{u}..HEAD') { throw 'Unpushed commits. Run `git push` before publishing.' }
}

$repoUrl = (Select-String -Path 'src/ClaudeGameHelper/Platform/UpdateChecker.cs' -Pattern 'RepoUrl = "([^"]+)"').Matches[0].Groups[1].Value
$publishDir = 'artifacts/publish'
$releasesDir = 'releases'

Invoke-Step 'Restore tools' { dotnet tool restore }
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
Invoke-Step 'Publish' {
    dotnet publish src/ClaudeGameHelper -c Release -r win-x64 --self-contained -p:Version=$Version -o $publishDir
}

if ($Publish) {
    $token = gh auth token
    if ($LASTEXITCODE -ne 0 -or -not $token) { throw 'Not signed in to GitHub. Run: gh auth login' }
    Invoke-Step 'Download previous release (for delta updates)' {
        dotnet vpk download github --repoUrl $repoUrl --token $token -o $releasesDir
    }
}

Invoke-Step 'Pack installer' {
    dotnet vpk pack --packId ClaudeGameHelper --packVersion $Version --packDir $publishDir `
        --mainExe ClaudeGameHelper.exe --packTitle 'Claude Game Helper' --icon src/ClaudeGameHelper/app.ico -o $releasesDir
}

if ($Publish) {
    Invoke-Step 'Upload GitHub release' {
        dotnet vpk upload github --repoUrl $repoUrl --token $token --publish `
            --releaseName "Claude Game Helper $Version" --tag "v$Version" -o $releasesDir
    }
    Write-Host "Published v$Version to $repoUrl/releases" -ForegroundColor Green
}
else {
    Write-Host "Installer: $releasesDir/ClaudeGameHelper-win-Setup.exe" -ForegroundColor Green
}
