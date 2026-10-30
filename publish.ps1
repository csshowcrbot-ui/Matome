# Matome IIS Production Publish Script
$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building Matome Release for Production IIS...            " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$projectDir = $PSScriptRoot
$publishDir = Join-Path $projectDir "publish"
$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"

if (-not (Test-Path $msbuild)) {
    throw "MSBuild not found at: $msbuild"
}

# 1. Rebuild in Release mode
Write-Host "[1/3] Compiling Release assembly..." -ForegroundColor Yellow
& $msbuild "$projectDir\Matome.csproj" /t:Rebuild /p:Configuration=Release /v:m /nologo
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild build failed."
}

# 2. Prepare publish directory
Write-Host "[2/3] Preparing publish directory: $publishDir" -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publishDir "bin") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publishDir "Content") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publishDir "Admin") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publishDir "Approval") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publishDir "Services") | Out-Null

# 3. Copy files
Write-Host "[3/3] Copying application files..." -ForegroundColor Yellow

# bin
Copy-Item -Path "$projectDir\bin\Matome.dll" -Destination "$publishDir\bin\" -Force
if (Test-Path "$projectDir\bin\Matome.pdb") {
    Copy-Item -Path "$projectDir\bin\Matome.pdb" -Destination "$publishDir\bin\" -Force
}

# Root pages
Copy-Item -Path "$projectDir\Default.aspx" -Destination "$publishDir\" -Force
Copy-Item -Path "$projectDir\EquipmentBooking.aspx" -Destination "$publishDir\" -Force
Copy-Item -Path "$projectDir\Login.aspx" -Destination "$publishDir\" -Force
Copy-Item -Path "$projectDir\MyBookings.aspx" -Destination "$publishDir\" -Force
Copy-Item -Path "$projectDir\Site.Master" -Destination "$publishDir\" -Force

# Subfolders
Copy-Item -Path "$projectDir\Content\site.css" -Destination "$publishDir\Content\" -Force
Copy-Item -Path "$projectDir\Admin\RoomManage.aspx" -Destination "$publishDir\Admin\" -Force
Copy-Item -Path "$projectDir\Admin\EquipmentManage.aspx" -Destination "$publishDir\Admin\" -Force
Copy-Item -Path "$projectDir\Admin\UserManage.aspx" -Destination "$publishDir\Admin\" -Force
Copy-Item -Path "$projectDir\Approval\PendingList.aspx" -Destination "$publishDir\Approval\" -Force
Copy-Item -Path "$projectDir\Services\BookingService.ashx" -Destination "$publishDir\Services\" -Force

# Web.config with debug="false"
$webConfigContent = Get-Content "$projectDir\Web.config" -Raw
$prodWebConfig = $webConfigContent -replace 'compilation debug="true"', 'compilation debug="false"'
Set-Content -Path "$publishDir\Web.config" -Value $prodWebConfig -Encoding UTF8

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " [SUCCESS] Matome publish completed!" -ForegroundColor Green
Write-Host " Output Directory: $publishDir" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
