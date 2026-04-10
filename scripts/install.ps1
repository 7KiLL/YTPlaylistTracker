# ytpt installer for Windows — https://github.com/7KiLL/YTPlaylistTracker
# Usage:
#   irm https://raw.githubusercontent.com/7KiLL/YTPlaylistTracker/main/scripts/install.ps1 | iex
#   & ([scriptblock]::Create((irm https://.../install.ps1))) -Version v0.2.0
#
# Parameters:
#   -Version       Install a specific version (e.g. v0.2.0). Default: latest.
#
# Environment variables:
#   YTPT_VERSION   Install a specific version. Overridden by -Version parameter.

param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$Repo = "7KiLL/YTPlaylistTracker"
$GitHubApi = "https://api.github.com/repos/$Repo/releases"

# ---------------------------------------------------------------------------
# Resolve version
# ---------------------------------------------------------------------------

function Resolve-YtptVersion {
    if ($Version) {
        return $Version
    }
    if ($env:YTPT_VERSION) {
        return $env:YTPT_VERSION
    }

    Write-Host "Fetching latest release..."
    try {
        $release = Invoke-RestMethod -Uri "$GitHubApi/latest" -UseBasicParsing
        if (-not $release.tag_name) {
            throw "No tag_name in response"
        }
        return $release.tag_name
    }
    catch {
        Write-Error "Failed to determine latest release from GitHub API: $_"
        exit 1
    }
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

$Version = Resolve-YtptVersion
$Rid = "win-x64"
$Asset = "ytpt-${Rid}.zip"
$DownloadUrl = "https://github.com/$Repo/releases/download/$Version/$Asset"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\ytpt"

Write-Host ""
Write-Host "  ytpt installer"
Write-Host "  =============="
Write-Host "  OS:          Windows"
Write-Host "  Arch:        x64"
Write-Host "  RID:         $Rid"
Write-Host "  Version:     $Version"
Write-Host "  Install dir: $InstallDir"
Write-Host ""

# Download
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "ytpt-install-$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
$ZipPath = Join-Path $TempDir $Asset

try {
    Write-Host "Downloading $Asset ($Version)..."
    try {
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $ZipPath -UseBasicParsing
    }
    catch {
        Write-Error "Download failed: $_ — is the version '$Version' correct?"
        exit 1
    }

    # Extract
    Write-Host "Extracting..."
    $ExtractDir = Join-Path $TempDir "extracted"
    Expand-Archive -Path $ZipPath -DestinationPath $ExtractDir -Force

    # Find ytpt.exe — it may be at the root or in a subdirectory
    $Binary = Get-ChildItem -Path $ExtractDir -Filter "ytpt.exe" -Recurse -File | Select-Object -First 1
    if (-not $Binary) {
        Write-Error "Archive does not contain a 'ytpt.exe' binary"
        exit 1
    }

    # Install
    if (Test-Path $InstallDir) {
        Remove-Item -Path (Join-Path $InstallDir "ytpt.exe") -Force -ErrorAction SilentlyContinue
    }
    else {
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    }

    Copy-Item -Path $Binary.FullName -Destination (Join-Path $InstallDir "ytpt.exe") -Force
    Write-Host "Installed ytpt to $InstallDir\ytpt.exe"
}
finally {
    # Cleanup temp files
    Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# Add to PATH if needed
# ---------------------------------------------------------------------------

$UserPath = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
if ($UserPath -notlike "*$InstallDir*") {
    Write-Host ""
    Write-Host "Adding '$InstallDir' to user PATH..."
    $NewPath = "$InstallDir;$UserPath"
    [System.Environment]::SetEnvironmentVariable("Path", $NewPath, [System.EnvironmentVariableTarget]::User)

    # Also update current session so ytpt is immediately available
    $env:Path = "$InstallDir;$env:Path"
    Write-Host "Done. The PATH change will persist for future sessions."
}

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "ytpt $Version installed successfully!"
Write-Host "Run 'ytpt --help' to get started."
Write-Host ""
