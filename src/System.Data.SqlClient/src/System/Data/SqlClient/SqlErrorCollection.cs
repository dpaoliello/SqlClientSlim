// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

namespace System.Data.SqlClient
{
    public sealed class SqlErrorCollection : ICollection, IReadOnlyList<SqlError>
    {
        private List<SqlError> _errors = new List<SqlError>();

        internal SqlErrorCollection()
        {
        }
        
        void System.Collections.ICollection.CopyTo(Array array, int index)
        {
            _errors.CopyTo((SqlError[])array, index);
        }

        public void CopyTo(SqlError[] array, int index) => _errors.CopyTo(array, index);

        public int Count => _errors.Count;

        object ICollection.SyncRoot => this;

        bool ICollection.IsSynchronized => false;

        public SqlError this[int index] => (SqlError)_errors[index];

        public IEnumerator GetEnumerator() => _errors.GetEnumerator();

        internal void Add(SqlError error)
        {
            _errors.Add(error);
        }

        IEnumerator<SqlError> IEnumerable<SqlError>.GetEnumerator()
        {
            return ((IReadOnlyList<SqlError>)_errors).GetEnumerator();
        }
    }
}
