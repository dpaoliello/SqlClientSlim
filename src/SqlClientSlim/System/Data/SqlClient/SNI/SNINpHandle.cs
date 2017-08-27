using System.IO;
using System.IO.Pipes;
using System.Net.Security;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// Named Pipe connection handle
    /// </summary>
    internal sealed class SNINpHandle : SNITransportHandle
    {
        internal const string DefaultPipePath = @"sql\query"; // e.g. \\HOSTNAME\pipe\sql\query
        private const int MAX_PIPE_INSTANCES = 255;

        private readonly NamedPipeClientStream _pipeStream;

        protected override SNIProviders ProviderNumber => SNIProviders.NP_PROV;

        protected override Stream OriginalStream => _pipeStream;

        public SNINpHandle(string serverName, string pipeName, long timerExpire, out SNIError error)
            : base(serverName)
        {
            try
            {
                _pipeStream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.WriteThrough | PipeOptions.Asynchronous);

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
                error = new SNIError(SNIProviders.NP_PROV, SNIErrorCode.ConnTimeoutError, te);
                return;
            }
            catch(IOException ioe)
            {
                error = new SNIError(SNIProviders.NP_PROV, SNIErrorCode.ConnOpenFailedError, ioe);
                return;
            }

            if (!_pipeStream.IsConnected || !_pipeStream.CanWrite || !_pipeStream.CanRead)
            {
                error = new SNIError(SNIProviders.NP_PROV, 0, SNIErrorCode.ConnOpenFailedError, string.Empty);
                return;
            }

            _sslOverTdsStream = new SslOverTdsStream(_pipeStream);
            _sslStream = new SslStream(_sslOverTdsStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

            _stream = _pipeStream;
            error = null;
        }

        public override bool CheckConnection()
        {
            return (_stream.CanWrite && _stream.CanRead);
        }

        protected override void InternalDispose()
        {
            _pipeStream.Dispose();
        }

        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public override void KillConnection()
        {
            _pipeStream.Dispose();
        }
    }
}