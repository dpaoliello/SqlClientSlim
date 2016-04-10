using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace System.Data.SqlClient.Tests
{
    internal static class Utilities
    {
        public const string ServerOnlyConnectionString = "server=localhost;";
        public const string SqlAuthConnectionString = ServerOnlyConnectionString + "user id=sa;password=452g34f23t4324t2g43t;";
        public const string IntegratedAuthConnectionString = ServerOnlyConnectionString + "integrated security=true;";

        private static readonly MethodInfo KillConnectionMethod = typeof(SqlConnection).GetMethod("KillConnection", BindingFlags.NonPublic);

#if DEBUG
        public static Task KillConnection(SqlConnection connection)
        {
            //TODO: Use reflection instead of a public API
            //KillConnectionMethod.Invoke(connection, new object[0]);
            connection.KillConnection();

            // Connection is only checked if the last check was more that 50ms ago
            // so we need to add in a slight delay to ensure that the connection is rechecked.
            return Task.Delay(TimeSpan.FromMilliseconds(100));
        }
#endif

        /// <summary>
        /// Creates an XmlReader for the given XML text fragment.
        /// </summary>
        internal static XmlReader CreateXmlReader(string xmlText)
        {
            return XmlReader.Create(new StringReader(xmlText), new XmlReaderSettings() { ConformanceLevel = ConformanceLevel.Fragment });
        }
    }
}
