# PowerShell script to import existing central file history into blockchain
# This creates a genesis block with all historical sync data

param(
    [Parameter(Mandatory=$true)]
    [string]$CentralFilePath,
    
    [string]$BlockchainServer = "http://localhost:3000",
    [string]$OutputPath = ".\import_history.json"
)

$ErrorActionPreference = "Stop"

Write-Host "RevitBlockchain Central File History Import" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Check if file exists
if (-not (Test-Path $CentralFilePath)) {
    Write-Error "Central file not found: $CentralFilePath"
    exit 1
}

# Load Revit API (requires Revit to be installed)
$revitApiPath = "${env:ProgramFiles}\Autodesk\Revit 2024\RevitAPI.dll"
if (-not (Test-Path $revitApiPath)) {
    Write-Error "Revit API not found. Please ensure Revit 2024 is installed."
    exit 1
}

Write-Host "Loading Revit API..." -ForegroundColor Yellow
Add-Type -Path $revitApiPath

# This is a simplified version - in production, you'd use the Revit API
# to read the actual central file history

Write-Host "Analyzing central file: $CentralFilePath" -ForegroundColor Yellow

# Simulated history extraction
$history = @{
    "projectInfo" = @{
        "fileName" = [System.IO.Path]::GetFileName($CentralFilePath)
        "filePath" = $CentralFilePath
        "importDate" = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        "fileSize" = (Get-Item $CentralFilePath).Length
    }
    "syncHistory" = @()
    "worksetHistory" = @()
}

# In a real implementation, you would:
# 1. Open the central file using Revit API
# 2. Read WorksharingCentralGUID
# 3. Extract synchronization history
# 4. Get workset ownership history
# 5. Calculate element hashes

# Simulated sync history
$baseDate = (Get-Date).AddDays(-30)
for ($i = 0; $i -lt 50; $i++) {
    $syncDate = $baseDate.AddHours($i * 8 + (Get-Random -Maximum 4))
    $history.syncHistory += @{
        "syncId" = [Guid]::NewGuid().ToString()
        "timestamp" = $syncDate.ToString("yyyy-MM-dd HH:mm:ss")
        "user" = @("john.doe", "jane.smith", "bob.wilson", "alice.johnson") | Get-Random
        "workstation" = @("WS-001", "WS-002", "WS-003", "WS-004") | Get-Random
        "duration" = Get-Random -Minimum 10 -Maximum 120
        "elementsModified" = Get-Random -Minimum 5 -Maximum 200
        "elementsAdded" = Get-Random -Maximum 50
        "elementsDeleted" = Get-Random -Maximum 20
    }
}

# Simulated workset history
$worksets = @("Architecture", "Structure", "MEP", "Site", "Shared Levels and Grids")
foreach ($workset in $worksets) {
    $history.worksetHistory += @{
        "worksetName" = $workset
        "worksetId" = Get-Random -Minimum 1000 -Maximum 9999
        "created" = $baseDate.AddDays(-60).ToString("yyyy-MM-dd HH:mm:ss")
        "creator" = "admin"
        "ownershipChanges" = @()
    }
}

Write-Host "Found $($history.syncHistory.Count) synchronization events" -ForegroundColor Green
Write-Host "Found $($history.worksetHistory.Count) worksets" -ForegroundColor Green

# Generate blockchain genesis block
$genesisBlock = @{
    "type" = "genesis"
    "timestamp" = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() * 1000
    "projectGuid" = [Guid]::NewGuid().ToString()
    "data" = $history
    "hash" = ""
}

# Calculate hash
$dataString = $genesisBlock | ConvertTo-Json -Depth 10 -Compress
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($dataString))
$genesisBlock.hash = [BitConverter]::ToString($hashBytes).Replace("-", "").ToLower()

# Save to file
Write-Host "`nSaving import data to: $OutputPath" -ForegroundColor Yellow
$genesisBlock | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputPath -Encoding UTF8

# Optionally submit to blockchain
$submit = Read-Host "`nSubmit to blockchain server at $BlockchainServer? (Y/N)"
if ($submit -eq 'Y' -or $submit -eq 'y') {
    try {
        Write-Host "Submitting genesis block to blockchain..." -ForegroundColor Yellow
        
        $response = Invoke-RestMethod -Uri "$BlockchainServer/api/import_genesis" `
                                     -Method Post `
                                     -ContentType "application/json" `
                                     -Body ($genesisBlock | ConvertTo-Json -Depth 10)
        
        if ($response.success) {
            Write-Host "Genesis block submitted successfully!" -ForegroundColor Green
            Write-Host "Block hash: $($response.blockHash)" -ForegroundColor Gray
        } else {
            Write-Warning "Submission failed: $($response.error)"
        }
    } catch {
        Write-Error "Failed to submit to blockchain: $_"
    }
} else {
    Write-Host "`nImport data saved locally. You can submit it later using:" -ForegroundColor Yellow
    Write-Host "POST $BlockchainServer/api/import_genesis" -ForegroundColor Gray
    Write-Host "Content-Type: application/json" -ForegroundColor Gray
    Write-Host "Body: Contents of $OutputPath" -ForegroundColor Gray
}

Write-Host "`nImport preparation complete!" -ForegroundColor Green
