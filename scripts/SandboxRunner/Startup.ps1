Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Windows Sandbox for GitHub Actions Runner (UI Test)    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$TokenFile = "C:\SandboxShared\runner_token.txt"
if (-not (Test-Path $TokenFile)) {
    Write-Host "[ERROR] runner_token.txt not found!" -ForegroundColor Red
    Write-Host "Please save your GitHub Personal Access Token (PAT) in scripts\SandboxRunner\runner_token.txt" -ForegroundColor Yellow
    Write-Host "Press Enter to exit..."
    Read-Host
    exit
}

$PAT = (Get-Content $TokenFile).Trim()
if ([string]::IsNullOrWhiteSpace($PAT)) {
    Write-Host "[ERROR] PAT is empty in runner_token.txt" -ForegroundColor Red
    Read-Host "Press Enter to exit..."
    exit
}

# リポジトリ情報の定義 (ユーザー名/リポジトリ名)
$Owner = "w-red"
$Repo = "CashChangerSimulator"

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    Write-Host "[1/7] Fetching Registration Token from GitHub API..." -ForegroundColor Green
    $Uri = "https://api.github.com/repos/$Owner/$Repo/actions/runners/registration-token"
    $Headers = @{
        "Authorization" = "token $PAT"
        "Accept"        = "application/vnd.github.v3+json"
    }
    
    $Response = Invoke-RestMethod -Uri $Uri -Method Post -Headers $Headers
    $RunnerToken = $Response.token
    
    if ([string]::IsNullOrWhiteSpace($RunnerToken)) {
        throw "Failed to retrieve runner registration token."
    }
    Write-Host "Successfully fetched new registration token." -ForegroundColor Cyan

    Write-Host "[2/7] Getting Python (Portable ZIP)..." -ForegroundColor Green
    $PythonZip = "C:\python-portable.zip"
    $PythonDir = "C:\python"
    # 埋め込み用(embeddable)パッケージをダウンロード（超軽量・インストール不要）
    Invoke-WebRequest -Uri "https://www.python.org/ftp/python/3.11.8/python-3.11.8-embed-amd64.zip" -OutFile $PythonZip
    Expand-Archive -Path $PythonZip -DestinationPath $PythonDir -Force
    $env:PATH += ";$PythonDir"
    [Environment]::SetEnvironmentVariable("PATH", $env:PATH, "Machine")

    Write-Host "[3/7] Installing Git (MinGit Portable)..." -ForegroundColor Green
    $GitZip = "C:\mingit.zip"
    $GitDir = "C:\git"
    # Download MinGit (Smallest footprint for CI)
    Invoke-WebRequest -Uri "https://github.com/git-for-windows/git/releases/download/v2.44.0.windows.1/MinGit-2.44.0-64-bit.zip" -OutFile $GitZip
    Expand-Archive -Path $GitZip -DestinationPath $GitDir -Force
    $env:PATH += ";$GitDir\cmd"
    [Environment]::SetEnvironmentVariable("PATH", $env:PATH, "Machine")

    Write-Host "[4/7] Installing .NET 10.0 SDK (Silent)..." -ForegroundColor Green
    $DotNetInstallScript = "C:\dotnet-install.ps1"
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $DotNetInstallScript
    & $DotNetInstallScript -Channel 10.0 -InstallDir "C:\dotnet" -NoPath
    $env:PATH += ";C:\dotnet"
    [Environment]::SetEnvironmentVariable("PATH", $env:PATH, "Machine")

    Write-Host "[5/7] Downloading GitHub Actions Runner..." -ForegroundColor Green
    $RunnerZip = "C:\actions-runner.zip"
    $RunnerDir = "C:\actions-runner"
    Invoke-WebRequest -Uri "https://github.com/actions/runner/releases/download/v2.322.0/actions-runner-win-x64-2.322.0.zip" -OutFile $RunnerZip
    Expand-Archive -Path $RunnerZip -DestinationPath $RunnerDir -Force
    Set-Location $RunnerDir

    Write-Host "[6/7] Configuring Runner (Continuous Mode)..." -ForegroundColor Green
    .\config.cmd --url "https://github.com/$Owner/$Repo" --token $RunnerToken --name "Sandbox-UI-Runner" --labels "windows,ui-test" --unattended --replace

    Write-Host "[7/7] Listening for Jobs and running cleanup loop..." -ForegroundColor Green
    
    # run.cmd をバックグラウンド（別プロセス）で起動
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c .\run.cmd" -NoNewWindow
    
    # メインループ：ジョブが走る裏側で、定期的にゾンビプロセスを監視・掃除する（簡易版）
    # ※run.cmd自体は待機し続けるため、このループでサンドボックスを維持します
    Write-Host "Runner is active. Press Ctrl+C to stop." -ForegroundColor Cyan
    while ($true) {
        # テスト実行中でないタイミング（プロセスが古く、CPUを使っていない等）を判定するのは難しいため、
        # ここでは連続稼働環境の維持のみを行い、プロセスのクリーンアップはテストコード側（CashChangerTestApp.cs）に任せます。
        # 万が一ハングした場合は、ホストからサンドボックスを再起動する運用を推奨します。
        Start-Sleep -Seconds 60
    }
} catch {
    Write-Host "[ERROR] An error occurred:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Read-Host "Press Enter to exit..."
}