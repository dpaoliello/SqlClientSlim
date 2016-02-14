namespace System.Data.SqlClient.Tests
{
    internal static class Utilities
    {
        public const string ServerOnlyConnectionString = "server=localhost;";
        public const string SqlAuthConnectionString = ServerOnlyConnectionString + "user id=sa;password=452g34f23t4324t2g43t;";
        public const string IntegratedAuthConnectionString = ServerOnlyConnectionString + "integrated security=true;";
    }
}
