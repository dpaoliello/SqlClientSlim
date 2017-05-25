using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace System.Data.SqlClient.Tests
{
    internal static class Utilities
    {
        public const string MarsFragment = "MultipleActiveResultSets=true;";
        public const string MaxPoolOf1Fragment = "Max Pool Size=1;";
        public const string NonPooledFragment = "Pooling=false;";

        public static readonly string SqlAuthConnectionString = Environment.GetEnvironmentVariable("TEST_TCP_CONN_STR");
        public static readonly string SqlAuthNamesPipesConnectionString = Environment.GetEnvironmentVariable("TEST_NP_CONN_STR");
        public static readonly string IntegratedAuthConnectionString = ConvertConnectionStringToUseIntegratedAuth(SqlAuthConnectionString);

        private static string ConvertConnectionStringToUseIntegratedAuth(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(SqlAuthConnectionString);
            builder.Remove("User ID");
            builder.Remove("Password");
            builder.IntegratedSecurity = true;
            return builder.ToString();
        }

        public static Task KillConnection(SqlConnection connection)
        {
            connection.KillConnection();

            // Connection is only checked if the last check was more that 50ms ago
            // so we need to add in a slight delay to ensure that the connection is rechecked.
            return Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        /// <summary>
        /// Creates an XmlReader for the given XML text fragment.
        /// </summary>
        internal static XmlReader CreateXmlReader(string xmlText)
        {
            return XmlReader.Create(new StringReader(xmlText), new XmlReaderSettings() { ConformanceLevel = ConformanceLevel.Fragment });
        }
    }
}
