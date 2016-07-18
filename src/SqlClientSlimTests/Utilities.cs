using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace System.Data.SqlClient.Tests
{
    internal static class Utilities
    {
        private const string SqlAuthFragment = "user id=sa;password=452g34f23t4324t2g43t;";

        public const string ServerOnlyConnectionString = "server=localhost;";
        public const string ServerOnlyWithNamedPipesConnectionString = "server=np:localhost;";

        public const string SqlAuthConnectionString = ServerOnlyConnectionString + SqlAuthFragment;
        public const string SqlAuthNamesPipesConnectionString = ServerOnlyWithNamedPipesConnectionString + SqlAuthFragment;
        public const string IntegratedAuthConnectionString = ServerOnlyConnectionString + "integrated security=true;";

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
