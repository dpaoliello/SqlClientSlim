﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// TCP connection handle
    /// </summary>
    internal class SNITCPHandle : SNIHandle
    {
        private readonly string _targetServer;
        private readonly object _callbackObject;
        private readonly Socket _socket;
        private readonly NetworkStream _tcpStream;
        private readonly TaskScheduler _writeScheduler;
        private readonly TaskFactory _writeTaskFactory;

        private Stream _stream;
        private TcpClient _tcpClient;
        private SslStream _sslStream;
        private SslOverTdsStream _sslOverTdsStream;
        private SNIAsyncCallback _receiveCallback;
        private SNIAsyncCallback _sendCallback;
        private DebugLock _debugLock;

        private bool _validateCert = true;
        private int _bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE;
        private Guid _connectionId = Guid.NewGuid();

        /// <summary>
        /// Dispose object
        /// </summary>
        public override void Dispose()
        {
            using (_debugLock.Acquire(this))
            {
                if (_sslOverTdsStream != null)
                {
                    _sslOverTdsStream.Dispose();
                    _sslOverTdsStream = null;
                }

                if (_sslStream != null)
                {
                    _sslStream.Dispose();
                    _sslStream = null;
                }

                if (_tcpClient != null)
                {
                    _tcpClient.Dispose();
                    _tcpClient = null;
                }
            }
        }

        /// <summary>
        /// Connection ID
        /// </summary>
        public override Guid ConnectionId
        {
            get
            {
                return _connectionId;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serverName">Server name</param>
        /// <param name="port">TCP port number</param>
        /// <param name="timerExpire">Connection timer expiration</param>
        /// <param name="callbackObject">Callback object</param>
        public SNITCPHandle(string serverName, int port, long timerExpire, object callbackObject)
        {
            _writeScheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;
            _writeTaskFactory = new TaskFactory(_writeScheduler);
            _callbackObject = callbackObject;
            _targetServer = serverName;

            try
            {
                _tcpClient = new TcpClient();

                IAsyncResult result = _tcpClient.BeginConnect(serverName, port, null, null);

                TimeSpan ts;

                // In case the Timeout is Infinite, we will receive the max value of Int64 as the tick count
                // The infinite Timeout is a function of ConnectionString Timeout=0
                bool isInfiniteTimeOut = long.MaxValue == timerExpire;
                if (!isInfiniteTimeOut)
                {
                    ts = DateTime.FromFileTime(timerExpire) - DateTime.Now;
                    ts = ts.Ticks < 0 ? TimeSpan.FromTicks(0) : ts;
                }

                if (!(isInfiniteTimeOut ? result.AsyncWaitHandle.WaitOne(-1) : result.AsyncWaitHandle.WaitOne(ts)))
                {
                    ReportTcpSNIError(0, 40, SR.SNI_ERROR_40);
                    return;
                }

                _tcpClient.EndConnect(result);

                _tcpClient.NoDelay = true;
                _tcpStream = _tcpClient.GetStream();
                _socket = _tcpClient.Client;

                _sslOverTdsStream = new SslOverTdsStream(_tcpStream);
                _sslStream = new SslStream(_sslOverTdsStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            }
            catch (SocketException se)
            {
                ReportTcpSNIError(se.Message);
                return;
            }
            catch (Exception e)
            {
                ReportTcpSNIError(e.Message);
                return;
            }

            _stream = _tcpStream;
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public override SNIError EnableSsl(uint options)
        {
            _validateCert = (options & TdsEnums.SNI_SSL_VALIDATE_CERTIFICATE) != 0;

            try
            {
                _sslStream.AuthenticateAsClient(_targetServer);
                _sslOverTdsStream.FinishHandshake();
            }
            catch (AuthenticationException aue)
            {
                return ReportTcpSNIError(aue.Message);
            }
            catch (InvalidOperationException ioe)
            {
                return ReportTcpSNIError(ioe.Message);
            }

            _stream = _sslStream;
            return null;
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public override void DisableSsl()
        {
            _sslStream.Dispose();
            _sslStream = null;
            _sslOverTdsStream.Dispose();
            _sslOverTdsStream = null;

            _stream = _tcpStream;
        }

        /// <summary>
        /// Validate server certificate callback
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="cert">X.509 certificate</param>
        /// <param name="chain">X.509 chain</param>
        /// <param name="policyErrors">Policy errors</param>
        /// <returns>True if certificate is valid</returns>
        private bool ValidateServerCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors policyErrors)
        {
            if (!_validateCert)
            {
                return true;
            }

            return SNICommon.ValidateSslServerCertificate(_targetServer, sender, cert, chain, policyErrors);
        }

        /// <summary>
        /// Set buffer size
        /// </summary>
        /// <param name="bufferSize">Buffer size</param>
        public void SetBufferSize(int bufferSize)
        {
            _bufferSize = bufferSize;
            _socket.SendBufferSize = bufferSize;
            _socket.ReceiveBufferSize = bufferSize;
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override SNIError Send(SNIPacket packet)
        {
            using (_debugLock.Acquire(this))
            {
                try
                {
                    packet.WriteToStream(_stream);
                    return null;
                }
                catch (ObjectDisposedException ode)
                {
                    return ReportTcpSNIError(ode.Message);
                }
                catch (SocketException se)
                {
                    return ReportTcpSNIError(se.Message);
                }
                catch (IOException ioe)
                {
                    return ReportTcpSNIError(ioe.Message);
                }
            }
        }

        /// <summary>
        /// Receive a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeout">Timeout</param>
        /// <returns>SNI error code</returns>
        public override SNIError Receive(ref SNIPacket packet, int timeout)
        {
            using (_debugLock.Acquire(this))
            {
                try
                {
                    _tcpClient.ReceiveTimeout = (timeout != 0) ? timeout : 1;
                    packet = new SNIPacket(null);
                    packet.Allocate(_bufferSize);
                    packet.ReadFromStream(_stream);

                    if (packet.Length == 0)
                    {
                        return ReportError(packet, "Connection was terminated");
                    }

                    return null;
                }
                catch (ObjectDisposedException ode)
                {
                    return ReportError(packet, ode.Message);
                }
                catch (SocketException se)
                {
                    return ReportError(packet, se.Message);
                }
                catch (IOException ioe)
                {
                    return ReportError(packet, ioe.Message);
                }
                finally
                {
                    _tcpClient.ReceiveTimeout = 0;
                }
            }
        }

        /// <summary>
        /// Set async callbacks
        /// </summary>
        /// <param name="receiveCallback">Receive callback</param>
        /// <param name="sendCallback">Send callback</param>
        /// <summary>
        public override void SetAsyncCallbacks(SNIAsyncCallback receiveCallback, SNIAsyncCallback sendCallback)
        {
            _receiveCallback = receiveCallback;
            _sendCallback = sendCallback;
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>SNI error code</returns>
        public override bool SendAsync(SNIPacket packet, SNIAsyncCallback callback, bool forceCallback, out SNIError sniError)
        {
            try
            {
                Task writeTask;
                using (_debugLock.Acquire(this))
                {
                    writeTask = packet.WriteToStreamAsync(_stream);
                }

                if (writeTask.IsCompleted && !forceCallback)
                {
                    if (writeTask.IsFaulted)
                    {
                        sniError = new SNIError(SNIProviders.TCP_PROV, 0, 0, writeTask.Exception.Message);
                    }
                    else
                    {
                        sniError = null;
                    }
                    return true;
                }
                else
                {
                    writeTask.ContinueWith(SendAsyncContinuation,
                        Tuple.Create(packet, callback),
                        CancellationToken.None,
                        TaskContinuationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                    sniError = null;
                    return false;
                }
            }
            catch (ObjectDisposedException ode)
            {
                sniError = ReportError(packet, ode.Message);
            }
            catch (SocketException se)
            {
                sniError = ReportError(packet, se.Message);
            }
            catch (IOException ioe)
            {
                sniError = ReportError(packet, ioe.Message);
            }

            // Fallthrough: We caught an error
            Debug.Assert(sniError != null, "Should have either set an error or returned early");
            if (forceCallback)
            {
                Task.Factory.StartNew(SendAsyncErrorContinuation,
                    Tuple.Create(packet, callback, sniError),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default);
                sniError = null;
                return false;
            }
            else
            {
                return true;
            }
        }

        private void SendAsyncErrorContinuation(object rawState)
        {
            var state = (Tuple<SNIPacket, SNIAsyncCallback, SNIError>)rawState;
            SNIPacket packet = state.Item1;
            SNIAsyncCallback callback = state.Item2;
            SNIError sniError = state.Item3;
            if (callback != null)
            {
                callback(packet, sniError);
            }
            else
            {
                _sendCallback(packet, sniError);
            }
        }

        private void SendAsyncContinuation(Task task, object rawState)
        {
            var state = (Tuple<SNIPacket, SNIAsyncCallback>)rawState;
            SNIPacket packet = state.Item1;
            SNIAsyncCallback callback = state.Item2;

            if (task.IsFaulted)
            {
                SNIError sniError = new SNIError(SNIProviders.TCP_PROV, 0, 0, task.Exception.Message);

                if (callback != null)
                {
                    callback(packet, sniError);
                }
                else
                {
                    _sendCallback(packet, sniError);
                }
            }
            else
            {
                if (callback != null)
                {
                    callback(packet, null);
                }
                else
                {
                    _sendCallback(packet, null);
                }
            }
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override bool ReceiveAsync(bool forceCallback, ref SNIPacket packet, out SNIError sniError)
        {
            using (_debugLock.Acquire(this))
            {
                packet = new SNIPacket(null);
                packet.Allocate(_bufferSize);

                try
                {
                    packet.ReadFromStreamAsync(_stream, _receiveCallback);
                    sniError = null;
                    return false;
                }
                catch (ObjectDisposedException ode)
                {
                    sniError = ReportError(packet, ode.Message);
                }
                catch (SocketException se)
                {
                    sniError = ReportError(packet, se.Message);
                }
                catch (IOException ioe)
                {
                    sniError = ReportError(packet, ioe.Message);
                }

                // Fallthrough: Something failed
                Debug.Assert(sniError != null, "Error should be set if we didn't already return");
                if (forceCallback)
                {
                    Task.Factory.StartNew(
                        ReceiveAsyncErrorContinuation,
                        Tuple.Create(packet, sniError),
                        CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                    sniError = null;
                    return false;
                }
                return true;
            }
        }

        private void ReceiveAsyncErrorContinuation(object rawState)
        {
            var state = (Tuple<SNIPacket, SNIError>)rawState;
            SNIPacket packet = state.Item1;
            SNIError sniError = state.Item2;
            _receiveCallback(packet, sniError);
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

        private SNIError ReportTcpSNIError(uint nativeError, uint sniError, string errorMessage)
        {
            return new SNIError(SNIProviders.TCP_PROV, nativeError, sniError, errorMessage);
        }

        private SNIError ReportTcpSNIError(string errorMessage)
        {
            return ReportTcpSNIError(0, 0, errorMessage);
        }

        private SNIError ReportError(SNIPacket packet, string errorMessage)
        {
            return ReportTcpSNIError(0, 0, errorMessage);
        }

#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public override void KillConnection()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Disconnect(reuseSocket: false);
        }
#endif

        private struct DebugLock
        {
            private const int NoThread = 0;
#if DEBUG
            private int _threadHoldingLock;
#endif

            public IDisposable Acquire(SNITCPHandle handle)
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
                private readonly SNITCPHandle _handle;

                public Holder(SNITCPHandle handle)
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