# Install script for the Revit BIM Intelligence add-in.
# Copies the built DLL and the .addin manifest into Revit's user Addins folder.
#
# Usage (from the project root, after `dotnet build`):
#     powershell -ExecutionPolicy Bypass -File .\install.ps1
#
# Optional parameters:
#     -RevitVersion 2026     # Target a different Revit version
#     -Configuration Release # Use Release build instead of Debug

param(
    [string]$RevitVersion = "2026",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

# Resolve paths relative to this script so the install works from any machine
$projectRoot  = $PSScriptRoot
$dllSource    = Join-Path $projectRoot "src\bin\$Configuration\net8.0-windows\RevitBIMIntelligence.dll"
$addinSource  = Join-Path $projectRoot "RevitBIMIntelligence.addin"
$addinsPath   = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"

if (-not (Test-Path $dllSource)) {
    Write-Error "DLL not found at $dllSource. Run 'dotnet build src/RevitBIMIntelligence.csproj -c $Configuration' first."
}

if (-not (Test-Path $addinsPath)) {
    New-Item -ItemType Directory -Force -Path $addinsPath | Out-Null
    Write-Host "Created directory: $addinsPath"
}

Copy-Item $dllSource   $addinsPath -Force
Write-Host "Copied: RevitBIMIntelligence.dll"

Copy-Item $addinSource $addinsPath -Force
Write-Host "Copied: RevitBIMIntelligence.addin"

Write-Host ""
Write-Host "Installation complete. Files installed to: $addinsPath"
Write-Host ""
Get-ChildItem $addinsPath | Format-Table Name, Length, LastWriteTime
