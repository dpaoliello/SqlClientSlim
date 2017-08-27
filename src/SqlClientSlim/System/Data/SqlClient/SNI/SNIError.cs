// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// SNI error
    /// </summary>
    internal class SNIError
    {
        public readonly SNIProviders provider;
        public readonly string errorMessage;
        public readonly uint nativeError;
        public readonly SNIErrorCode sniError;
        public readonly Exception exception;

        public SNIError(SNIProviders provider, uint nativeError, SNIErrorCode sniErrorCode, string errorMessage)
        {
            Debug.Assert(errorMessage != null);
            Debug.Assert((errorMessage.Length > 0) || sniErrorCode != 0);

            this.provider = provider;
            this.nativeError = nativeError;
            this.sniError = sniErrorCode;
            this.errorMessage = errorMessage;
            this.exception = null;
        }

        public SNIError(SNIProviders provider, SNIErrorCode sniErrorCode, Exception sniException)
        {
            Debug.Assert(sniException != null);

            this.provider = provider;
            this.nativeError = 0;
            this.sniError = sniErrorCode;
            this.errorMessage = null;
            this.exception = sniException;
        }
    }
}
