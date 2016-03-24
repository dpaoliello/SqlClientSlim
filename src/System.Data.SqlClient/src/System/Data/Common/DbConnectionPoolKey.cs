// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.



//------------------------------------------------------------------------------

namespace System.Data.Common
{
    // DbConnectionPoolKey: Base class implementation of a key to connection pool groups
    //  Only connection string is used as a key
    internal struct DbConnectionPoolKey : IEquatable<DbConnectionPoolKey>
    {
        private string _connectionString;

        internal DbConnectionPoolKey(string connectionString)
        {
            _connectionString = connectionString;
        }

        internal string ConnectionString
        {
            get
            {
                return _connectionString;
            }

            set
            {
                _connectionString = value;
            }
        }

        public bool Equals(DbConnectionPoolKey other)
        {
            return object.ReferenceEquals(_connectionString, other._connectionString) ||
                (_connectionString == other._connectionString);
        }

        public override bool Equals(object obj)
        {
            return (obj is DbConnectionPoolKey && Equals((DbConnectionPoolKey)obj));
        }

        public override int GetHashCode()
        {
            return _connectionString == null ? 0 : _connectionString.GetHashCode();
        }
    }
}
