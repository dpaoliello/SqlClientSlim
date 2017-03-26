// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// TCP connection handle
    /// </summary>
    internal sealed class SNITCPHandle : SNITransportHandle
    {
        private readonly Socket _socket;
        private readonly NetworkStream _tcpStream;

        private const int MaxParallelIpAddresses = 64;

        protected override SNIProviders ProviderNumber => SNIProviders.TCP_PROV;

        protected override Stream OriginalStream => _tcpStream;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serverName">Server name</param>
        /// <param name="port">TCP port number</param>
        /// <param name="timerExpire">Connection timer expiration</param>
        /// <param name="callbackObject">Callback object</param>
        public SNITCPHandle(string serverName, int port, long timerExpire, bool parallel, out SNIError sniError)
            : base(serverName)
        {
            try
            {
                TimeSpan ts;

                // In case the Timeout is Infinite, we will receive the max value of Int64 as the tick count
                // The infinite Timeout is a function of ConnectionString Timeout=0
                bool isInfiniteTimeOut = long.MaxValue == timerExpire;
                if (!isInfiniteTimeOut)
                {
                    ts = DateTime.FromFileTime(timerExpire) - DateTime.Now;
                    ts = ts.Ticks < 0 ? TimeSpan.FromTicks(0) : ts;
                }

                Task<Socket> connectTask;
                if (parallel)
                {
                    Task<IPAddress[]> serverAddrTask = Dns.GetHostAddressesAsync(serverName);
                    serverAddrTask.Wait(ts);
                    IPAddress[] serverAddresses = serverAddrTask.Result;

                    if (serverAddresses.Length > MaxParallelIpAddresses)
                    {
                        // Fail if above 64 to match legacy behavior
                        sniError = new SNIError(SNIProviders.TCP_PROV, 0, SNICommon.MultiSubnetFailoverWithMoreThan64IPs, string.Empty);
                        return;
                    }

                    connectTask = ConnectAsync(serverAddresses, port);
                }
                else
                {
                    connectTask = ConnectAsync(serverName, port);
                }

                if (!(isInfiniteTimeOut ? connectTask.Wait(-1) : connectTask.Wait(ts)))
                {
                    sniError = new SNIError(SNIProviders.TCP_PROV, 0, SNICommon.ConnOpenFailedError, SQLMessage.Timeout());
                    return;
                }

                _socket = connectTask.Result;
                _socket.NoDelay = true;
                _tcpStream = new NetworkStream(_socket, true);

                _sslOverTdsStream = new SslOverTdsStream(_tcpStream);
                _sslStream = new SslStream(_sslOverTdsStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            }
            catch (SocketException se)
            {
                sniError = new SNIError(SNIProviders.TCP_PROV, 0, se);
                return;
            }
            catch (Exception e)
            {
                sniError = new SNIError(SNIProviders.TCP_PROV, 0, e);
                return;
            }

            _stream = _tcpStream;
            sniError = null;
        }

        private static async Task<Socket> ConnectAsync(string serverName, int port)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(serverName, port).ConfigureAwait(false);
                return socket;
            }

            // On unix we can't use the instance Socket methods that take multiple endpoints

            IPAddress[] addresses = await Dns.GetHostAddressesAsync(serverName).ConfigureAwait(false);
            return await ConnectAsync(addresses, port).ConfigureAwait(false);
        }

        private static async Task<Socket> ConnectAsync(IPAddress[] serverAddresses, int port)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(serverAddresses, port).ConfigureAwait(false);
                return socket;
            }

            // On unix we can't use the instance Socket methods that take multiple endpoints

            if (serverAddresses == null)
            {
                throw new ArgumentNullException(nameof(serverAddresses));
            }
            if (serverAddresses.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serverAddresses));
            }

            // Try each address in turn, and return the socket opened for the first one that works.
            ExceptionDispatchInfo lastException = null;
            foreach (IPAddress address in serverAddresses)
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(address, port).ConfigureAwait(false);
                    return socket;
                }
                catch (Exception exc)
                {
                    socket.Dispose();
                    lastException = ExceptionDispatchInfo.Capture(exc);
                }
            }

            // Propagate the last failure that occurred
            if (lastException != null)
            {
                lastException.Throw();
            }

            // Should never get here.  Either there will have been no addresses and we'll have thrown
            // at the beginning, or one of the addresses will have worked and we'll have returned, or
            // at least one of the addresses will failed, in which case we will have propagated that.
            throw new ArgumentException();
        }

        protected override void InternalSetBufferSize(int bufferSize)
        {
            _socket.SendBufferSize = bufferSize;
            _socket.ReceiveBufferSize = bufferSize;
        }

        protected override SNIError SetupTimeoutForReceive(int timeoutInMilliseconds)
        {
            if (timeoutInMilliseconds > 0)
            {
                _socket.ReceiveTimeout = timeoutInMilliseconds;
                return null;
            }
            else if (timeoutInMilliseconds == -1)
            {   // SqlCient internally represents infinite timeout by -1, and for TcpClient this is translated to a timeout of 0 
                _socket.ReceiveTimeout = 0;
                return null;
            }
            else
            {
                // otherwise it is timeout for 0 or less than -1
                return new SNIError(SNIProviders.TCP_PROV, 0, SNICommon.ConnTimeoutError, string.Empty);
            }
        }

        /// <summary>
        /// Check SNI handle connection
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>SNI error status</returns>
        public override bool CheckConnection()
        {
            try
            {
                return (_socket.Connected && !_socket.Poll(0, SelectMode.SelectError));
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        protected override void InternalDispose()
        {
            _tcpStream.Dispose();
            _socket.Dispose();
        }

        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public sealed override void KillConnection()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (ObjectDisposedException)
            {
                // Connection has already been closed
            }
        }
    }
}