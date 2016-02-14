using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    public class SqlConnectionTests
    {
        /// <summary>
        /// Verifies that connecting using SQL auth works
        /// </summary>
        [Fact]
        public async Task SqlAuthConnectionTest()
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString))
            {
                await connection.OpenAsync();
                await RunBasicQueryAsync(connection);
            }
        }

        /// <summary>
        /// Verifies that connecting using integrated auth works
        /// </summary>
        [Fact]
        public async Task IntegratedAuthConnectionTest()
        {
            using (var connection = new SqlConnection(Utilities.IntegratedAuthConnectionString))
            {
                await Assert.ThrowsAsync<PlatformNotSupportedException>(connection.OpenAsync);
            }
        }

        /// <summary>
        /// Verifies that the same connect is returned every time if pooling is enabled
        /// </summary>
        [Fact]
        public async Task ConnectionPoolTest()
        {
            Guid connectionId = Guid.Empty;

            for (int i = 0; i < 10; i++)
            {
                using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString))
                {
                    await connection.OpenAsync();

                    // Check that the connection ids are the same every time
                    if (connectionId == Guid.Empty)
                    {
                        connectionId = connection.ClientConnectionId;
                    }
                    else
                    {
                        Assert.Equal(connectionId, connection.ClientConnectionId);
                    }

                    await RunBasicQueryAsync(connection);
                }
            }
        }

        /// <summary>
        /// Verifies that a different connection is returned every time if pooling is disabled
        /// </summary>
        [Fact]
        public async Task NonConnectionPoolTest()
        {
            const int iterations = 10;
            List<Guid> observedConnectionIds = new List<Guid>(iterations);

            for (int i = 0; i < iterations; i++)
            {
                using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString + "pooling=false"))
                {
                    await connection.OpenAsync();
                    Assert.DoesNotContain(connection.ClientConnectionId, observedConnectionIds);
                    observedConnectionIds.Add(connection.ClientConnectionId);
                    await RunBasicQueryAsync(connection);
                }
            }
        }

        /// <summary>
        /// Verifies that min pool size connection string keyword works
        /// </summary>
        [Fact]
        public async Task MinPoolSizeTest()
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString + "min pool size = 2"))
            {
                await connection.OpenAsync();
                // TODO: How to verify this?
            }
        }

        /// <summary>
        /// Verifies that if OpenAsync does not complete immediately, it returns a pending task until it can complete
        /// </summary>
        [Fact]
        public async Task PendingOpenAsyncTest()
        {
            const string connectionString = Utilities.SqlAuthConnectionString + "max pool size = 1";
            using (var firstConnection = new SqlConnection(connectionString))
            using (var secondConnection = new SqlConnection(connectionString))
            {
                // Open the first connection, should complete
                await firstConnection.OpenAsync();
                await RunBasicQueryAsync(firstConnection);

                // Open the second connection, should return without completing
                Task openTask = secondConnection.OpenAsync();
                Assert.False(openTask.Wait(TimeSpan.Zero), "Opening the second task should not have completed");
                await Assert.ThrowsAsync<InvalidOperationException>(() => RunBasicQueryAsync(secondConnection));

                // Close the first connection, and now the second connection should complete
                firstConnection.Close();
                Assert.True(openTask.Wait(TimeSpan.FromSeconds(1)), "Opening the second task should have completed");
                await RunBasicQueryAsync(secondConnection);
            }
        }

        /// <summary>
        /// Verifies that attempting to connect with bad credentials throws
        /// </summary>
        [Fact]
        public async Task BadCredentialsTest()
        {
            const string badCredentialConnectionString = Utilities.ServerOnlyConnectionString + "user id=notauser;password=badpassword;";
            using (var connection = new SqlConnection(badCredentialConnectionString))
            {
                await Assert.ThrowsAsync<SqlException>(connection.OpenAsync);
                Assert.Equal(ConnectionState.Closed, connection.State);
            }
        }

        /// <summary>
        /// Runs a basic query (SELECT 1) on the given connection
        /// </summary>
        private async Task RunBasicQueryAsync(SqlConnection connection)
        {
            using (var command = new SqlCommand("SELECT 1", connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
