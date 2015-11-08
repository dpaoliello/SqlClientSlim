using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    public class SqlConnectionTests
    {
        private const string SqlAuthConnectionString = "server=localhost;user id=sa;password=452g34f23t4324t2g43t;";
        private const string IntegratedAuthConnectionString = "server=localhost;integrated security=true;";

        /// <summary>
        /// Verifies that connecting using SQL auth works
        /// </summary>
        [Fact]
        public async Task SqlAuthConnectionTest()
        {
            using (var connection = new SqlConnection(SqlAuthConnectionString))
            {
                await connection.OpenAsync();
            }
        }

        /// <summary>
        /// Verifies that connecting using integrated auth works
        /// </summary>
        [Fact]
        public async Task IntegratedAuthConnectionTest()
        {
            using (var connection = new SqlConnection(IntegratedAuthConnectionString))
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
                using (var connection = new SqlConnection(SqlAuthConnectionString))
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
                using (var connection = new SqlConnection(SqlAuthConnectionString + "pooling=false"))
                {
                    await connection.OpenAsync();
                    Assert.DoesNotContain(connection.ClientConnectionId, observedConnectionIds);
                    observedConnectionIds.Add(connection.ClientConnectionId);
                }
            }
        }

        [Fact]
        public async Task MinPoolSizeTest()
        {
            using (var connection = new SqlConnection(SqlAuthConnectionString + "min pool size = 2"))
            {
                await connection.OpenAsync();
                // TODO: How to verify this?
            }
        }
    }
}
