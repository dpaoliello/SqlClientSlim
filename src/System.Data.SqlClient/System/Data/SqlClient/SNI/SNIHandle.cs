// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// SNI connection handle
    /// </summary>
    internal abstract class SNIHandle
    {
        protected DebugLock _debugLock;

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
        /// Set buffer size
        /// </summary>
        /// <param name="bufferSize">Buffer size</param>
        public abstract void SetBufferSize(int bufferSize);

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
        /// <param name="timeoutInMilliseconds">Timeout in Milliseconds</param>
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

        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public abstract void KillConnection();

        protected struct DebugLock
        {
            private const int NoThread = 0;
#if DEBUG
            private int _threadHoldingLock;
#endif

            public IDisposable Acquire(SNIHandle handle)
            {
#if DEBUG
                int previousThread = Interlocked.CompareExchange(ref _threadHoldingLock, Thread.CurrentThread.ManagedThreadId, NoThread);
                if (previousThread != NoThread)
                {
                    Debug.Assert(false, $"Another thread is holding the lock: {previousThread}");
                }
                return new Holder(handle);
#else
                return null;
#endif
            }

            public void Release()
            {
#if DEBUG
                int previousThread = Interlocked.CompareExchange(ref _threadHoldingLock, NoThread, Thread.CurrentThread.ManagedThreadId);
                Debug.Assert(previousThread == Thread.CurrentThread.ManagedThreadId, "This thread was not holding the lock");
#endif
            }

            private struct Holder : IDisposable
            {
                private readonly SNIHandle _handle;

                public Holder(SNIHandle handle)
                {
                    _handle = handle;
                }

                public void Dispose()
                {
                    _handle._debugLock.Release();
                }
            }
        }

    }
}
