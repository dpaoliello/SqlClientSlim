using System.Threading.Tasks;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    /// <summary>
    /// Tests for SqlCommand.
    /// </summary>
    public sealed class SqlCommandTests
    {
        /// <summary>
        /// Verifies that ExecuteScalar returns the first value from the first result, even when recycling the SqlCommand object.
        /// </summary>
        [Fact]
        public async Task ExecuteScalarTest()
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1";
                    Assert.Equal(1, (int)await command.ExecuteScalarAsync());

                    command.CommandText = "SELECT 'Hello, World!'";
                    Assert.Equal("Hello, World!", (string)await command.ExecuteScalarAsync());

                    command.CommandText = "SELECT @p1";
                    command.Parameters.Add(new SqlParameter("p1", "Hello"));
                    Assert.Equal("Hello", (string)await command.ExecuteScalarAsync());

                    command.CommandText = "SELECT @p1, @p2";
                    command.Parameters.Add(new SqlParameter("p2", "World!"));
                    Assert.Equal("Hello", (string)await command.ExecuteScalarAsync());
                }
            }
        }
    }
}
