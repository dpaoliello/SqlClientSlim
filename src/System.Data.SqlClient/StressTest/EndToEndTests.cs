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
        private const string MarsFragment = "multipleactiveresultsets=true";

        private const string SqlAuthConnectionString = "server=localhost;user id=sa;password=452g34f23t4324t2g43t;";
        private const string SqlAuthNonPooledConnectionString = SqlAuthConnectionString + NonPooledFragment;

        /// <summary>
        /// Set of tests with SQL Auth
        /// </summary>
        public static async Task SqlAuthConnectionTest(CancellationToken cancellationToken)
        {
            // 80% chance of pooling
            string connectionString = RandomHelper.NextBoolWithProbability(80) ?
                SqlAuthConnectionString :
                SqlAuthNonPooledConnectionString;

            var connection = new SqlConnection(connectionString);
            try
            {
                if (await TryOpenConnectionAsync(connection, cancellationToken))
                {
                    await CommandTests.RunAsync(connection.CreateCommand(), cancellationToken);
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
            var connection = new SqlConnection(SqlAuthConnectionString + MarsFragment);
            try
            {
                if (await TryOpenConnectionAsync(connection, cancellationToken))
                {
                    Task[] executionTasks = new Task[RandomHelper.Next(0, 8) * RandomHelper.Next(1, 8)];
                    for (int i = 0; i < executionTasks.Length; i++)
                    {
                        executionTasks[i] = CommandTests.RunAsync(connection.CreateCommand(), cancellationToken);
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
