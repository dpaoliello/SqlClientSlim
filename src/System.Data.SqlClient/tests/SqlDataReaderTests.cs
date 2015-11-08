using System.Threading.Tasks;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    public sealed class SqlDataReaderTests
    {
        /// <summary>
        /// Verifies that parameters can be sent to the server and then retrieved
        /// </summary>
        [Fact]
        public async Task EndToEndTest()
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString))
            using (var command = new SqlCommand("SELECT @number, @string", connection))
            {
                command.Parameters.Add(new SqlParameter("number", 1));
                command.Parameters.Add(new SqlParameter("string", "Hello, World!"));

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    Assert.Equal(true, await reader.ReadAsync());

                    Assert.Equal(1, reader.GetFieldValue<int>(0));
                    Assert.Equal("Hello, World!", await reader.GetFieldValueAsync<string>(1));
                }
            }
        }
    }
}
