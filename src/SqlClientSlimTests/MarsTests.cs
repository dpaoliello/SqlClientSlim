using System.Threading.Tasks;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    public sealed class MarsTests
    {
        /// <summary>
        /// Verifies that using MARS over TCP works correctly.
        /// </summary>
        [Fact]
        public async Task MarsOverTcpSingleCommand()
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString + Utilities.MarsFragment + Utilities.NonPooledFragment))
            using (var command = new SqlCommand("SELECT 1", connection))
            {
                await connection.OpenAsync();
                int value = (int)await command.ExecuteScalarAsync();
                Assert.Equal(1, value);
            }
        }

        /// <summary>
        /// Verifies that using MARS over TCP works correctly.
        /// </summary>
        [Fact]
        public async Task MarsOverNpSingleCommand()
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthNamesPipesConnectionString + Utilities.MarsFragment + Utilities.NonPooledFragment))
            using (var command = new SqlCommand("SELECT 1", connection))
            {
                await connection.OpenAsync();
                int value = (int)await command.ExecuteScalarAsync();
                Assert.Equal(1, value);
            }
        }

        /// <summary>
        /// Verifies that MARS connections can have multiple readers active at once.
        /// </summary>
        [Fact]
        public async Task MarsMultiCommand()
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString + Utilities.MarsFragment))
            using (var command1 = new SqlCommand("SELECT 1", connection))
            using (var command2 = new SqlCommand("SELECT 2", connection))
            using (var command3 = new SqlCommand("SELECT 3", connection))
            using (var command4 = new SqlCommand("SELECT 4", connection))
            {
                await connection.OpenAsync();
                using (var reader1 = await command1.ExecuteReaderAsync())
                using (var reader2 = await command2.ExecuteReaderAsync())
                using (var reader3 = await command3.ExecuteReaderAsync())
                using (var reader4 = await command4.ExecuteReaderAsync())
                {
                    // Read from the readers backwards
                    Assert.True(await reader4.ReadAsync());
                    Assert.True(await reader3.ReadAsync());
                    Assert.True(await reader2.ReadAsync());
                    Assert.True(await reader1.ReadAsync());

                    Assert.Equal(1, reader1.GetInt32(0));
                    Assert.Equal(2, reader2.GetInt32(0));
                    Assert.Equal(3, reader3.GetInt32(0));
                    Assert.Equal(4, reader4.GetInt32(0));
                }
            }
        }

        /// <summary>
        /// Verifies that closing a MARS connection will close any still-open readers.
        /// </summary>
        [Fact]
        public async Task MarsLeakedReader()
        {
            const string connectionString = Utilities.SqlAuthConnectionString + Utilities.MarsFragment + Utilities.MaxPoolOf1Fragment;

            SqlDataReader reader;
            Guid connectionId;
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("SELECT 1", connection))
            {
                await connection.OpenAsync();
                connectionId = connection.ClientConnectionId;

                // Leak reader on purpose
                reader = await command.ExecuteReaderAsync();
            }
            // Reader should now be closed
            await Assert.ThrowsAsync<InvalidOperationException>(reader.ReadAsync);

            // The connection should still be usable
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("SELECT 2", connection))
            {
                await connection.OpenAsync();
                Assert.Equal(connectionId, connection.ClientConnectionId);

                int value = (int)await command.ExecuteScalarAsync();
                Assert.Equal(2, value);
            }
        }
    }
}
