# Runs tests against a SQL Server docker instance created by tools\StartDocker.ps1

$env:TEST_TCP_CONN_STR='server=tcp:SqlClientSlimTest;user id=sa;password=Th151z4nAwes0m3P455w0rd;'
$env:TEST_NP_CONN_STR='server=np:\\SqlClientSlimTest\pipe\MSSQL$SQLEXPRESS\sql\query;user id=sa;password=Th151z4nAwes0m3P455w0rd;'

# Currently file sharing (and, thus, remote named pipes) are not supported in Windows Containers:
# https://github.com/moby/moby/issues/26409

Push-Location $PSScriptRoot
dotnet test src\tests\SqlClientSlimTests\SqlClientSlimTests.csproj --filter "connection!=np"
dotnet test src\tests\FunctionalTests\System.Data.SqlClient.Tests.csproj --filter "connection!=np"
Pop-Location