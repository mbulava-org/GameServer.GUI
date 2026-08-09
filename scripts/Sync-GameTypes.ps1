# GameType Synchronization Script
# Syncs all GameTypes and extended metadata from source to target

param(
    [string]$SourceBaseUrl = "http://192.168.10.50:5164",
    [string]$TargetBaseUrl = "http://192.168.10.50:5163"
)

$ErrorActionPreference = "Continue"

Write-Host "🔄 Starting GameType synchronization..." -ForegroundColor Cyan
Write-Host "   Source: $SourceBaseUrl" -ForegroundColor Gray
Write-Host "   Target: $TargetBaseUrl" -ForegroundColor Gray
Write-Host ""

# 1. Get all GameTypes from source
Write-Host "📥 Fetching GameTypes from source..." -ForegroundColor Cyan
try {
    $sourceGameTypes = Invoke-RestMethod -Uri "$SourceBaseUrl/api/gametypes" -Method Get -ContentType "application/json"
    $gameTypeCount = $sourceGameTypes.Count
    Write-Host "✅ Found $gameTypeCount GameTypes on source" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "❌ Failed to get GameTypes from source: $_" -ForegroundColor Red
    exit 1
}

$successCount = 0
$errorCount = 0

# 2. Sync each GameType
foreach ($gameType in $sourceGameTypes) {
    $key = $gameType.key
    $displayName = $gameType.displayName
    
    Write-Host "🔄 Syncing: $displayName ($key)" -ForegroundColor Cyan

    try {
        # 2a. Create/Update GameType on target
        $gameTypeJson = $gameType | ConvertTo-Json -Depth 10
        try {
            $response = Invoke-RestMethod -Uri "$TargetBaseUrl/api/gametypes/$key" `
                -Method Put `
                -Body $gameTypeJson `
                -ContentType "application/json"
            
            Write-Host "   ✅ GameType synced" -ForegroundColor Green
        }
        catch {
            Write-Host "   ⚠️  GameType sync failed: $_" -ForegroundColor Yellow
            $errorCount++
            Write-Host ""
            continue
        }

        # 2b. Get extended metadata from source
        try {
            $extendedMetadata = Invoke-RestMethod -Uri "$SourceBaseUrl/api/gametypes/extended/$key" `
                -Method Get `
                -ContentType "application/json"
            
            # 2c. Save extended metadata to target
            $metadataJson = $extendedMetadata | ConvertTo-Json -Depth 10
            try {
                $metadataResponse = Invoke-RestMethod -Uri "$TargetBaseUrl/api/gametypes/extended/$key" `
                    -Method Post `
                    -Body $metadataJson `
                    -ContentType "application/json"
                
                Write-Host "   ✅ Extended metadata synced" -ForegroundColor Green
            }
            catch {
                Write-Host "   ⚠️  Extended metadata sync failed: $_" -ForegroundColor Yellow
            }
        }
        catch {
            if ($_.Exception.Response.StatusCode -eq 404) {
                Write-Host "   ℹ️  No extended metadata found" -ForegroundColor Gray
            }
            else {
                Write-Host "   ⚠️  Extended metadata error: $_" -ForegroundColor Yellow
            }
        }

        $successCount++
        Write-Host ""
    }
    catch {
        Write-Host "   ❌ Error: $_" -ForegroundColor Red
        $errorCount++
        Write-Host ""
    }
}

# Summary
Write-Host ("=" * 50) -ForegroundColor Gray
Write-Host "✅ Successfully synced: $successCount/$gameTypeCount" -ForegroundColor Green
if ($errorCount -gt 0) {
    Write-Host "❌ Failed: $errorCount/$gameTypeCount" -ForegroundColor Red
}
Write-Host ("=" * 50) -ForegroundColor Gray

if ($errorCount -gt 0) {
    exit 1
}
