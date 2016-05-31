// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// Managed SNI proxy implementation. Contains many SNI entry points used by SqlClient.
    /// </summary>
    internal class SNIProxy
    {
        public static readonly SNIProxy Singleton = new SNIProxy();

        /// <summary>
        /// Enable MARS support on a connection
        /// </summary>
        /// <param name="handle">Connection handle</param>
        /// <returns>SNI error code</returns>
        public SNIError EnableMars(SNIHandle handle)
        {
            return SNIMarsManager.Singleton.CreateMarsConnection(handle);
        }

        /// <summary>
        /// Enable SSL on a connection
        /// </summary>
        /// <param name="handle">Connection handle</param>
        /// <returns>SNI error code</returns>
        public SNIError EnableSsl(SNIHandle handle, uint options)
        {
            try
            {
                return handle.EnableSsl(options);
            }
            catch (Exception e)
            {
                return new SNIError(SNIProviders.SSL_PROV, SNICommon.HandshakeFailureError, e);
            }
        }

        /// <summary>
        /// Disable SSL on a connection
        /// </summary>
        /// <param name="handle">Connection handle</param>
        /// <returns>SNI error code</returns>
        public void DisableSsl(SNIHandle handle)
        {
            handle.DisableSsl();
        }

        /// <summary>
        /// Generate SSPI context
        /// </summary>
        /// <param name="handle">SNI connection handle</param>
        /// <param name="receivedBuff">Receive buffer</param>
        /// <param name="sendBuff">Send buffer</param>
        /// <param name="sendLength">Send length</param>
        /// <param name="serverName">Service Principal Name buffer</param>
        /// <returns>SNI error code</returns>
        public uint GenSspiClientContext(SNIHandle handle, byte[] receivedBuff, byte[] sendBuff, ref uint sendLength, byte[] serverName)
        {
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// Initialize SSPI
        /// </summary>
        /// <param name="maxLength">Max length of SSPI packet</param>
        /// <returns>SNI error code</returns>
        public uint InitializeSspiPackage(ref uint maxLength)
        {
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// Set connection buffer size
        /// </summary>
        /// <param name="handle">SNI handle</param>
        /// <param name="bufferSize">Buffer size</param>
        /// <returns>SNI error code</returns>
        public uint SetConnectionBufferSize(SNIHandle handle, uint bufferSize)
        {
            handle.SetBufferSize((int)bufferSize);
            return TdsEnums.SNI_SUCCESS;
        }

        /// <summary>
        /// Get packet data
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="inBuff">Buffer</param>
        /// <param name="dataSize">Data size</param>
        /// <returns>SNI error status</returns>
        public uint PacketGetData(SNIPacket packet, byte[] inBuff, ref uint dataSize)
        {
            int dataSizeInt = 0;
            packet.GetData(inBuff, ref dataSizeInt);
            dataSize = (uint)dataSizeInt;

            return TdsEnums.SNI_SUCCESS;
        }

        /// <summary>
        /// Read synchronously
        /// </summary>
        /// <param name="handle">SNI handle</param>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeout">Timeout</param>
        /// <returns>SNI error status</returns>
        public SNIError ReadSyncOverAsync(SNIHandle handle, ref SNIPacket packet, int timeout)
        {
            return handle.Receive(ref packet, timeout);
        }

        /// <summary>
        /// Get SNI connection ID
        /// </summary>
        /// <param name="handle">SNI handle</param>
        /// <param name="clientConnectionId">Client connection ID</param>
        /// <returns>SNI error status</returns>
        public uint GetConnectionId(SNIHandle handle, ref Guid clientConnectionId)
        {
            clientConnectionId = handle.ConnectionId;

            return TdsEnums.SNI_SUCCESS;
        }

        /// <summary>
        /// Send a packet
        /// </summary>
        /// <param name="handle">SNI handle</param>
        /// <param name="packet">SNI packet</param>
        /// <param name="sync">true if synchronous, false if asynchronous</param>
        /// <returns>True if completed synchronous, otherwise false</returns>
        public bool WritePacket(SNIHandle handle, SNIPacket packet, bool sync, out SNIError sniError)
        {
            if (sync)
            {
                sniError = handle.Send(packet.Clone());
                return true;
            }
            else
            {
                return handle.SendAsync(packet.Clone(), null, false, out sniError);
            }
        }

        /// <summary>
        /// Reset a packet
        /// </summary>
        /// <param name="handle">SNI handle</param>
        /// <param name="write">true if packet is for write</param>
        /// <param name="packet">SNI packet</param>
        public void PacketReset(SNIHandle handle, bool write, SNIPacket packet)
        {
            packet.Reset();
        }

        /// <summary>
        /// Create a SNI connection handle
        /// </summary>
        /// <param name="callbackObject">Asynchronous I/O callback object</param>
        /// <param name="fullServerName">Full server name from connection string</param>
        /// <param name="ignoreSniOpenTimeout">Ignore open timeout</param>
        /// <param name="timerExpire">Timer expiration</param>
        /// <param name="instanceName">Instance name</param>
        /// <param name="flushCache">Flush packet cache</param>
        /// <param name="parallel">Attempt parallel connects</param>
        /// <returns>SNI handle</returns>
        public SNIHandle CreateConnectionHandle(object callbackObject, string fullServerName, bool ignoreSniOpenTimeout, long timerExpire, out byte[] instanceName, bool flushCache, bool parallel, out SNIError sniError)
        {
            instanceName = new byte[1];
            instanceName[0] = 0;

            string[] serverNameParts = fullServerName.Split(':');

            if (serverNameParts.Length > 2)
            {
                sniError = new SNIError(SNIProviders.INVALID_PROV, 0, SNICommon.InvalidConnStringError, string.Empty);
                return null;
            }

            // Default to using tcp if no protocol is provided
            SNIHandle handle;
            if (serverNameParts.Length == 1)
            {
                handle = CreateTcpHandle(serverNameParts[0], timerExpire, callbackObject, parallel, out sniError);
            }
            else
            {
                switch (serverNameParts[0])
                {
                    case TdsEnums.TCP:
                        handle = CreateTcpHandle(serverNameParts[1], timerExpire, callbackObject, parallel, out sniError);
                        break;

                    case TdsEnums.NP:
                        handle = CreateNpHandle(serverNameParts[1], timerExpire, callbackObject, parallel, out sniError);
                        break;

                    default:
                        if (parallel)
                        {
                            sniError = new SNIError(SNIProviders.INVALID_PROV, 0, SNICommon.MultiSubnetFailoverWithNonTcpProtocol, string.Empty);
                        }
                        else
                        {
                            sniError = new SNIError(SNIProviders.INVALID_PROV, 0, SNICommon.ProtocolNotSupportedError, string.Empty);
                        }
                        handle = null;
                        break;
                }
            }

            if (sniError != null)
            {
                handle = null;
            }
            return handle;
        }

        /// <summary>
        /// Creates an SNITCPHandle object
        /// </summary>
        /// <param name="fullServerName">Server string. May contain a comma delimited port number.</param>
        /// <param name="timerExpire">Timer expiration</param>
        /// <param name="callbackObject">Asynchronous I/O callback object</param>
        /// <param name="parallel">Should MultiSubnetFailover be used</param>
        /// <returns>SNITCPHandle</returns>
        private SNITCPHandle CreateTcpHandle(string fullServerName, long timerExpire, object callbackObject, bool parallel, out SNIError sniError)
        {
            // TCP Format: 
            // tcp:<host name>\<instance name>
            // tcp:<host name>,<TCP/IP port number>
            int portNumber = 1433;
            string[] serverAndPortParts = fullServerName.Split(',');

            if (serverAndPortParts.Length == 2)
            {
                try
                {
                    portNumber = ushort.Parse(serverAndPortParts[1]);
                }
                catch (Exception e)
                {
                    sniError = new SNIError(SNIProviders.TCP_PROV, SNICommon.InvalidConnStringError, e);
                    return null;
                }
            }
            else if (serverAndPortParts.Length > 2)
            {
                sniError = new SNIError(SNIProviders.TCP_PROV, 0, SNICommon.InvalidConnStringError, string.Empty);
                return null;
            }

            return new SNITCPHandle(serverAndPortParts[0], portNumber, timerExpire, callbackObject, parallel, out sniError);
        }

        /// <summary>
        /// Creates an SNINpHandle object
        /// </summary>
        /// <param name="fullServerName">Server string representing a UNC pipe path.</param>
        /// <param name="timerExpire">Timer expiration</param>
        /// <param name="callbackObject">Asynchronous I/O callback object</param>
        /// <param name="parallel">Should MultiSubnetFailover be used. Only returns an error for named pipes.</param>
        /// <returns>SNINpHandle</returns>
        private SNINpHandle CreateNpHandle(string fullServerName, long timerExpire, object callbackObject, bool parallel, out SNIError sniError)
        {
            if (parallel)
            {
                sniError = new SNIError(SNIProviders.NP_PROV, 0, SNICommon.MultiSubnetFailoverWithNonTcpProtocol, string.Empty);
                return null;
            }

            if(fullServerName.Length == 0 || fullServerName.Contains("/")) // Pipe paths only allow back slashes
            {
                sniError = new SNIError(SNIProviders.NP_PROV, 0, SNICommon.InvalidConnStringError, string.Empty);
                return null;
            }

            string serverName, pipeName;
            if (!fullServerName.Contains(@"\"))
            {
                serverName = fullServerName;
                pipeName = SNINpHandle.DefaultPipePath;
            }
            else
            {
                try
                {
                    Uri pipeURI = new Uri(fullServerName);
                    string resourcePath = pipeURI.AbsolutePath;

                    string pipeToken = "/pipe/";
                    if (!resourcePath.StartsWith(pipeToken))
                    {
                        sniError = new SNIError(SNIProviders.NP_PROV, 0, SNICommon.InvalidConnStringError, string.Empty);
                        return null;
                    }
                    pipeName = resourcePath.Substring(pipeToken.Length);
                    serverName = pipeURI.Host;
                }
                catch(UriFormatException)
                {
                    sniError = new SNIError(SNIProviders.NP_PROV, 0, SNICommon.InvalidConnStringError, string.Empty);
                    return null;
                }
            }

            return new SNINpHandle(serverName, pipeName, timerExpire, callbackObject, out sniError);
        }

        /// <summary>
        /// Create MARS handle
        /// </summary>
        /// <param name="callbackObject">Asynchronous I/O callback object</param>
        /// <param name="physicalConnection">SNI connection handle</param>
        /// <param name="defaultBufferSize">Default buffer size</param>
        /// <param name="async">Asynchronous connection</param>
        /// <returns>SNI error status</returns>
        public SNIHandle CreateMarsHandle(TdsParserStateObject callbackObject, SNIHandle physicalConnection, int defaultBufferSize, out SNIError sniError)
        {
            SNIMarsConnection connection = SNIMarsManager.Singleton.GetConnection(physicalConnection);
            return connection.CreateSession(callbackObject, out sniError);
        }

        /// <summary>
        /// Read packet asynchronously
        /// </summary>
        public bool ReadAsync(SNIHandle handle, ref SNIPacket packet, out SNIError sniError)
        {
            packet = new SNIPacket(null);

            return handle.ReceiveAsync(false, ref packet, out sniError);
        }

        /// <summary>
        /// Set packet data
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="data">Data</param>
        /// <param name="length">Length</param>
        public void PacketSetData(SNIPacket packet, byte[] data, int length)
        {
            packet.SetData(data, length);
        }
    }
}
