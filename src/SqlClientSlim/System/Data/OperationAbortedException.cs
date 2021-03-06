// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.



//------------------------------------------------------------------------------

using System.Data.SqlClient.Resources;

namespace System.Data
{
    public sealed class OperationAbortedException : Exception
    {
        private OperationAbortedException(string message, Exception innerException) : base(message, innerException)
        {
            HResult = unchecked((int)0x80131936);
        }


        static internal OperationAbortedException Aborted(Exception inner)
        {
            OperationAbortedException e;
            if (inner == null)
            {
                e = new OperationAbortedException(Res.GetString(Res.ADP_OperationAborted), null);
            }
            else
            {
                e = new OperationAbortedException(Res.GetString(Res.ADP_OperationAbortedExceptionMessage), inner);
            }
            return e;
        }
    }
}
