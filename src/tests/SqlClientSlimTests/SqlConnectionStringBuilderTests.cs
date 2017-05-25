using Xunit;

namespace System.Data.SqlClient.Tests
{
    [Trait("connection", "none")]
    public class SqlConnectionStringBuilderTests
    {
        [Fact]
        public void UnsupportedKeywordTest()
        {
            var builder = new SqlConnectionStringBuilder();

            // Previously allowed, but not deprecated keywords
            Assert.Throws<NotSupportedException>(() => { var value = builder["async"]; });
            Assert.Throws<NotSupportedException>(() => { builder["async"] = "true"; });

            // There is a different message for the "network" keywork
            Assert.Throws<NotSupportedException>(() => { var value = builder["network library"]; });
            Assert.Throws<NotSupportedException>(() => { builder["network library"] = ""; });
        }
    }
}
