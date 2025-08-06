# This is the final, robust, all-in-one publishing script.

# 1. Define paths
$ReleaseDir = ".\release"
$HubDir = "$ReleaseDir\CentralHub"
$GarnetDir = "$ReleaseDir\Garnet"
$ProjectFile = ".\src5\central-hub-aspnet\central-hub-aspnet.csproj"

# 2. Clean and create the entire release directory structure
Write-Host "Cleaning up the old '$ReleaseDir' directory..."
if (Test-Path -Path $ReleaseDir) {
    Remove-Item -Recurse -Force $ReleaseDir
}
Write-Host "Creating a new release directory structure..."
New-Item -ItemType Directory -Force -Path $HubDir
New-Item -ItemType Directory -Force -Path $GarnetDir

# 3. Publish the application directly into the target directory
Write-Host "Publishing Central Hub to '$HubDir'..."
dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true -o $HubDir

# 4. Create the clean, compatible startup script inside the release directory
Write-Host "Creating the 'start-all.bat' startup script..."
# Use a verbatim string and OEM encoding for maximum compatibility with cmd.exe
$StartScriptContent = @'
@echo off

echo Starting Garnet Server...
start "Garnet Server" /D ".\Garnet" GarnetServer.exe

echo Starting Central Hub...
start "Central Hub" /D ".\CentralHub" central-hub-aspnet.exe
'@
$StartScriptContent | Out-File -FilePath "$ReleaseDir\start-all.bat" -Encoding oem

# 5. Print final instructions
Write-Host "--------------------------------------------------" -ForegroundColor Green
Write-Host "Publish complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Copy your Garnet server files into the '$GarnetDir' directory."
Write-Host "2. The entire '$ReleaseDir' folder is now your complete deployment package."
Write-Host "3. Run '$ReleaseDir\start-all.bat' to start all services."
Write-Host "--------------------------------------------------" -ForegroundColor Green
