# Tests

## What you need to run the tests
* A SQL Server instance.
* Set the `TEST_TCP_CONN_STR` and `TEST_NP_CONN_STR` environment variables with the TCP and Named Pipes connection string respectively using SQL Authentication (do NOT provide a data source).

You can also create a RunTests.cmd in the root of the repo (it will be ignored by Git) to set the environment and run the tests:

```
setlocal

set TEST_TCP_CONN_STR=server=tcp:localhost;user id=sa;password=InsertPasswordHere;
set TEST_NP_CONN_STR=server=np:localhost;user id=sa;password=InsertPasswordHere;

pushd %~dp0
dotnet test src\tests\SqlClientSlimTests\SqlClientSlimTests.csproj
dotnet test src\tests\FunctionalTests\System.Data.SqlClient.Tests.csproj
popd
```

### Running the tests against SQL Server in a container
Ensure that you have [Docker for Windows](https://docs.docker.com/docker-for-windows) installed and are [setup to use Windows
containers](https://docs.docker.com/docker-for-windows/#switch-between-windows-and-linux-containers).

To create the testing SQL Server container, run `tools\Start-TestServerContainer.ps1`

To run the tests: `RunTestsInDocker.ps1`

To stop and remove the testing SQL Server container, run `tools\Stop-TestServerContainer.ps1`

The tests are non-destructive, so it is best to leave the testing SQL Server container running as it takes a while to start.

Currently, Named Pipes are not supported in Windows containers, so these tests are skipped.

### Running the non-connectivity tests without a SQL Server
You can also run just the test that don't use a connection to SQL Server by filtering the "connection" trait to "none":

```
dotnet test src\tests\SqlClientSlimTests\SqlClientSlimTests.csproj --filter "connection=none"
dotnet test src\tests\FunctionalTests\System.Data.SqlClient.Tests.csproj --filter "connection=none"
```

### Testing against SQL Azure
Since SQL Azure doesn't support Named Pipes, add a filter to remove any "connection" trait of "np":

```
set TEST_TCP_CONN_STR=server=tcp:sqlclientslimtest.database.windows.net;user id=cloudsa;password=InsertPasswordHere;
set TEST_NP_CONN_STR=

dotnet test src\tests\SqlClientSlimTests\SqlClientSlimTests.csproj --filter "connection!=np"
dotnet test src\tests\FunctionalTests\System.Data.SqlClient.Tests.csproj --filter "connection!=np"
```

## Test provenance
FunctionTests and ManualTests are ported from the Official SqlClient Git repo: https://github.com/dotnet/corefx/tree/master/src/System.Data.SqlClient/tests