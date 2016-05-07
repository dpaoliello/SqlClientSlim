// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// In the desktop version of the framework, this file is generated from ProviderBase\DbParameterCollectionHelper.cs
//#line 1 "e:\\fxdata\\src\\ndp\\fx\\src\\data\\system\\data\\providerbase\\dbparametercollectionhelper.cs"

using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;

namespace System.Data.SqlClient
{
    public sealed partial class SqlParameterCollection : DbParameterCollection
    {
        private List<SqlParameter> _items;

        override public int Count
        {
            get
            {
                return _items?.Count ?? 0;
            }
        }

        private List<SqlParameter> InnerList
        {
            get
            {
                if (_items == null)
                {
                    _items = new List<SqlParameter>();
                }
                return _items;
            }
        }


        override public object SyncRoot
        {
            get
            {
                return ((System.Collections.ICollection)InnerList).SyncRoot;
            }
        }

        override public int Add(object value)
        {
            OnChange();
            SqlParameter castedValue = ValidateType(value);
            Validate(-1, castedValue);
            InnerList.Add(castedValue);
            return Count - 1;
        }

        override public void AddRange(System.Array values)
        {
            OnChange();
            if (null == values)
            {
                throw ADP.ArgumentNull(nameof(values));
            }
            foreach (object value in values)
            {
                ValidateType(value);
            }
            foreach (SqlParameter value in values)
            {
                Validate(-1, value);
                InnerList.Add(value);
            }
        }

        private int CheckName(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, typeof(SqlParameter));
            }
            return index;
        }

        override public void Clear()
        {
            OnChange();
            List<SqlParameter> items = _items;

            if (null != items)
            {
                foreach (SqlParameter item in items)
                {
                    item.ResetParent();
                }
                items.Clear();
            }
        }

        override public bool Contains(object value)
        {
            return (-1 != IndexOf(value));
        }

        override public void CopyTo(Array array, int index)
        {
            ((System.Collections.ICollection)InnerList).CopyTo(array, index);
        }

        override public System.Collections.IEnumerator GetEnumerator()
        {
            return ((System.Collections.ICollection)InnerList).GetEnumerator();
        }

        override protected DbParameter GetParameter(int index)
        {
            RangeCheck(index);
            return InnerList[index];
        }

        override protected DbParameter GetParameter(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, typeof(SqlParameter));
            }
            return InnerList[index];
        }

        private static int IndexOf(IEnumerable<SqlParameter> items, string parameterName)
        {
            if (null != items)
            {
                int i = 0;

                foreach (SqlParameter parameter in items)
                {
                    if (parameterName == parameter.ParameterName)
                    {
                        return i;
                    }
                    ++i;
                }
                i = 0;

                foreach (SqlParameter parameter in items)
                {
                    if (0 == ADP.DstCompare(parameterName, parameter.ParameterName))
                    {
                        return i;
                    }
                    ++i;
                }
            }
            return -1;
        }

        override public int IndexOf(string parameterName)
        {
            return IndexOf(InnerList, parameterName);
        }

        override public int IndexOf(object value)
        {
            if (null != value)
            {
                SqlParameter castedValue = ValidateType(value);

                List<SqlParameter> items = _items;
                if (null != items)
                {
                    return items.IndexOf(castedValue);
                }
            }
            return -1;
        }

        override public void Insert(int index, object value)
        {
            OnChange();
            SqlParameter castedValue = ValidateType(value);
            Validate(-1, castedValue);
            InnerList.Insert(index, castedValue);
        }

        private void RangeCheck(int index)
        {
            if ((index < 0) || (Count <= index))
            {
                throw ADP.ParametersMappingIndex(index, this);
            }
        }

        override public void Remove(object value)
        {
            OnChange();
            SqlParameter castedValue = ValidateType(value);
            int index = InnerList.IndexOf(castedValue);
            if (-1 != index)
            {
                RemoveIndex(index);
            }
            else if (this != ((SqlParameter)value).CompareExchangeParent(null, this))
            {
                throw ADP.CollectionRemoveInvalidObject(typeof(SqlParameter), this);
            }
        }

        override public void RemoveAt(int index)
        {
            OnChange();
            RangeCheck(index);
            RemoveIndex(index);
        }

        override public void RemoveAt(string parameterName)
        {
            OnChange();
            int index = CheckName(parameterName);
            RemoveIndex(index);
        }

        private void RemoveIndex(int index)
        {
            List<SqlParameter> items = InnerList;
            Debug.Assert((null != items) && (0 <= index) && (index < Count), "RemoveIndex, invalid");
            SqlParameter item = items[index];
            items.RemoveAt(index);
            item.ResetParent();
        }

        private void Replace(int index, object newValue)
        {
            List<SqlParameter> items = InnerList;
            Debug.Assert((null != items) && (0 <= index) && (index < Count), "Replace Index invalid");
            SqlParameter castedValue = ValidateType(newValue);
            Validate(index, castedValue);
            SqlParameter item = items[index];
            if (!object.ReferenceEquals(castedValue, item))
            {
                items[index] = castedValue;
                item.ResetParent();
            }
        }

        override protected void SetParameter(int index, DbParameter value)
        {
            OnChange();
            RangeCheck(index);
            Replace(index, value);
        }

        override protected void SetParameter(string parameterName, DbParameter value)
        {
            OnChange();
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, typeof(SqlParameter));
            }
            Replace(index, value);
        }

        private void Validate(int index, SqlParameter value)
        {
            if (null == value)
            {
                throw ADP.ParameterNull(nameof(value), this, typeof(SqlParameter));
            }

            object parent = value.CompareExchangeParent(this, null);
            if (null != parent)
            {
                if (this != parent)
                {
                    throw ADP.ParametersIsNotParent(typeof(SqlParameter), this);
                }
                if (index != IndexOf(value))
                {
                    throw ADP.ParametersIsParent(typeof(SqlParameter), this);
                }
            }

            String name = value.ParameterName;
            if (0 == name.Length)
            {
                index = 1;
                do
                {
                    name = ADP.Parameter + index.ToString(CultureInfo.CurrentCulture);
                    index++;
                } while (-1 != IndexOf(name));
                value.ParameterName = name;
            }
        }

        private SqlParameter ValidateType(object value)
        {
            if (null == value)
            {
                throw ADP.ParameterNull("value", this, typeof(SqlParameter));
            }
            else
            {
                SqlParameter castedValue = value as SqlParameter;
                if (castedValue != null)
                {
                    return castedValue;
                }
                else
                {
                    throw ADP.InvalidParameterType(this, typeof(SqlParameter), value);
                }
            }
        }
    };
}

