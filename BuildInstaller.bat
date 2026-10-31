@echo off
echo.
echo ----------------------------------------------
echo Tama's Palace POS - Build ^& Compile Installer
echo ----------------------------------------------
echo.

echo [1/2] Publishing Application (Release win-x64)...
dotnet publish -c Release -r win-x64 --self-contained true -o publish
if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Dotnet publish failed. Check the errors above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/2] Compiling Installer (Inno Setup)...
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
    "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer.iss
) else if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    "C:\Program Files\Inno Setup 6\ISCC.exe" installer.iss
) else (
    echo.
    echo ERROR: Inno Setup (ISCC.exe) not found in standard paths.
    echo Please ensure Inno Setup is installed, or compile installer.iss manually.
    pause
    exit /b 1
)

echo.
echo ----------------------------------------------
echo SUCCESS! Your installer is in installer_output/
echo ----------------------------------------------
echo.
pause
