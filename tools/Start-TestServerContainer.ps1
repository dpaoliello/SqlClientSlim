# Starts a SQL Server container using docker that can be used to test SqlClientSlim.

Invoke-Expression "$PSScriptRoot\Import-DockerPowershell.ps1"
$tempDir = "$PSScriptRoot\..\Temp"

# Check for existing SQL docker containers
if ((Get-Container -Name 'SqlClientSlimTest' -ErrorAction Stop).Count -ne 0) {
    Write-Error 'SQL Docker Container already started. Run tools\Stop-TestServerContainer.ps1 to stop.' -ErrorAction Stop
}

# Get the docker container image
if ((Get-ContainerImage microsoft/mssql-server-windows-express -ErrorAction Stop).Count -eq 0) {
    Write-Output 'Could not find SQL Server Docker image'
    Request-ContainerImage microsoft/mssql-server-windows-express -ErrorAction Stop
}

# Create a new image with the files
# NOTE: New-ContainerImage is currently broken
Write-Output "Creating docker image"
$dockerFile = "$PSScriptRoot\TestServer.dockerfile"
docker build -f $dockerFile $PSScriptRoot -t sqlclientslim/test-server

# Create and start the container
Write-Output 'Starting container'
$config = New-Object 'Docker.DotNet.Models.Config'
$config.Env = New-Object 'System.Collections.Generic.List[string]'
$config.Env.Add('ACCEPT_EULA=Y')
$config.Env.Add('sa_password=Th151z4nAwes0m3P455w0rd')
$config.Hostname = 'SqlClientSlimTest'
$config.ExposedPorts = New-Object 'System.Collections.Generic.Dictionary[string,object]'
$config.ExposedPorts.Add('139',$null)
$config.ExposedPorts.Add('445',$null)
$config.ExposedPorts.Add('1433',$null)
$config.Image = 'sqlclientslim/test-server'

$container = New-Container -Id 'sqlclientslim/test-server' -Name 'SqlClientSlimTest' -Configuration $config -ErrorAction Stop
Start-Container $container -ErrorAction Stop

Write-Output 'All done!'