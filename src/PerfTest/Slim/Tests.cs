using System.Data;
using System.Data.SqlClient;

namespace PerfTest
{
    internal static class SqlClientTests
    {
        private const string SqlAuthFragment = "user id=sa;password=452g34f23t4324t2g43t;";
        private const string SqlAuthTcpConnectionString = "server=tcp:localhost;" + SqlAuthFragment;
        private const string SqlAuthNpConnectionString = "server=np:localhost;" + SqlAuthFragment;

        /// <summary>
        /// Opening and closing a connection from the connection pool
        /// </summary>
        public static void OpenPooledConnectionTest()
        {
            using (var connection = new SqlConnection(SqlAuthTcpConnectionString))
            {
                connection.OpenAsync().Wait();
            }
        }

        /// <summary>
        /// ExecuteScalar using a very small data set
        /// </summary>
        public static void SelectOneTest()
        {
            using (var connection = new SqlConnection(SqlAuthTcpConnectionString))
            using (var command = new SqlCommand("SELECT 1", connection))
            {
                connection.OpenAsync().Wait();
                command.ExecuteScalarAsync().Wait();
            }
        }

        /// <summary>
        /// DataReader selecting data passed to the server via parameters
        /// </summary>
        public static void SelectParametersTest()
        {
            using (var connection = new SqlConnection(SqlAuthTcpConnectionString))
            using (var command = new SqlCommand("SELECT @number, @string, @coercenumber, @coercestring", connection))
            {
                command.Parameters.Add(new SqlParameter("number", 1));
                command.Parameters.Add(new SqlParameter("string", "Hello, World!"));
                command.Parameters.Add(new SqlParameter("coercenumber", SqlDbType.Int) { Value = 1.5 });
                command.Parameters.Add(new SqlParameter("coercestring", SqlDbType.NVarChar) { Value = 2 });

                connection.OpenAsync().Wait();
                using (var reader = command.ExecuteReaderAsync().Result)
                {
                    reader.ReadAsync().Wait();

                    reader.GetFieldValue<int>(0);
                    reader.GetFieldValue<string>(1);
                    reader.GetFieldValue<int>(2);
                    reader.GetFieldValue<string>(3);
                }
            }
        }

        /// <summary>
        /// Streaming a large amount of data from the server using TCP
        /// </summary>
        public static void LargeStreamTcpTest() => LargeStreamTest(SqlAuthTcpConnectionString);

        /// <summary>
        /// Streaming a large amount of data from the server using Named Pipes
        /// </summary>
        public static void LargeStreamNpTest() => LargeStreamTest(SqlAuthNpConnectionString);

        /// <summary>
        /// Streaming a large amount of data from the server
        /// </summary>
        private static void LargeStreamTest(string connectionString)
        {
            const int dataSize = 128 * 1024;
            const int blockSize = 1024;

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("SELECT @data", connection))
            {
                var outStream = new MockStream(dataSize);
                command.Parameters.Add(new SqlParameter("data", outStream));

                connection.OpenAsync().Wait();
                using (var reader = command.ExecuteReaderAsync().Result)
                {
                    reader.ReadAsync().Wait();

                    using (var inStream = reader.GetStream(0))
                    {
                        int bytesRead;
                        byte[] data = new byte[blockSize];
                        do
                        {
                            bytesRead = inStream.ReadAsync(data, 0, blockSize).Result;
                        } while (bytesRead != 0);
                    }
                }
            }
        }
    }
}