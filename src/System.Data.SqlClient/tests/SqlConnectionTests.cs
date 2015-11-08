using System.Collections.Generic;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    public class SqlConnectionTests
    {
        private const string SqlAuthConnectionString = "server=localhost;user id=sa;password=452g34f23t4324t2g43t;";

        /// <summary>
        /// Verifies that we can connect to a local SQL Server
        /// </summary>
        [Fact]
        public void ConnectToLocalServerTest()
        {
            using (var connection = new SqlConnection(SqlAuthConnectionString))
            {
                connection.OpenAsync().Wait();
            }
        }

        /// <summary>
        /// Verifies that the same connect is returned every time if pooling is enabled
        /// </summary>
        [Fact]
        public void ConnectionPoolTest()
        {
            Guid connectionId = Guid.Empty;

            for (int i = 0; i < 10; i++)
            {
                using (var connection = new SqlConnection(SqlAuthConnectionString))
                {
                    connection.OpenAsync().Wait();

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
        public void NonConnectionPoolTest()
        {
            const int iterations = 10;
            List<Guid> observedConnectionIds = new List<Guid>(iterations);

            for (int i = 0; i < iterations; i++)
            {
                using (var connection = new SqlConnection(SqlAuthConnectionString + "pooling=false"))
                {
                    connection.OpenAsync().Wait();
                    Assert.DoesNotContain(connection.ClientConnectionId, observedConnectionIds);
                    observedConnectionIds.Add(connection.ClientConnectionId);
                }
            }
        }

        [Fact]
        public void MinPoolSizeTest()
        {
            using (var connection = new SqlConnection(SqlAuthConnectionString + "min pool size = 2"))
            {
                connection.OpenAsync().Wait();
                // TODO: How to verify this?
            }
        }
    }
}
