@echo off
echo Starting Sandbox Setup... > C:\Users\WDAGUtilityAccount\Desktop\SetupLog.txt

powershell.exe -ExecutionPolicy Bypass -NoProfile -File "C:\SandboxShared\Startup.ps1" >> C:\Users\WDAGUtilityAccount\Desktop\SetupLog.txt 2>&1

echo Setup script finished. >> C:\Users\WDAGUtilityAccount\Desktop\SetupLog.txt
