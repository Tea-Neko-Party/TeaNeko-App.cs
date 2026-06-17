[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("show", "init", "set", "bump", "release")]
    [string] $Action = "show",

    [Parameter(Position = 1)]
    [string] $Part = "patch",

    [string] $Version,
    [switch] $AllowDirty,
    [switch] $NoCommit,
    [switch] $NoTag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$DefaultVersion = "5.0.0"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$VersionTextPath = Join-Path $RepoRoot "VERSION"
$VersionPropsPath = Join-Path $RepoRoot "Version.props"
$DirectoryBuildPropsPath = Join-Path $RepoRoot "Directory.Build.props"
$VersionDocsDir = Join-Path $RepoRoot "docs\version"
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Write-TextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $Content
    )

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, $Utf8NoBom)
}

function Assert-Version {
    param([Parameter(Mandatory = $true)][string] $Value)

    if ($Value -notmatch "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$") {
        throw "Version must use a.b.c format, for example 5.0.0. Received: $Value"
    }
}

function Get-VersionFromProps {
    if (-not (Test-Path $VersionPropsPath)) {
        return $null
    }

    [xml] $xml = Get-Content -Raw -Path $VersionPropsPath
    foreach ($propertyGroup in $xml.Project.PropertyGroup) {
        if ($propertyGroup.TeaNekoVersion) {
            return ([string] $propertyGroup.TeaNekoVersion).Trim()
        }
    }

    return $null
}

function Get-CurrentVersion {
    $propsVersion = Get-VersionFromProps
    if ($propsVersion) {
        Assert-Version $propsVersion
        return $propsVersion
    }

    if (Test-Path $VersionTextPath) {
        $textVersion = (Get-Content -Raw -Path $VersionTextPath).Trim()
        Assert-Version $textVersion
        return $textVersion
    }

    return $DefaultVersion
}

function Get-VersionPropsContent {
    param([Parameter(Mandatory = $true)][string] $Value)

    return @"
<Project>
  <PropertyGroup>
    <TeaNekoVersion>$Value</TeaNekoVersion>
  </PropertyGroup>
</Project>
"@
}

function Get-DirectoryBuildPropsContent {
    return @"
<Project>
  <Import Project="`$(MSBuildThisFileDirectory)Version.props" Condition="Exists('`$(MSBuildThisFileDirectory)Version.props')" />

  <PropertyGroup>
    <TeaNekoVersion Condition="'`$(TeaNekoVersion)' == ''">0.0.0</TeaNekoVersion>
    <Version>`$(TeaNekoVersion)</Version>
    <VersionPrefix>`$(TeaNekoVersion)</VersionPrefix>
    <PackageVersion>`$(TeaNekoVersion)</PackageVersion>
    <AssemblyVersion>`$(TeaNekoVersion).0</AssemblyVersion>
    <FileVersion>`$(TeaNekoVersion).0</FileVersion>
    <InformationalVersion>`$(TeaNekoVersion)</InformationalVersion>
  </PropertyGroup>
</Project>
"@
}

function Set-ProjectVersion {
    param([Parameter(Mandatory = $true)][string] $Value)

    Assert-Version $Value
    Write-TextFile -Path $VersionTextPath -Content ($Value + [Environment]::NewLine)
    Write-TextFile -Path $VersionPropsPath -Content (Get-VersionPropsContent $Value)
}

function Initialize-VersionFiles {
    $currentVersion = Get-CurrentVersion
    Set-ProjectVersion $currentVersion

    if (-not (Test-Path $DirectoryBuildPropsPath)) {
        Write-TextFile -Path $DirectoryBuildPropsPath -Content (Get-DirectoryBuildPropsContent)
    }

    Write-Host "Initialized version files at $currentVersion"
}

function Ensure-VersionFiles {
    $currentVersion = Get-CurrentVersion
    $changed = $false

    if (-not (Test-Path $VersionTextPath) -or -not (Test-Path $VersionPropsPath)) {
        Set-ProjectVersion $currentVersion
        $changed = $true
    }

    if (-not (Test-Path $DirectoryBuildPropsPath)) {
        Write-TextFile -Path $DirectoryBuildPropsPath -Content (Get-DirectoryBuildPropsContent)
        $changed = $true
    }

    if ($changed) {
        Write-Host "Initialized missing version files at $currentVersion"
    }
}

function Get-BumpedVersion {
    param(
        [Parameter(Mandatory = $true)][string] $Value,
        [Parameter(Mandatory = $true)][string] $PartName
    )

    Assert-Version $Value
    $numbers = $Value.Split(".") | ForEach-Object { [int] $_ }
    $major = $numbers[0]
    $minor = $numbers[1]
    $patch = $numbers[2]

    switch ($PartName.ToLowerInvariant()) {
        "major" {
            $major += 1
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor += 1
            $patch = 0
        }
        "patch" {
            $patch += 1
        }
        default {
            throw "Bump part must be major, minor, or patch. Received: $PartName"
        }
    }

    return "$major.$minor.$patch"
}

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    $gitArguments = @("-C", $RepoRoot) + $Arguments
    & git @gitArguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Invoke-GitText {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [switch] $AllowFailure
    )

    $gitArguments = @("-C", $RepoRoot) + $Arguments
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        if ($AllowFailure) {
            $output = & git @gitArguments 2>$null
        }
        else {
            $output = & git @gitArguments 2>&1
        }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "git $($Arguments -join ' ') failed with exit code $exitCode. $output"
    }

    if ($exitCode -ne 0) {
        return @()
    }

    return @($output)
}

