using Xunit;

namespace System.Data.SqlClient.Tests
{
    public class SqlConnectionTests
    {
        [Fact]
        public void CreateSqlConnectionTest()
        {
            var connection = new SqlConnection();
        }
    }
}
