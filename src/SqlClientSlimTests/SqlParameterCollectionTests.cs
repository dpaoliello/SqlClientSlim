using System.Data.Common;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    [Trait("connection", "none")]
    public sealed class SqlParameterCollectionTests
    {
        /// <summary>
        /// Verifies that the basic operations of a collection work.
        /// </summary>
        [Fact]
        public void BasicOperationsTest()
        {
            SqlCommand command = new SqlCommand();
            DbParameterCollection collection = command.Parameters;
            SqlParameter firstParam = new SqlParameter();

            // Add
            int index = collection.Add(firstParam);
            Assert.Equal(0, index);

            // Get
            Assert.Same(firstParam, collection[index]);
            Assert.Equal(0, collection.IndexOf(firstParam));
            Assert.Equal(-1, collection.IndexOf(new SqlParameter()));
            Assert.Equal(-1, collection.IndexOf(null));

            // Insert
            SqlParameter secondParam = new SqlParameter();
            collection.Insert(0, secondParam);
            Assert.Same(secondParam, collection[0]);
            Assert.Same(firstParam, collection[1]);

            // Replace
            SqlParameter thirdParam = new SqlParameter();
            collection[1] = thirdParam;
            Assert.Same(thirdParam, collection[1]);

            // Remove
            collection.Remove(secondParam);
            Assert.Equal(1, collection.Count);
            Assert.Same(thirdParam, collection[0]);

            // Clear
            collection.Clear();
            Assert.Equal(0, collection.Count);
        }

        /// <summary>
        /// Verifies that added something that isn't a SqlParameter will throw.
        /// </summary>
        [Fact]
        public void AddNonSqlParameter()
        {
            SqlCommand command = new SqlCommand();
            DbParameterCollection collection = command.Parameters;

            Assert.Throws<ArgumentNullException>(() => collection.Add(null));
            Assert.Throws<InvalidCastException>(() => collection.Add(new NotASqlParameter()));
            Assert.Throws<ArgumentNullException>(() => collection.Insert(0 ,null));
            Assert.Throws<InvalidCastException>(() => collection.Insert(0, new NotASqlParameter()));
            Assert.Throws<InvalidCastException>(() => collection.AddRange(new object[] { new SqlParameter(), new NotASqlParameter() }));
            Assert.Equal(0, collection.Count);

            collection.Add(new SqlParameter());
            Assert.Throws<ArgumentNullException>(() => collection[0] = null);
            Assert.Throws<InvalidCastException>(() => collection[0] = new NotASqlParameter());
        }

        /// <summary>
        /// Verifies that searching for something that isn't a SqlParameter will throw.
        /// </summary>
        [Fact]
        public void IndexOfNonSqlParameter()
        {
            SqlCommand command = new SqlCommand();
            DbParameterCollection collection = command.Parameters;

            Assert.Throws<InvalidCastException>(() => collection.IndexOf(new NotASqlParameter()));
        }

        /// <summary>
        /// Verifies that added a duplicate parameter will throw.
        /// </summary>
        [Fact]
        public void AddDuplicate()
        {
            SqlCommand command = new SqlCommand();
            DbParameterCollection collection = command.Parameters;
            SqlParameter firstParam = new SqlParameter();
            collection.Add(firstParam);

            Assert.Throws<ArgumentException>(() => collection.Add(firstParam));
            Assert.Throws<ArgumentException>(() => collection.Insert(1, firstParam));
            Assert.Throws<ArgumentException>(() => collection.AddRange(new object[] { new SqlParameter(), firstParam }));
            // Back-compat:
            // Checking for duplications happens while adding items to the collection, so we have one item added before the exception is thrown
            Assert.Equal(2, collection.Count);

            // Setting the item to its current index should succeed
            collection[0] = firstParam;
            Assert.Throws<ArgumentException>(() => collection[1] = firstParam);
        }

        private sealed class NotASqlParameter : DbParameter
        {
            public override DbType DbType { get; set; }
            public override ParameterDirection Direction { get; set; }
            public override bool IsNullable { get; set; }
            public override string ParameterName { get; set; }
            public override int Size { get; set; }
            public override string SourceColumn { get; set; }
            public override bool SourceColumnNullMapping { get; set; }
            public override object Value { get; set; }
            public override void ResetDbType()
            { }
        }
    }
}
