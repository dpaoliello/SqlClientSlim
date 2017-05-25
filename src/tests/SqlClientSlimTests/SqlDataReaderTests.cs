using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    [Trait("connection", "tcp")]
    public sealed class SqlDataReaderTests
    {
        /// <summary>
        /// DataSet to be used in the SelectSqlTypesTest
        /// </summary>
        public static IEnumerable<object[]> SelectSqlTypesDataSet()
        {
            yield return MakeSelectSqlTypesDataSetRow(new SqlBinary(new byte[] { 0x12, 0x34 }));
            yield return MakeSelectSqlTypesDataSetRow(new SqlBoolean(true));
            yield return MakeSelectSqlTypesDataSetRow(new SqlByte(0x12));
            yield return MakeSelectSqlTypesDataSetRow(new SqlDateTime(2010, 1, 2, 3, 4, 5));
            yield return MakeSelectSqlTypesDataSetRow(new SqlDecimal(0.1234M));
            yield return MakeSelectSqlTypesDataSetRow(new SqlDouble(0.12345678));
            yield return MakeSelectSqlTypesDataSetRow(new SqlGuid(Guid.NewGuid()));
            yield return MakeSelectSqlTypesDataSetRow(new SqlInt16(1234));
            yield return MakeSelectSqlTypesDataSetRow(new SqlInt32(12345678));
            yield return MakeSelectSqlTypesDataSetRow(new SqlInt64(1234567890123456));
            yield return MakeSelectSqlTypesDataSetRow(new SqlMoney(12.34M));
            yield return MakeSelectSqlTypesDataSetRow(new SqlSingle(0.1234));
            yield return MakeSelectSqlTypesDataSetRow(new SqlString("Hello"));

            // SqlXml requires a custom comparer
            var xml = new SqlXml(Utilities.CreateXmlReader("<a>b</a>"));
            yield return new object[] {
                xml,
                (Action<SqlDataReader>)(reader => VerifySqlTypeFromReader(reader, xml, validateINullable: false, comparer: SqlXmlEqualityComparer.Instance)) };

            // SqlBytes and SqlChars are for input only
            yield return new object[] {
                new SqlBytes(new byte[] { 0x12, 0x34 }),
                (Action<SqlDataReader>)(reader => VerifySqlTypeFromReader(reader, new SqlBinary(new byte[] { 0x12, 0x34 }))) };
            yield return new object[] {
                new SqlChars(new char[] { 'H', 'i' }),
                (Action<SqlDataReader>)(reader => VerifySqlTypeFromReader(reader, new SqlString("Hi"))) };

            // Special case: SqlXml to SqlString
            yield return new object[] {
                new SqlXml(Utilities.CreateXmlReader("<c>d</c>")),
                (Action<SqlDataReader>)(reader => VerifySqlTypeFromReader(reader, new SqlString("<c>d</c>"), validateINullable: false)) };
            yield return new object[] {
                SqlXml.Null,
                (Action<SqlDataReader>)(reader => VerifySqlTypeFromReader(reader, SqlString.Null, validateINullable: false)) };
        }

        /// <summary>
        /// Verifies that parameters can be sent to the server and then retrieved
        /// </summary>
        [Fact]
        public async Task EndToEndTest()
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString))
            using (var command = new SqlCommand("SELECT @number, @string", connection))
            {
                command.Parameters.Add(new SqlParameter("number", 1));
                command.Parameters.Add(new SqlParameter("string", "Hello, World!"));

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    Assert.Equal(true, await reader.ReadAsync());

                    Assert.Equal(1, reader.GetFieldValue<int>(0));
                    Assert.Equal("Hello, World!", await reader.GetFieldValueAsync<string>(1));
                }
            }
        }

        /// <summary>
        /// Verifies that SqlTypes can be round-tripped to the server via SqlParameters and GetFieldValue
        /// </summary>
        [Theory]
        [MemberData("SelectSqlTypesDataSet")]
        public async Task SelectSqlTypesTest(object inputValue, Action<SqlDataReader> verify)
        {
            using (var connection = new SqlConnection(Utilities.SqlAuthConnectionString))
            using (var command = new SqlCommand("SELECT @p", connection))
            {
                command.Parameters.Add(new SqlParameter("@p", inputValue));

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    Assert.Equal(true, await reader.ReadAsync());
                    verify(reader);
                }
            }
        }

        /// <summary>
        /// Helper method to create a single scenario for SelectSqlTypesDataSet
        /// </summary>
        private static object[] MakeSelectSqlTypesDataSetRow<T>(T value)
            where T : INullable
        {
            return new object[] { value, (Action<SqlDataReader>)(reader => VerifySqlTypeFromReader(reader, value)) };
        }

        /// <summary>
        /// Helper method to validate that the given <paramref name="reader"/> has the <paramref name="expectedValue"/> at index 0.
        /// </summary>
        private static void VerifySqlTypeFromReader<T>(SqlDataReader reader, T expectedValue, bool validateINullable = true, IEqualityComparer<T> comparer = null)
            where T : INullable
        {
            Assert.Equal(expectedValue, reader.GetFieldValue<T>(0), comparer ?? EqualityComparer<T>.Default);
            if (validateINullable)
            {
                Assert.Equal(expectedValue, (T)reader.GetFieldValue<INullable>(0));
            }
        }

        private struct SqlXmlEqualityComparer : IEqualityComparer<SqlXml>
        {
            public static readonly IEqualityComparer<SqlXml> Instance = new SqlXmlEqualityComparer();

            public bool Equals(SqlXml x, SqlXml y)
            {
                return x.Value == y.Value;
            }

            public int GetHashCode(SqlXml obj)
            {
                return obj.Value.GetHashCode();
            }
        }
    }
}
