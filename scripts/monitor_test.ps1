# ホスト側でリアルタイム監視を行うためのスクリプト
# リポジトリルートで実行してください

while($true) { 
    Clear-Host
    Write-Host "--- Current test in Sandbox ---" -ForegroundColor Cyan
    Get-Content .\logs\current_test.txt -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1 
}
