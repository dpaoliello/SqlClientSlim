// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace System.Data.SqlClient
{
    internal static class SNINativeMethodWrapper
    {
        #region Structs\Enums

        internal enum SniSpecialErrors : uint
        {
            LocalDBErrorCode = 50,

            // multi-subnet-failover specific error codes
            MultiSubnetFailoverWithMoreThan64IPs = 47,
            MultiSubnetFailoverWithInstanceSpecified = 48,
            MultiSubnetFailoverWithNonTcpProtocol = 49,

            // max error code value
            MaxErrorValue = 50157
        }
        #endregion
    }
}

namespace System.Data
{
    internal static class SafeNativeMethods
    {
        [DllImport("api-ms-win-core-libraryloader-l1-1-0.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr HModule, [MarshalAs(UnmanagedType.LPStr), In] string funcName);

        [DllImport("api-ms-win-security-base-l1-2-0.dll", BestFitMapping = false, SetLastError = false)]
        internal static extern bool IsTokenRestricted(IntPtr TokenHandle);
    }
}