// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// SNI connection handle
    /// </summary>
    internal abstract class SNIHandle
    {
        /// <summary>
        /// Dispose class
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Set async callbacks
        /// </summary>
        /// <param name="receiveCallback">Receive callback</param>
        /// <param name="sendCallback">Send callback</param>
        public abstract void SetAsyncCallbacks(SNIAsyncCallback receiveCallback, SNIAsyncCallback sendCallback);

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public abstract SNIError Send(SNIPacket packet);

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>True if completed synchronously, otherwise false</returns>
        public abstract bool SendAsync(SNIPacket packet, SNIAsyncCallback callback, bool forceCallback, out SNIError error);

        /// <summary>
        /// Receive a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeout">Timeout</param>
        /// <returns>SNI error code</returns>
        public abstract SNIError Receive(ref SNIPacket packet, int timeout);

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        public abstract bool ReceiveAsync(bool forceCallback, ref SNIPacket packet, out SNIError sniError);

        /// <summary>
        /// Enable SSL
        /// </summary>
        public abstract SNIError EnableSsl(uint options);

        /// <summary>
        /// Disable SSL
        /// </summary>
        public abstract void DisableSsl();

        /// <summary>
        /// Check connection status
        /// </summary>
        /// <returns>SNI error code</returns>
        public abstract bool CheckConnection();

        /// <summary>
        /// Connection ID
        /// </summary>
        public abstract Guid ConnectionId { get; }

#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public abstract void KillConnection();
#endif
    }
}