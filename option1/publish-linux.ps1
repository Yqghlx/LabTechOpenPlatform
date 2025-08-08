# This script publishes a self-contained version for Linux and prepares the deployment package.

# 1. Define paths
$RootDir = Get-Location
$ReleaseDir = "$RootDir\release-linux" # Use a separate directory for the Linux release
$HubDir = "$ReleaseDir\CentralHub"
$GarnetSourceDir = "$RootDir\Garnet_linux"
$GarnetTargetDir = "$ReleaseDir\Garnet"
$ProjectFile = "$RootDir\src\central-hub-aspnet\central-hub-aspnet.csproj"

# 2. Clean and create the entire release directory structure
Write-Host "Cleaning up the old '$ReleaseDir' directory..."
if (Test-Path -Path $ReleaseDir) {
    Remove-Item -Recurse -Force $ReleaseDir
}
Write-Host "Creating a new Linux release directory structure..."
New-Item -ItemType Directory -Force -Path $HubDir
New-Item -ItemType Directory -Force -Path $GarnetTargetDir

# 3. Publish the application for Linux
Write-Host "Publishing Central Hub for Linux (linux-x64)..."
# The key here is the '-r linux-x64' argument
dotnet publish $ProjectFile -c Release -r linux-x64 --self-contained true -o $HubDir

# 4. Copy the Linux version of Garnet
Write-Host "Copying Linux version of Garnet from '$GarnetSourceDir'..."
if (Test-Path -Path $GarnetSourceDir) {
    Copy-Item -Path "$GarnetSourceDir\*" -Destination $GarnetTargetDir -Recurse -Force
} else {
    Write-Host "Warning: Garnet source directory for Linux not found at '$GarnetSourceDir'." -ForegroundColor Yellow
}

# 5. Create the Linux startup script (start-all.sh)
Write-Host "Creating the 'start-all.sh' startup script..."
# Use a verbatim string and UTF-8 encoding for Linux compatibility
$StartScriptContent = @'
#!/bin/bash
# This script starts both the Garnet server and the Central Hub application.

# Get the directory where the script is located
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"

echo "Starting Garnet Server in the background..."
cd "$DIR/Garnet"
./GarnetServer &

# Go back to the root of the release directory
cd "$DIR"

echo "Starting Central Hub..."
cd "$DIR/CentralHub"
./central-hub-aspnet
'@
$StartScriptContent | Out-File -FilePath "$ReleaseDir\start-all.sh" -Encoding utf8

# 6. Print final instructions
Write-Host "--------------------------------------------------" -ForegroundColor Green
Write-Host "Linux publish complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps for deployment on your Linux server:"
Write-Host "1. Copy the entire '$ReleaseDir' folder to your Linux server."
Write-Host "2. Open a terminal on the Linux server and navigate into the folder."
Write-Host "3. Make the script executable by running this command: chmod +x start-all.sh"
Write-Host "4. Run the script to start all services: ./start-all.sh"
Write-Host "--------------------------------------------------" -ForegroundColor Green
