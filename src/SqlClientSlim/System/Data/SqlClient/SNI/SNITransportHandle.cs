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
    internal abstract class SNITransportHandle : SNIHandle
    {
        protected readonly string _targetServer;
        protected readonly object _callbackObject;

        protected Stream _stream;
        protected SslStream _sslStream;

        protected SslOverTdsStream _sslOverTdsStream;
        protected SNIAsyncCallback _receiveCallback;
        protected SNIAsyncCallback _sendCallback;

        protected bool _validateCert = true;
        protected int _bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE;
        protected Guid _connectionId = Guid.NewGuid();

        protected SNITransportHandle(string targetServer, object callbackObject)
        {
            _callbackObject = callbackObject;
            _targetServer = targetServer;
        }

        /// <summary>
        /// Connection ID
        /// </summary>
        public sealed override Guid ConnectionId
        {
            get
            {
                return _connectionId;
            }
        }

        protected abstract SNIProviders ProviderNumber { get; }

        protected abstract Stream OriginalStream { get; }

        /// <summary>
        /// Set async callbacks
        /// </summary>
        /// <param name="receiveCallback">Receive callback</param>
        /// <param name="sendCallback">Send callback</param>
        /// <summary>
        public sealed override void SetAsyncCallbacks(SNIAsyncCallback receiveCallback, SNIAsyncCallback sendCallback)
        {
            _receiveCallback = receiveCallback;
            _sendCallback = sendCallback;
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public sealed override SNIError Send(SNIPacket packet)
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
                    return new SNIError(ProviderNumber, 0, ode);
                }
                catch (SocketException se)
                {
                    return new SNIError(ProviderNumber, 0, se);
                }
                catch (IOException ioe)
                {
                    return new SNIError(ProviderNumber, 0, ioe);
                }
            }
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>SNI error code</returns>
        public sealed override bool SendAsync(SNIPacket packet, SNIAsyncCallback callback, bool forceCallback, out SNIError sniError)
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
                        sniError = new SNIError(ProviderNumber, 0, writeTask.Exception);
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
                sniError = new SNIError(ProviderNumber, 0, ode);
            }
            catch (SocketException se)
            {
                sniError = new SNIError(ProviderNumber, 0, se);
            }
            catch (IOException ioe)
            {
                sniError = new SNIError(ProviderNumber, 0, ioe);
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
                SNIError sniError = new SNIError(ProviderNumber, 0, task.Exception);

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
        public sealed override bool ReceiveAsync(bool forceCallback, ref SNIPacket packet, out SNIError sniError)
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
                    sniError = new SNIError(ProviderNumber, 0, ode);
                }
                catch (SocketException se)
                {
                    sniError = new SNIError(ProviderNumber, 0, se);
                }
                catch (IOException ioe)
                {
                    sniError = new SNIError(ProviderNumber, 0, ioe);
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
        /// Receive a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeoutInMilliseconds">Timeout in Milliseconds</param>
        /// <returns>SNI error code</returns>
        public sealed override SNIError Receive(ref SNIPacket packet, int timeoutInMilliseconds)
        {
            using (_debugLock.Acquire(this))
            {
                packet = null;
                try
                {
                    SNIError timeoutError = SetupTimeoutForReceive(timeoutInMilliseconds);
                    if (timeoutError != null)
                    {
                        return timeoutError;
                    }

                    packet = new SNIPacket(null);
                    packet.Allocate(_bufferSize);
                    packet.ReadFromStream(_stream);

                    if (packet.Length == 0)
                    {
                        return new SNIError(ProviderNumber, 0, 0, "Connection was terminated");
                    }

                    return null;
                }
                catch (ObjectDisposedException ode)
                {
                    packet = null;
                    return new SNIError(ProviderNumber, 0, ode);
                }
                catch (SocketException se)
                {
                    packet = null;
                    return new SNIError(ProviderNumber, 0, se);
                }
                catch (IOException ioe)
                {
                    uint errorCode = 0;
                    if (ioe.InnerException is SocketException && ((SocketException)(ioe.InnerException)).SocketErrorCode == SocketError.TimedOut)
                    {
                        errorCode = TdsEnums.SNI_WAIT_TIMEOUT;
                    }

                    packet = null;
                    return new SNIError(ProviderNumber, errorCode, ioe);
                }
            }
        }

        protected virtual SNIError SetupTimeoutForReceive(int timeoutInMilliseconds)
        {
            return null;
        }

        /// <summary>
        /// Set buffer size
        /// </summary>
        /// <param name="bufferSize">Buffer size</param>
        public sealed override void SetBufferSize(int bufferSize)
        {
            _bufferSize = bufferSize;
            InternalSetBufferSize(bufferSize);
        }

        protected virtual void InternalSetBufferSize(int bufferSize)
        { }

        protected virtual void InternalDispose()
        { }

        public sealed override void Dispose()
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

                InternalDispose();
            }
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public sealed override SNIError EnableSsl(uint options)
        {
            _validateCert = (options & TdsEnums.SNI_SSL_VALIDATE_CERTIFICATE) != 0;

            try
            {
                _sslStream.AuthenticateAsClientAsync(_targetServer).GetAwaiter().GetResult();
                _sslOverTdsStream.FinishHandshake();
            }
            catch (AuthenticationException aue)
            {
                return new SNIError(ProviderNumber, 0, aue);
            }
            catch (InvalidOperationException ioe)
            {
                return new SNIError(ProviderNumber, 0, ioe);
            }

            _stream = _sslStream;
            return null;
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public sealed override void DisableSsl()
        {
            _sslStream.Dispose();
            _sslStream = null;
            _sslOverTdsStream.Dispose();
            _sslOverTdsStream = null;

            _stream = OriginalStream;
        }

        /// <summary>
        /// Validate server certificate
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="cert">X.509 certificate</param>
        /// <param name="chain">X.509 chain</param>
        /// <param name="policyErrors">Policy errors</param>
        /// <returns>true if valid</returns>
        protected bool ValidateServerCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors policyErrors)
        {
            if (!_validateCert)
            {
                return true;
            }

            return SNICommon.ValidateSslServerCertificate(_targetServer, sender, cert, chain, policyErrors);
        }
    }
}
