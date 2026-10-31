Write-Host "`n----------------------------------------------" -ForegroundColor Cyan
Write-Host "Tama's Palace POS - Build & Compile Installer" -ForegroundColor Green
Write-Host "----------------------------------------------`n" -ForegroundColor Cyan

Write-Host "[1/2] Publishing Application (Release win-x64)..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -o publish

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nERROR: Dotnet publish failed." -ForegroundColor Red
    pause
    exit $LASTEXITCODE
}

Write-Host "`n[2/2] Compiling Installer (Inno Setup)..." -ForegroundColor Yellow
$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $iscc) {
    $commonPaths = @(
        "C:\Users\SireTheo\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
    )
    foreach ($path in $commonPaths) {
        if (Test-Path $path) { $iscc = $path; break }
    }
}

if ($iscc) {
    Write-Host "Using: $iscc"
    & "$iscc" installer.iss
} else {
    Write-Host "`nERROR: Inno Setup (ISCC.exe) not found." -ForegroundColor Red
    Write-Host "Please install Inno Setup 6 or run manually."
}

Write-Host "`n----------------------------------------------" -ForegroundColor Cyan
if ($LASTEXITCODE -eq 0 -and $iscc) {
    Write-Host "SUCCESS! Your installer is in installer_output/" -ForegroundColor Green
}
Write-Host "----------------------------------------------`n" -ForegroundColor Cyan
pause
