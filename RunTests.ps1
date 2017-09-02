Param
(
    [parameter(Mandatory=$true)]
    [ValidateSet("Local", "Azure")]
    [String]
    $Location
)

if ($Location -eq 'local') {
    Write-Output 'Running tests against local server'

    $env:TEST_TCP_CONN_STR='server=tcp:localhost;user id=sa;password=452g34f23t4324t2g43t';
    $env:TEST_NP_CONN_STR='server=np:localhost;user id=sa;password=452g34f23t4324t2g43t';

    Push-Location $PSScriptRoot
    dotnet test src\tests\SqlClientSlimTests\SqlClientSlimTests.csproj
    dotnet test src\tests\FunctionalTests\System.Data.SqlClient.Tests.csproj
    Pop-Location
 } elseif ($Location -eq 'Azure') {
    Write-Output 'Running tests against SQL Azure'

    $env:TEST_TCP_CONN_STR='server=tcp:sqlclientslimtest.database.windows.net;user id=cloudsa;password=452g34f23t4324t2g43t!;'
    $env:TEST_NP_CONN_STR=''

    Push-Location $PSScriptRoot
    dotnet test src\tests\SqlClientSlimTests\SqlClientSlimTests.csproj --filter "connection!=np"
    dotnet test src\tests\FunctionalTests\System.Data.SqlClient.Tests.csproj --filter "connection!=np"
    Pop-Location
}