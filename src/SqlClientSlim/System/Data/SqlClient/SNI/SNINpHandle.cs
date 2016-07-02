using System.IO;
using System.IO.Pipes;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// Named Pipe connection handle
    /// </summary>
    internal class SNINpHandle : SNIHandle
    {
        internal const string DefaultPipePath = @"sql\query"; // e.g. \\HOSTNAME\pipe\sql\query
        private const int MAX_PIPE_INSTANCES = 255;

        private readonly string _targetServer;
        private readonly object _callbackObject;
        private readonly TaskScheduler _writeScheduler;
        private readonly TaskFactory _writeTaskFactory;

        private Stream _stream;
        private NamedPipeClientStream _pipeStream;
        private SslOverTdsStream _sslOverTdsStream;
        private SslStream _sslStream;
        private SNIAsyncCallback _receiveCallback;
        private SNIAsyncCallback _sendCallback;

        private bool _validateCert = true;
        private int _bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE;
        private readonly Guid _connectionId = Guid.NewGuid();

        public SNINpHandle(string serverName, string pipeName, long timerExpire, object callbackObject, out SNIError error)
        {
            _targetServer = serverName;
            _callbackObject = callbackObject;
            _writeScheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;
            _writeTaskFactory = new TaskFactory(_writeScheduler);

            try
            {
                _pipeStream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.WriteThrough, Security.Principal.TokenImpersonationLevel.None);

                bool isInfiniteTimeOut = long.MaxValue == timerExpire;
                if (isInfiniteTimeOut)
                {
                    _pipeStream.Connect(Threading.Timeout.Infinite);
                }
                else
                {
                    TimeSpan ts = DateTime.FromFileTime(timerExpire) - DateTime.Now;
                    ts = ts.Ticks < 0 ? TimeSpan.FromTicks(0) : ts;

                    _pipeStream.Connect((int)ts.TotalMilliseconds);
                }
            }
            catch(TimeoutException te)
            {
                error = new SNIError(SNIProviders.NP_PROV, SNICommon.ConnTimeoutError, te);
                return;
            }
            catch(IOException ioe)
            {
                error = new SNIError(SNIProviders.NP_PROV, SNICommon.ConnOpenFailedError, ioe);
                return;
            }

            if (!_pipeStream.IsConnected || !_pipeStream.CanWrite || !_pipeStream.CanRead)
            {
                error = new SNIError(SNIProviders.NP_PROV, 0, SNICommon.ConnOpenFailedError, string.Empty);
                return;
            }

            _sslOverTdsStream = new SslOverTdsStream(_pipeStream);
            _sslStream = new SslStream(_sslOverTdsStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

            _stream = _pipeStream;
            error = null;
        }

        public override Guid ConnectionId
        {
            get
            {
                return _connectionId;
            }
        }

        public override bool CheckConnection()
        {
            return (_stream.CanWrite && _stream.CanRead);
        }

        public override void Dispose()
        {
            lock (this)
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

                if (_pipeStream != null)
                {
                    _pipeStream.Dispose();
                    _pipeStream = null;
                }
            }
        }

        public override SNIError Receive(ref SNIPacket packet, int timeout)
        {
            lock (this)
            {
                packet = null;
                try
                {
                    packet = new SNIPacket(null);
                    packet.Allocate(_bufferSize);
                    packet.ReadFromStream(_stream);

                    if (packet.Length == 0)
                    {
                        return new SNIError(SNIProviders.NP_PROV, 0, SNICommon.ConnTerminatedError, string.Empty);
                    }
                }
                catch (ObjectDisposedException ode)
                {
                    return new SNIError(SNIProviders.NP_PROV, 0, ode);
                }
                catch (IOException ioe)
                {
                    return new SNIError(SNIProviders.NP_PROV, 0, ioe);
                }

                return null;
            }
        }

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
                    sniError = new SNIError(SNIProviders.NP_PROV, 0, ode);
                }
                catch (IOException ioe)
                {
                    sniError = new SNIError(SNIProviders.NP_PROV, 0, ioe);
                }
            }

            return true;
        }

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
                    return new SNIError(SNIProviders.NP_PROV, 0, ode);
                }
                catch (IOException ioe)
                {
                    return new SNIError(SNIProviders.NP_PROV, 0, ioe);
                }
            }
        }

        public override bool SendAsync(SNIPacket packet, SNIAsyncCallback callback, bool forceCallback, out SNIError error)
        {
            SNIPacket newPacket = packet;

            _writeTaskFactory.StartNew(() =>
            {
                try
                {
                    using (_debugLock.Acquire(this))
                    {
                        packet.WriteToStream(_stream);
                    }
                }
                catch (Exception e)
                {
                    SNIError internalError = new SNIError(SNIProviders.NP_PROV, SNICommon.InternalExceptionError, e);

                    if (callback != null)
                    {
                        callback(packet, internalError);
                    }
                    else
                    {
                        _sendCallback(packet, internalError);
                    }

                    return;
                }

                if (callback != null)
                {
                    callback(packet, null);
                }
                else
                {
                    _sendCallback(packet, null);
                }
            });

            error = null;
            return false;
        }

        public override void SetAsyncCallbacks(SNIAsyncCallback receiveCallback, SNIAsyncCallback sendCallback)
        {
            _receiveCallback = receiveCallback;
            _sendCallback = sendCallback;
        }

        public override SNIError EnableSsl(uint options)
        {
            _validateCert = (options & TdsEnums.SNI_SSL_VALIDATE_CERTIFICATE) != 0;

            try
            {
                _sslStream.AuthenticateAsClientAsync(_targetServer).GetAwaiter().GetResult();
                _sslOverTdsStream.FinishHandshake();
            }
            catch (AuthenticationException aue)
            {
                return new SNIError(SNIProviders.NP_PROV, SNICommon.InternalExceptionError, aue);
            }
            catch (InvalidOperationException ioe)
            {
                return new SNIError(SNIProviders.NP_PROV, SNICommon.InternalExceptionError, ioe);
            }

            _stream = _sslStream;
            return null;
        }

        public override void DisableSsl()
        {
            _sslStream.Dispose();
            _sslStream = null;
            _sslOverTdsStream.Dispose();
            _sslOverTdsStream = null;

            _stream = _pipeStream;
        }

        /// <summary>
        /// Validate server certificate
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="cert">X.509 certificate</param>
        /// <param name="chain">X.509 chain</param>
        /// <param name="policyErrors">Policy errors</param>
        /// <returns>true if valid</returns>
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
        public override void SetBufferSize(int bufferSize)
        {
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public override void KillConnection()
        {
            _pipeStream.Dispose();
            _pipeStream = null;
        }
    }
}