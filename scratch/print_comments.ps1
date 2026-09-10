$headers = @{ 'User-Agent' = 'Antigravity' }
$comments = Invoke-RestMethod -Uri 'https://api.github.com/repos/mbulava-org/GameServer.GUI/pulls/25/comments' -Headers $headers
$i = 1
foreach ($c in $comments) {
    Write-Host "========== COMMENT $i =========="
    Write-Host "ID: $($c.id)"
    Write-Host "Path: $($c.path)"
    Write-Host "Line: $($c.line)"
    Write-Host "Body: $($c.body)"
    Write-Host "--------------------------------"
    $i++
}
