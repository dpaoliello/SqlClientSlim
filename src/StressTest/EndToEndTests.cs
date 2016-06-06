using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace StressTest
{
    /// <summary>
    /// Set of end-to-end stress tests
    /// </summary>
    public static class EndToEndTests
    {
        private const string NonPooledFragment = "pooling=false;";
        private const string PooledFragment = "max pool size=1000;";
        private const string MarsFragment = "multipleactiveresultsets=true;";
        private const string SqlAuthFragment = "user id=sa;password=452g34f23t4324t2g43t;";

        private const string TcpFragment = "server=tcp:localhost;";
        private const string NpFragment = "server=np:localhost;";

        /// <summary>
        /// Set of tests with SQL Auth
        /// </summary>
        public static async Task SqlAuthConnectionTest(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(CreateBaseConnectionString());
            try
            {
                if (await TryOpenConnectionAsync(connection, cancellationToken))
                {
                    await CommandTests.RunAsync(connection.CreateCommand(), new ConnectionManager(connection, isMarsEnabled: false), cancellationToken);
                }
            }
            finally
            {
                // 95% change of closing (otherwise leaked)
                if (RandomHelper.NextBoolWithProbability(95))
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Set of tests with MARS enabled
        /// </summary>
        public static async Task MarsConnectionTest(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(CreateBaseConnectionString() + MarsFragment);
            try
            {
                if (await TryOpenConnectionAsync(connection, cancellationToken))
                {
                    ConnectionManager connectionManager = new ConnectionManager(connection, isMarsEnabled: true);
                    Task[] executionTasks = new Task[RandomHelper.Next(0, 8) * RandomHelper.Next(1, 8)];
                    for (int i = 0; i < executionTasks.Length; i++)
                    {
                        executionTasks[i] = Task.Run(() => CommandTests.RunAsync(connection.CreateCommand(), connectionManager, cancellationToken));
                    }
                    await Task.WhenAll(executionTasks);
                }
            }
            finally
            {
                // 95% change of closing (otherwise leaked)
                if (RandomHelper.NextBoolWithProbability(95))
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Creates a basic connection string
        /// </summary>
        /// <remarks>
        /// Chooses between:
        /// * TCP vs Named Pipes
        /// * Pooled or not pooled
        /// </remarks>
        private static string CreateBaseConnectionString()
        {
            // Equal change of TCP and Named Pipes
            string connectionString = RandomHelper.NextBoolWithProbability(80) ?
                TcpFragment :
                NpFragment;

            // 80% chance of pooling
            connectionString += RandomHelper.NextBoolWithProbability(80) ?
                PooledFragment :
                NonPooledFragment;

            return connectionString;
        }

        /// <summary>
        /// Attempts to open a connection handling expected failures
        /// </summary>
        private static async Task<bool> TryOpenConnectionAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                await connection.OpenAsync(cancellationToken);
                return true;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            { }

            // Fallthrough: Failure
            return false;
        }
    }
}
