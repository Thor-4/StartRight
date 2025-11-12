# PowerShell script to create installer for Start Right
param(
    [string]$OutputPath = ".\Publish",
    [string]$InstallerPath = ".\Installer"
)

# Create directories
New-Item -ItemType Directory -Force -Path $OutputPath
New-Item -ItemType Directory -Force -Path $InstallerPath

Write-Host "Publishing Start Right application..." -ForegroundColor Green

# Publish the application as self-contained
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Application published successfully!" -ForegroundColor Green

# Create a simple installer batch file
$installerContent = @"
@echo off
setlocal enabledelayedexpansion

echo Start Right Installer
echo ====================
echo.

set "TARGET_DIR=%PROGRAMFILES%\Start Right"
set "APP_EXE=StartRight.exe"

echo Installing Start Right to !TARGET_DIR!...

REM Create target directory
if not exist "!TARGET_DIR!" (
    mkdir "!TARGET_DIR!"
)

REM Copy files
echo Copying files...
copy "%~dp0*" "!TARGET_DIR!\" >nul

REM Create desktop shortcut
echo Creating desktop shortcut...
powershell -Command "`$WshShell = New-Object -comObject WScript.Shell; `$Shortcut = `$WshShell.CreateShortcut(`$Env:USERPROFILE + '\Desktop\Start Right.lnk'); `$Shortcut.TargetPath = '!TARGET_DIR!\!APP_EXE!'; `$Shortcut.WorkingDirectory = '!TARGET_DIR!'; `$Shortcut.Save()"

REM Create start menu shortcut
echo Creating start menu shortcut...
powershell -Command "`$WshShell = New-Object -comObject WScript.Shell; `$Shortcut = `$WshShell.CreateShortcut(`$Env:APPDATA + '\Microsoft\Windows\Start Menu\Programs\Start Right.lnk'); `$Shortcut.TargetPath = '!TARGET_DIR!\!APP_EXE!'; `$Shortcut.WorkingDirectory = '!TARGET_DIR!'; `$Shortcut.Save()"

echo.
echo Installation completed successfully!
echo.
echo Start Right has been installed to: !TARGET_DIR!
echo Shortcuts have been created on your desktop and start menu.
echo.
echo You can now run Start Right from your desktop or start menu.
echo.
pause
"@

# Save the installer batch file
$installerContent | Out-File -FilePath "$InstallerPath\Install-StartRight.bat" -Encoding ASCII

# Copy published files to installer directory
Copy-Item "$OutputPath\*" "$InstallerPath\" -Recurse -Force

Write-Host "Installer created in: $InstallerPath" -ForegroundColor Green
Write-Host "Files in installer directory:" -ForegroundColor Yellow
Get-ChildItem $InstallerPath

Write-Host "`nTo distribute the application:" -ForegroundColor Cyan
Write-Host "1. Give users the entire 'Installer' folder" -ForegroundColor White
Write-Host "2. Users should run 'Install-StartRight.bat' as Administrator" -ForegroundColor White
Write-Host "3. The application will be installed to Program Files and shortcuts created" -ForegroundColor White