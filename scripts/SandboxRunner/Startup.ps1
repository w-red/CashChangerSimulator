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

    Write-Host "[1/6] Fetching Registration Token from GitHub API..." -ForegroundColor Green
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

    Write-Host "[2/6] Installing Python (Silent)..." -ForegroundColor Green
    $PythonInstaller = "C:\python-installer.exe"
    Invoke-WebRequest -Uri "https://www.python.org/ftp/python/3.11.8/python-3.11.8-amd64.exe" -OutFile $PythonInstaller
    $proc = Start-Process -FilePath $PythonInstaller -ArgumentList "/quiet InstallAllUsers=1 PrependPath=1 Include_test=0" -PassThru -NoNewWindow
    $proc.WaitForExit()
    
    $env:PATH = [Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [Environment]::GetEnvironmentVariable("PATH", "User")

    Write-Host "[3/6] Installing .NET 10.0 SDK (Silent)..." -ForegroundColor Green
    $DotNetInstallScript = "C:\dotnet-install.ps1"
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $DotNetInstallScript
    & $DotNetInstallScript -Channel 10.0 -InstallDir "C:\dotnet" -NoPath
    $env:PATH += ";C:\dotnet"
    [Environment]::SetEnvironmentVariable("PATH", $env:PATH, "Machine")

    Write-Host "[4/6] Downloading GitHub Actions Runner..." -ForegroundColor Green
    $RunnerZip = "C:\actions-runner.zip"
    $RunnerDir = "C:\actions-runner"
    Invoke-WebRequest -Uri "https://github.com/actions/runner/releases/download/v2.322.0/actions-runner-win-x64-2.322.0.zip" -OutFile $RunnerZip
    Expand-Archive -Path $RunnerZip -DestinationPath $RunnerDir -Force
    Set-Location $RunnerDir

    Write-Host "[5/6] Configuring Ephemeral Runner..." -ForegroundColor Green
    .\config.cmd --url "https://github.com/$Owner/$Repo" --token $RunnerToken --ephemeral --name "Sandbox-UI-Runner" --labels "windows,ui-test" --unattended --replace

    Write-Host "[6/6] Listening for Jobs..." -ForegroundColor Green
    .\run.cmd

    Write-Host "Job completed. Press Enter to shutdown Sandbox..." -ForegroundColor Cyan
    Read-Host
    Stop-Computer -Force
} catch {
    Write-Host "[ERROR] An error occurred:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Read-Host "Press Enter to exit..."
}