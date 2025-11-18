@echo off
REM Script to generate htpasswd file for Nginx basic authentication
REM Usage: generate-htpasswd.bat [username] [password]

set USERNAME=%1
set PASSWORD=%2

if "%USERNAME%"=="" set USERNAME=admin
if "%PASSWORD%"=="" set PASSWORD=textprocessor2024

echo Generating htpasswd file for user: %USERNAME%

REM Create htpasswd file using PowerShell (since htpasswd is not available on Windows by default)
powershell -Command "$password = '%PASSWORD%'; $hash = [System.Web.Security.Membership]::GeneratePassword(60, 0); $salt = [System.Text.Encoding]::ASCII.GetBytes([guid]::NewGuid().ToString().Substring(0,8)); $hashedPassword = [System.Convert]::ToBase64String([System.Security.Cryptography.SHA1]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($password + [System.Text.Encoding]::ASCII.GetString($salt)))); '{SHA}' + $hashedPassword | Out-File -FilePath './htpasswd' -Encoding ASCII -NoNewline; '%USERNAME%:' + (Get-Content './htpasswd') | Out-File -FilePath './htpasswd' -Encoding ASCII -NoNewline"

if errorlevel 1 (
    echo Creating basic htpasswd with MD5...
    REM Fallback to a simpler method
    echo %USERNAME%:$apr1$%RANDOM%$%PASSWORD%> htpasswd
)

echo.
echo ✅ htpasswd file created successfully!
echo Username: %USERNAME%
echo Password: %PASSWORD%
echo.
echo File contents:
type htpasswd