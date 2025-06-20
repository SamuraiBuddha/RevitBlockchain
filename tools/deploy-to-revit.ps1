# PowerShell script to deploy RevitBlockchain to Revit
# Run as Administrator for best results

param(
    [string]$RevitVersion = "2024",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "RevitBlockchain Deployment Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Paths
$projectRoot = Split-Path -Parent $PSScriptRoot
$addinPath = "$env:ProgramData\Autodesk\Revit\Addins\$RevitVersion"
$dllSource = Join-Path $projectRoot "bin\$Configuration\RevitBlockchain.dll"
$addinSource = Join-Path $projectRoot "RevitBlockchain.addin"

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Warning "Not running as Administrator. Some operations may fail."
}

# Check if Revit is installed
$revitPath = "${env:ProgramFiles}\Autodesk\Revit $RevitVersion"
if (-not (Test-Path $revitPath)) {
    Write-Error "Revit $RevitVersion not found at $revitPath"
    exit 1
}

# Build the project if DLL doesn't exist
if (-not (Test-Path $dllSource)) {
    Write-Host "Building project..." -ForegroundColor Yellow
    
    $msbuildPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe"
    $msbuild = Get-ChildItem -Path $msbuildPath -ErrorAction SilentlyContinue | Select-Object -First 1
    
    if ($msbuild) {
        & $msbuild.FullName "$projectRoot\RevitBlockchain.csproj" /p:Configuration=$Configuration
    } else {
        Write-Error "MSBuild not found. Please build the project in Visual Studio first."
        exit 1
    }
}

# Create add-in directory if it doesn't exist
if (-not (Test-Path $addinPath)) {
    Write-Host "Creating add-in directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $addinPath -Force | Out-Null
}

# Kill Revit if running
$revitProcess = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if ($revitProcess) {
    Write-Host "Closing Revit..." -ForegroundColor Yellow
    $revitProcess | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Copy files
try {
    Write-Host "Copying add-in files..." -ForegroundColor Yellow
    
    # Copy DLL
    Copy-Item -Path $dllSource -Destination $addinPath -Force
    Write-Host "  - Copied RevitBlockchain.dll" -ForegroundColor Green
    
    # Copy .addin manifest
    Copy-Item -Path $addinSource -Destination $addinPath -Force
    Write-Host "  - Copied RevitBlockchain.addin" -ForegroundColor Green
    
    # Copy dependencies if any
    $dependencies = @(
        "Newtonsoft.Json.dll",
        "System.Net.Http.Json.dll"
    )
    
    foreach ($dep in $dependencies) {
        $depPath = Join-Path $projectRoot "bin\$Configuration\$dep"
        if (Test-Path $depPath) {
            Copy-Item -Path $depPath -Destination $addinPath -Force
            Write-Host "  - Copied $dep" -ForegroundColor Green
        }
    }
    
    # Create Resources folder for icons
    $resourcesPath = Join-Path $addinPath "Resources"
    if (-not (Test-Path $resourcesPath)) {
        New-Item -ItemType Directory -Path $resourcesPath -Force | Out-Null
    }
    
    Write-Host "`nDeployment completed successfully!" -ForegroundColor Green
    Write-Host "RevitBlockchain has been installed to Revit $RevitVersion" -ForegroundColor Green
    
    # Prompt to start Revit
    $startRevit = Read-Host "`nWould you like to start Revit now? (Y/N)"
    if ($startRevit -eq 'Y' -or $startRevit -eq 'y') {
        Write-Host "Starting Revit $RevitVersion..." -ForegroundColor Yellow
        Start-Process "$revitPath\Revit.exe"
    }
    
} catch {
    Write-Error "Deployment failed: $_"
    exit 1
}

# Create desktop shortcut for easy deployment
$shortcutPath = [Environment]::GetFolderPath("Desktop") + "\Deploy RevitBlockchain.lnk"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($shortcutPath)
$Shortcut.TargetPath = "powershell.exe"
$Shortcut.Arguments = "-ExecutionPolicy Bypass -File `"$PSCommandPath`""
$Shortcut.WorkingDirectory = $projectRoot
$Shortcut.IconLocation = "$revitPath\Revit.exe"
$Shortcut.Save()
Write-Host "`nCreated desktop shortcut for easy deployment" -ForegroundColor Gray
