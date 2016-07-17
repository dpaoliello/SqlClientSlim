using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
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
    }
}
