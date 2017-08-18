# Cleans up a SQL Server container using docker that can be used to test SqlClientSlim.

Invoke-Expression "$PSScriptRoot\Import-DockerPowershell.ps1"

$container = @(Get-Container) | Where-Object {$_.Names -eq '/SqlClientSlimTest'}
$container | Stop-Container
$container | Remove-Container