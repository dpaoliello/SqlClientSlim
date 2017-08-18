# Imports the Powershell Docker module

# Ensure the temp directory exists in the repo
$tempDir = "$PSScriptRoot\..\Temp"
if (!(Test-Path $tempDir)) {
    New-Item $tempDir -ItemType Directory -ErrorAction Stop
}

# Load the Docker Powershell module
$powershellDockerDir = "$tempDir\Docker"
if (!(Test-Path $powershellDockerDir)) {
    Write-Output 'Could not find Docker Powershell module'

    $powershellDockerZip = "$tempDir\Docker.0.1.0.zip"
    if (!(Test-Path $powershellDockerZip)) {
        Write-Output 'Downloading Docker Powershell module'
        Invoke-WebRequest -Uri 'https://github.com/Microsoft/Docker-PowerShell/releases/download/v0.1.0/Docker.0.1.0.zip' -OutFile $powershellDockerZip -ErrorAction Stop
    }
    Write-Output 'Extracting Docker Powershell module'
    Add-Type -Assembly 'System.IO.Compression.FileSystem' -ErrorAction Stop
    [System.IO.Compression.ZipFile]::ExtractToDirectory($powershellDockerZip, $powershellDockerDir)
    Write-Output 'Done'
}
Import-Module -Name $powershellDockerDir -ErrorAction Stop