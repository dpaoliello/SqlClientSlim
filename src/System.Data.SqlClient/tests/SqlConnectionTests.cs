using Xunit;

namespace System.Data.SqlClient.Tests
{
    public class SqlConnectionTests
    {
        [Fact]
        public void ConnectToLocalDbTest()
        {
            using (var connection = new SqlConnection("server=localhost;user id=sa;password=452g34f23t4324t2g43t"))
            {
                connection.OpenAsync().Wait();
            }
        }
    }
}
