using System.Data;
using System.Data.SqlClient;

namespace PerfTest
{
    internal static class SqlClientTests
    {
        private const string SqlAuthConnectionString = "server=localhost;user id=sa;password=452g34f23t4324t2g43t;";

        public static void OpenPooledConnectionTest()
        {
            using (var connection = new SqlConnection(SqlAuthConnectionString))
            {
                connection.OpenAsync().Wait();
            }
        }

        public static void SelectOneTest()
        {
            using (var connection = new SqlConnection(SqlAuthConnectionString))
            using (var command = new SqlCommand("SELECT 1", connection))
            {
                connection.OpenAsync().Wait();
                command.ExecuteScalarAsync().Wait();
            }
        }

        public static void SelectParametersTest()
        {
            using (var connection = new SqlConnection(SqlAuthConnectionString))
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

        public static void LargeStreamTest()
        {
            const int dataSize = 128 * 1024;
            const int blockSize = 1024;

            using (var connection = new SqlConnection(SqlAuthConnectionString))
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