function Assert-GitRepo {
    $topLevel = (Invoke-GitText @("rev-parse", "--show-toplevel") | Select-Object -First 1)
    if (-not $topLevel) {
        throw "This script must be run inside a git repository."
    }
}

function Assert-CleanWorkTree {
    if ($AllowDirty) {
        return
    }

    $status = @(Invoke-GitText @("status", "--porcelain"))
    if ($status.Count -gt 0) {
        throw "Working tree is not clean. Commit or stash changes before release, or pass -AllowDirty."
    }
}

function Get-GitRelativePath {
    param([Parameter(Mandatory = $true)][string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetFullPath($RepoRoot)
    if (-not $rootPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootPath += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $fullPath.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside repository root: $Path"
    }

    return $fullPath.Substring($rootPath.Length).Replace("\", "/")
}

function Test-GitTagExists {
    param([Parameter(Mandatory = $true)][string] $TagName)

    $result = @(Invoke-GitText @("rev-parse", "--verify", "--quiet", "refs/tags/$TagName") -AllowFailure)
    return $result.Count -gt 0
}

function Get-PreviousVersionTag {
    $result = @(Invoke-GitText @("describe", "--tags", "--abbrev=0", "--match", "v[0-9]*.[0-9]*.[0-9]*") -AllowFailure)
    if ($result.Count -eq 0) {
        return $null
    }

    return ([string] ($result | Select-Object -First 1)).Trim()
}

function New-VersionLog {
    param(
        [Parameter(Mandatory = $true)][string] $Value,
        [Parameter(Mandatory = $true)][string] $TagName,
        [string] $PreviousTag
    )

    $docPath = Join-Path $VersionDocsDir "$Value.md"
    $date = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"
    $head = (Invoke-GitText @("rev-parse", "--short", "HEAD") | Select-Object -First 1)

    $logArguments = @("log", "--reverse", "--date=short", "--pretty=format:- %h %ad %s")
    if ($PreviousTag) {
        $logArguments += "$PreviousTag..HEAD"
    }

    $commits = @(Invoke-GitText $logArguments)
    if ($commits.Count -eq 0) {
        $commits = @("- No commits since $PreviousTag")
    }

    $previousLine = if ($PreviousTag) { $PreviousTag } else { "none" }
    $contentLines = @(
        "# $TagName",
        "",
        "- Version: $Value",
        "- Tag: $TagName",
        "- Previous tag: $previousLine",
        "- Generated at: $date",
        "- Source HEAD before release commit: $head",
        "",
        "## Commits",
        ""
    ) + $commits + @("")

    Write-TextFile -Path $docPath -Content ($contentLines -join [Environment]::NewLine)
    return $docPath
}

function Publish-Version {
    param([Parameter(Mandatory = $true)][string] $PartName)

    if ($NoCommit -and -not $NoTag) {
        throw "-NoCommit must be used together with -NoTag."
    }

    Assert-GitRepo
    Assert-CleanWorkTree

    $currentVersion = Get-CurrentVersion
    $releaseVersion = $currentVersion
    if ($PartName.ToLowerInvariant() -ne "current") {
        $releaseVersion = Get-BumpedVersion -Value $currentVersion -PartName $PartName
        Set-ProjectVersion $releaseVersion
    }

    $tagName = "v$releaseVersion"
    if (Test-GitTagExists $tagName) {
        throw "Tag already exists: $tagName"
    }

    $previousTag = Get-PreviousVersionTag
    $docPath = New-VersionLog -Value $releaseVersion -TagName $tagName -PreviousTag $previousTag
    $docRelativePath = Get-GitRelativePath $docPath

    if ($NoCommit) {
        Write-Host "Generated $docRelativePath"
        Write-Host "Skipped commit and tag."
        return
    }

    Invoke-Git (@("add", "--", "VERSION", "Version.props", "Directory.Build.props", $docRelativePath))
    Invoke-Git (@("commit", "-m", "chore(release): $tagName"))

    if ($NoTag) {
        Write-Host "Created release commit for $tagName"
        Write-Host "Skipped git tag."
        return
    }

    Invoke-Git (@("tag", "-a", $tagName, "-m", "Release $tagName"))
    Write-Host "Released $tagName"
    Write-Host "Version log: $docRelativePath"
}

switch ($Action) {
    "show" {
        Ensure-VersionFiles
        Write-Host "Current version: $(Get-CurrentVersion)"
        Write-Host "MSBuild version file: $VersionPropsPath"
        Write-Host "Plain version file: $VersionTextPath"
    }
    "init" {
        Initialize-VersionFiles
    }
    "set" {
        if (-not $Version) {
            throw "Use -Version to set an explicit version, for example: .\scripts\version\version.ps1 set -Version 5.1.0"
        }
        Set-ProjectVersion $Version
        Write-Host "Set version to $Version"
    }
    "bump" {
        $currentVersion = Get-CurrentVersion
        $nextVersion = Get-BumpedVersion -Value $currentVersion -PartName $Part
        Set-ProjectVersion $nextVersion
        Write-Host "Bumped ${Part}: $currentVersion -> $nextVersion"
    }
    "release" {
        Publish-Version $Part
    }
}
