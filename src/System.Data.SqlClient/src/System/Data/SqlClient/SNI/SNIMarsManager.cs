// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// Singleton to manage all MARS connection
    /// </summary>
    internal class SNIMarsManager
    {
        public static readonly SNIMarsManager Singleton = new SNIMarsManager();
        private ConcurrentDictionary<SNIHandle, SNIMarsConnection> _connections = new ConcurrentDictionary<SNIHandle, SNIMarsConnection>();

        /// <summary>
        /// Constructor
        /// </summary>
        public SNIMarsManager()
        {
        }

        /// <summary>
        /// Create a MARS connection
        /// </summary>
        /// <param name="lowerHandle">Lower SNI handle</param>
        /// <returns>SNI error code</returns>
        public SNIError CreateMarsConnection(SNIHandle lowerHandle)
        {
            SNIMarsConnection connection = new SNIMarsConnection(lowerHandle);

            if (_connections.TryAdd(lowerHandle, connection))
            {
                connection.StartReceive();
                return null;
            }
            else
            {
                Debug.Assert(false, "Handle already exists in the collection");
                return new SNIError(SNIProviders.SMUX_PROV, 0, 0, "Internal error: Duplicate handle");
            }
        }

        /// <summary>
        /// Get a MARS connection by lower handle
        /// </summary>
        /// <param name="lowerHandle">Lower SNI handle</param>
        /// <returns>MARS connection</returns>
        public SNIMarsConnection GetConnection(SNIHandle lowerHandle)
        {
            return _connections[lowerHandle];
        }
    }
}