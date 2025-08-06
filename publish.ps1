# This is the final, simplified publishing script.

# 1. Define directory paths
$ReleaseDir = ".\release"
$HubDir = "$ReleaseDir\CentralHub"
$GarnetDir = "$ReleaseDir\Garnet"
$ProjectFile = ".\src5\central-hub-aspnet\central-hub-aspnet.csproj"

# 2. Clean and create the release directory
Write-Host "Cleaning up the old '$ReleaseDir' directory..."
if (Test-Path -Path $ReleaseDir) {
    Remove-Item -Recurse -Force $ReleaseDir
}
Write-Host "Creating a new release directory structure..."
New-Item -ItemType Directory -Force -Path $HubDir
New-Item -ItemType Directory -Force -Path $GarnetDir

# 3. Publish the application
Write-Host "Publishing Central Hub to '$HubDir'..."
dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true -o $HubDir

# 4. Manually create the start-all.bat file (already done)
Write-Host "The 'start-all.bat' file has been created separately."

# 5. Print final instructions
Write-Host "--------------------------------------------------" -ForegroundColor Green
Write-Host "Publish complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Copy your Garnet server files into the '$GarnetDir' directory."
Write-Host "2. The entire '$ReleaseDir' folder is now your deployment package."
Write-Host "3. Run '$ReleaseDir\start-all.bat' to start all services."
Write-Host "--------------------------------------------------" -ForegroundColor Green