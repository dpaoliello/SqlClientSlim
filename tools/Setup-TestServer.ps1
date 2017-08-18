# Enables the Named Pipes provider and installs the necessary databases

Import-Module sqlps
$wmi = new-object ('Microsoft.SqlServer.Management.Smo.Wmi.ManagedComputer')

$uri = "ManagedComputer[@Name='" + (Get-Item env:\computername).Value + "']/ServerInstance[@Name='SQLEXPRESS']/ServerProtocol[@Name='Np']"
$np = $wmi.GetSmoObject($uri)  
$np.IsEnabled = $true  
$np.Alter()

Invoke-SqlCmd -InputFile 'C:\TestDBs\instnwnd.sql' -ServerInstance '.\SQLEXPRESS' -ErrorAction Stop
Invoke-SqlCmd -InputFile 'C:\TestDBs\instpubs.sql' -ServerInstance '.\SQLEXPRESS' -ErrorAction Stop

$sqlService = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction Stop
$sqlService | Stop-Service -ErrorAction Stop