# This is the final, fully automated publishing script.

# 1. Define paths
$RootDir = Get-Location
$ReleaseDir = "$RootDir\release"
$HubDir = "$ReleaseDir\CentralHub"
$GarnetSourceDir = "$RootDir\Garnet"
$GarnetTargetDir = "$ReleaseDir\Garnet"
$ProjectFile = "$RootDir\src5\central-hub-aspnet\central-hub-aspnet.csproj"

# 2. Clean and create the entire release directory structure
Write-Host "Cleaning up the old '$ReleaseDir' directory..."
if (Test-Path -Path $ReleaseDir) {
    Remove-Item -Recurse -Force $ReleaseDir
}
Write-Host "Creating a new release directory structure..."
New-Item -ItemType Directory -Force -Path $HubDir
New-Item -ItemType Directory -Force -Path $GarnetTargetDir

# 3. Publish the application directly into the target directory
Write-Host "Publishing Central Hub to '$HubDir'..."
dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true -o $HubDir

# 4. Copy Garnet files from the project root to the release directory
Write-Host "Copying Garnet files from '$GarnetSourceDir' to '$GarnetTargetDir'..."
if (Test-Path -Path $GarnetSourceDir) {
    Copy-Item -Path "$GarnetSourceDir\*" -Destination $GarnetTargetDir -Recurse -Force
} else {
    Write-Host "Warning: Garnet source directory not found at '$GarnetSourceDir'. Please place your Garnet files there." -ForegroundColor Yellow
}

# 5. Create the clean, compatible startup script inside the release directory
Write-Host "Creating the 'start-all.bat' startup script..."
$StartScriptContent = @'
@echo off

echo Starting Garnet Server...
start "Garnet Server" /D ".\Garnet" GarnetServer.exe

echo Starting Central Hub...
start "Central Hub" /D ".\CentralHub" central-hub-aspnet.exe
'@
$StartScriptContent | Out-File -FilePath "$ReleaseDir\start-all.bat" -Encoding oem

# 6. Print final instructions
Write-Host "--------------------------------------------------" -ForegroundColor Green
Write-Host "Publish complete!" -ForegroundColor Green
Write-Host ""
Write-Host "The entire '$ReleaseDir' folder is now your complete, self-contained deployment package."
Write-Host "You can run '$ReleaseDir\start-all.bat' to start all services."
Write-Host "--------------------------------------------------" -ForegroundColor Green