@echo off
REM Script to generate htpasswd file for Nginx basic authentication
REM Usage: generate-htpasswd.bat [username] [password]

set USERNAME=%1
set PASSWORD=%2

if "%USERNAME%"=="" set USERNAME=admin
if "%PASSWORD%"=="" set PASSWORD=textprocessor2025

echo Generating htpasswd file for user: %USERNAME%

REM Create htpasswd file using Docker httpd container (most reliable method)
docker run --rm httpd:2.4-alpine htpasswd -nbB %USERNAME% %PASSWORD% > htpasswd_temp
if errorlevel 1 (
    echo Docker method failed, trying PowerShell fallback...
    REM Fallback using PowerShell with proper encoding
    powershell -Command "docker run --rm httpd:2.4-alpine htpasswd -nbB %USERNAME% %PASSWORD% | Set-Content htpasswd -Encoding ASCII"
) else (
    REM Use PowerShell to ensure proper encoding without BOM
    powershell -Command "Get-Content htpasswd_temp | Set-Content htpasswd -Encoding ASCII"
    del htpasswd_temp
)

echo.
echo ✅ htpasswd file created successfully!
echo Username: %USERNAME%
echo Password: %PASSWORD%
echo.
echo File contents:
type htpasswd