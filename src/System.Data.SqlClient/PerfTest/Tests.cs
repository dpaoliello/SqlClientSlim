extern alias SqlClientSlim;
extern alias SqlClientFull;

namespace PerfTest
{

    internal static class SqlClientSlimTests
    {
        private const string SqlAuthConnectionString = "server=localhost;user id=sa;password=452g34f23t4324t2g43t;";

        public static void OpenPooledConnectionTest()
        {
            using (var connection = new SqlClientSlim::System.Data.SqlClient.SqlConnection(SqlAuthConnectionString))
            {
                connection.OpenAsync().Wait();
            }
        }

        public static void SelectParametersTest()
        {
            using (var connection = new SqlClientSlim::System.Data.SqlClient.SqlConnection(SqlAuthConnectionString))
            using (var command = new SqlClientSlim::System.Data.SqlClient.SqlCommand("SELECT @number, @string", connection))
            {
                command.Parameters.Add(new SqlClientSlim::System.Data.SqlClient.SqlParameter("number", 1));
                command.Parameters.Add(new SqlClientSlim::System.Data.SqlClient.SqlParameter("string", "Hello, World!"));

                connection.OpenAsync().Wait();
                using (var reader = command.ExecuteReaderAsync().Result)
                {
                    reader.ReadAsync().Wait();

                    reader.GetFieldValue<int>(0);
                    reader.GetFieldValue<string>(1);
                }
            }
        }

        public static void LargeStreamTest()
        {
            const int dataSize = 128 * 1024;
            const int blockSize = 1024;

            using (var connection = new SqlClientSlim::System.Data.SqlClient.SqlConnection(SqlAuthConnectionString))
            using (var command = new SqlClientSlim::System.Data.SqlClient.SqlCommand("SELECT @data", connection))
            {
                var outStream = new MockStream(dataSize);
                command.Parameters.Add(new SqlClientSlim::System.Data.SqlClient.SqlParameter("data", outStream));

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


    internal static class SqlClientFullTests
    {
        private const string SqlAuthConnectionString = "server=localhost;user id=sa;password=452g34f23t4324t2g43t;";

        public static void OpenPooledConnectionTest()
        {
            using (var connection = new SqlClientFull::System.Data.SqlClient.SqlConnection(SqlAuthConnectionString))
            {
                connection.OpenAsync().Wait();
            }
        }

        public static void SelectParametersTest()
        {
            using (var connection = new SqlClientFull::System.Data.SqlClient.SqlConnection(SqlAuthConnectionString))
            using (var command = new SqlClientFull::System.Data.SqlClient.SqlCommand("SELECT @number, @string", connection))
            {
                command.Parameters.Add(new SqlClientFull::System.Data.SqlClient.SqlParameter("number", 1));
                command.Parameters.Add(new SqlClientFull::System.Data.SqlClient.SqlParameter("string", "Hello, World!"));

                connection.OpenAsync().Wait();
                using (var reader = command.ExecuteReaderAsync().Result)
                {
                    reader.ReadAsync().Wait();

                    reader.GetFieldValue<int>(0);
                    reader.GetFieldValue<string>(1);
                }
            }
        }

        public static void LargeStreamTest()
        {
            const int dataSize = 128 * 1024;
            const int blockSize = 1024;

            using (var connection = new SqlClientFull::System.Data.SqlClient.SqlConnection(SqlAuthConnectionString))
            using (var command = new SqlClientFull::System.Data.SqlClient.SqlCommand("SELECT @data", connection))
            {
                var outStream = new MockStream(dataSize);
                command.Parameters.Add(new SqlClientFull::System.Data.SqlClient.SqlParameter("data", outStream));

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