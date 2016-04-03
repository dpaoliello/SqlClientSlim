using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    public sealed class SqlParameterTests
    {
        /// <summary>
        /// Test cases for CoerceToStringTest.
        /// </summary>
        public static IEnumerable<object[]> CoerceToStringDataSet()
        {
            const string testString = "Hello, World!";

            yield return new object[] { new SqlXml(CreateXmlReader("<a><b /><c>text</c></a>")), "<a><b /><c>text</c></a>" };
            yield return new object[] { CreateXmlReader("<a><b /><c>text</c></a>"), "<a><b /><c>text</c></a>" };
            yield return new object[] { new SqlString(testString), testString };
            yield return new object[] { testString.ToCharArray(), testString };
            yield return new object[] { new SqlChars(testString.ToCharArray()), testString };
            yield return new object[] { new StringReader(testString), "\uFEFF" + testString };
            yield return new object[] { 1234, "1234" };
        }

        /// <summary>
        /// Test cases for CoerceToBytesTest.
        /// </summary>
        public static IEnumerable<object[]> CoerceToBytesDataSet()
        {
            byte[] testBytes = { 0xDE, 0xAD, 0xBE, 0xEF };
            yield return new object[] { new SqlBytes(testBytes), testBytes };
            yield return new object[] { new MemoryStream(testBytes), testBytes };
        }

        /// <summary>
        /// Test cases for CoerceToTimeTest.
        /// </summary>
        public static IEnumerable<object[]> CoerceToTimeDataSet()
        {
            TimeSpan testTimeSpan = new TimeSpan(1, 2, 3);
            yield return new object[] { testTimeSpan.ToString(), testTimeSpan };
        }

        /// <summary>
        /// Test cases for CoerceToDateTimeOffsetTest.
        /// </summary>
        public static IEnumerable<object[]> CoerceToDateTimeOffsetDataSet()
        {
            DateTimeOffset testDateTime = new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.Zero);
            yield return new object[] { testDateTime.ToString(), testDateTime };
            yield return new object[] { testDateTime.LocalDateTime, testDateTime };
        }

        /// <summary>
        /// Test cases for CoerceToCurrencyTest.
        /// </summary>
        public static IEnumerable<object[]> CoerceToCurrencyDataSet()
        {
            decimal testDecimal = 1.234M;
            yield return new object[] { testDecimal.ToString(), testDecimal };
            yield return new object[] { 1234, 1234M };
        }

        /// <summary>
        /// Verifies that values are coerced into strings correctly.
        /// </summary>
        [Theory]
        [MemberData("CoerceToStringDataSet")]
        public Task CoerceToStringTest(object inputValue, string expectedOutputValue)
        {
            return CoercionTestRunner(SqlDbType.NVarChar, inputValue, expectedOutputValue);
        }

        /// <summary>
        /// Verifies that values are coerced into byte arrays correctly.
        /// </summary>
        [Theory]
        [MemberData("CoerceToBytesDataSet")]
        public Task CoerceToBytesTest(object inputValue, byte[] expectedOutputValue)
        {
            return CoercionTestRunner(SqlDbType.VarBinary, inputValue, expectedOutputValue);
        }

        /// <summary>
        /// Verifies that values are coerced into times correctly.
        /// </summary>
        [Theory]
        [MemberData("CoerceToTimeDataSet")]
        public Task CoerceToTimeTest(object inputValue, TimeSpan expectedOutputValue)
        {
            return CoercionTestRunner(SqlDbType.Time, inputValue, expectedOutputValue);
        }

        /// <summary>
        /// Verifies that values are coerced into date time offsets correctly.
        /// </summary>
        [Theory]
        [MemberData("CoerceToDateTimeOffsetDataSet")]
        public Task CoerceToDateTimeOffsetTest(object inputValue, DateTimeOffset expectedOutputValue)
        {
            return CoercionTestRunner(SqlDbType.DateTimeOffset, inputValue, expectedOutputValue);
        }

        /// <summary>
        /// Verifies that values are coerced into currencies correctly.
        /// </summary>
        [Theory]
        [MemberData("CoerceToCurrencyDataSet")]
        public Task CoerceToCurrencyTest(object inputValue, decimal expectedOutputValue)
        {
            return CoercionTestRunner(SqlDbType.Money, inputValue, expectedOutputValue);
        }

        /// <summary>
        /// Runner for primitive type coercion tests - sends the value to the SQL Server and verifies that the returned value is expected.
        /// </summary>
        private static async Task CoercionTestRunner<T>(SqlDbType parameterType, object inputValue, T expectedOutputValue)
        {
            Assert.Equal(expectedOutputValue, await ParameterRoundTrip<T>(parameterType, inputValue));
        }

        /// <summary>
        /// Runner for array type coercion tests - sends the value to the SQL Server and verifies that the returned value is expected.
        /// </summary>
        private static async Task CoercionTestRunner<T>(SqlDbType parameterType, object inputValue, T[] expectedOutputValue)
        {
            Assert.Equal<T>(expectedOutputValue, await ParameterRoundTrip<T[]>(parameterType, inputValue));
        }

        /// <summary>
        /// Sends a value to the SQL Server and returns the returned value.
        /// </summary>
        private static async Task<T> ParameterRoundTrip<T>(SqlDbType parameterType, object inputValue)
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString))
            using (var command = new SqlCommand("SELECT @p", connection))
            {
                command.Parameters.Add(new SqlParameter("@p", parameterType) { Value = inputValue });

                await connection.OpenAsync();
                T oututValue = (T)await command.ExecuteScalarAsync();
                return oututValue;
            }
        }

        /// <summary>
        /// Creates an XmlReader for the given XML text fragment.
        /// </summary>
        private static XmlReader CreateXmlReader(string xmlText)
        {
            return XmlReader.Create(new StringReader(xmlText), new XmlReaderSettings() { ConformanceLevel = ConformanceLevel.Fragment });
        }
    }
}
