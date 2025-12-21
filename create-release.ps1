$token = Read-Host "Please enter your GitHub Personal Access Token" -AsSecureString
$tokenPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($token))

$headers = @{
    "Authorization" = "Bearer $tokenPlain"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$releaseBody = @{
    tag_name = "v7.4.0.3"
    target_commitish = "main"
    name = "v7.4.0.3 - Complete Config UI Chinese Translation"
    body = "## Updates`n- Complete config tab UI translation`n- Fix terminology: Umbral -> Lingfeng`n- Collectable settings translation`n- Scrip shop UI translation"
    draft = $false
    prerelease = $false
} | ConvertTo-Json -Depth 10

Write-Host "Creating GitHub Release..." -ForegroundColor Green

try {
    $response = Invoke-RestMethod -Uri "https://api.github.com/repos/TonyCutHair/GatherBuddyRebornCN/releases" -Method Post -Headers $headers -Body $releaseBody -ContentType "application/json"
    
    Write-Host "Release created successfully!" -ForegroundColor Green
    Write-Host "Release ID: $($response.id)" -ForegroundColor Cyan
    
    $zipPath = "E:\Github\GatherBuddyReborn_CN\GatherBuddyReborn_v7.4.0.3.zip"
    $uploadUrl = $response.upload_url -replace '\{\?name,label\}', "?name=GatherBuddyReborn_v7.4.0.3.zip"
    
    Write-Host "Uploading ZIP file..." -ForegroundColor Green
    
    $uploadHeaders = @{
        "Authorization" = "Bearer $tokenPlain"
        "Accept" = "application/vnd.github+json"
        "Content-Type" = "application/zip"
    }
    
    $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -InFile $zipPath
    
    Write-Host "ZIP uploaded successfully!" -ForegroundColor Green
    Write-Host "Download URL: $($uploadResponse.browser_download_url)" -ForegroundColor Cyan
    Write-Host "Release page: $($response.html_url)" -ForegroundColor Yellow
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}