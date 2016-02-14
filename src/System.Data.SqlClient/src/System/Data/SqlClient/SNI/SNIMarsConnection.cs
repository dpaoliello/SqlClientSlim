﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// SNI MARS connection. Multiple MARS streams will be overlayed on this connection.
    /// </summary>
    internal class SNIMarsConnection
    {
        private readonly Guid _connectionId = Guid.NewGuid();
        private readonly Dictionary<int, SNIMarsHandle> _sessions = new Dictionary<int, SNIMarsHandle>();
        private readonly byte[] _headerBytes = new byte[SNISMUXHeader.HEADER_LENGTH];

        private SNIHandle _lowerHandle;
        private ushort _nextSessionId = 0;
        private int _currentHeaderByteCount = 0;
        private int _dataBytesLeft = 0;
        private SNISMUXHeader _currentHeader;
        private SNIPacket _currentPacket;

        /// <summary>
        /// Connection ID
        /// </summary>
        public Guid ConnectionId
        {
            get
            {
                return _connectionId;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lowerHandle">Lower handle</param>
        public SNIMarsConnection(SNIHandle lowerHandle)
        {
            _lowerHandle = lowerHandle;
            _lowerHandle.SetAsyncCallbacks(HandleReceiveComplete, HandleSendComplete);
        }

        public SNIMarsHandle CreateSession(TdsParserStateObject callbackObject, out SNIError sniError)
        {
            lock (this)
            {
                ushort sessionId = _nextSessionId++;
                SNIMarsHandle handle = new SNIMarsHandle(this, sessionId, callbackObject, out sniError);
                _sessions.Add(sessionId, handle);
                return handle;
            }
        }

        /// <summary>
        /// Start receiving
        /// </summary>
        public void StartReceive()
        {
            SNIPacket packet = null;
            ReceiveAsync(ref packet);
        }

        /// <summary>
        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public SNIError Send(SNIPacket packet)
        {
            lock (this)
            {
                return _lowerHandle.Send(packet);
            }
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>True if completed synchronously, otherwise false</returns>
        public bool SendAsync(SNIPacket packet, SNIAsyncCallback callback, bool forceCallback, out SNIError sniError)
        {
            lock (this)
            {
                return _lowerHandle.SendAsync(packet, callback, forceCallback, out sniError);
            }
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>True if completed synchronous, otherwise false</returns>
        public void ReceiveAsync(ref SNIPacket packet)
        {
            lock (this)
            {
                SNIError sniError;
                bool completedSync = _lowerHandle.ReceiveAsync(true, ref packet, out sniError);
                Debug.Assert(!completedSync && (sniError == null), "Should not have completed sync");
            }
        }

        /// <summary>
        /// Check SNI handle connection
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>SNI error status</returns>
        public SNIError CheckConnection()
        {
            lock (this)
            {
                return _lowerHandle.CheckConnection();
            }
        }

        /// <summary>
        /// Process a receive error
        /// </summary>
        public void HandleReceiveError(SNIError sniError)
        {
            Debug.Assert(Monitor.IsEntered(this), "HandleReceiveError was called without being locked.");
            foreach (SNIMarsHandle handle in _sessions.Values)
            {
                handle.HandleReceiveError(new SNIPacket(handle), sniError);
            }
        }

        /// <summary>
        /// Process a send completion
        /// </summary>
        public void HandleSendComplete(SNIPacket packet, SNIError sniError)
        {
            packet.InvokeCompletionCallback(sniError);
        }

        /// <summary>
        /// Process a receive completion
        /// </summary>
        public void HandleReceiveComplete(SNIPacket packet, SNIError sniError)
        {
            SNISMUXHeader currentHeader = null;
            SNIPacket currentPacket = null;
            SNIMarsHandle currentSession = null;


            if (sniError != null)
            {
                lock (this)
                {
                    HandleReceiveError(sniError);
                    return;
                }
            }

            while (true)
            {
                lock (this)
                {
                    bool sessionRemoved = false;

                    if (_currentHeaderByteCount != SNISMUXHeader.HEADER_LENGTH)
                    {
                        currentHeader = null;
                        currentPacket = null;
                        currentSession = null;

                        while (_currentHeaderByteCount != SNISMUXHeader.HEADER_LENGTH)
                        {
                            int bytesTaken = packet.TakeData(_headerBytes, _currentHeaderByteCount, SNISMUXHeader.HEADER_LENGTH - _currentHeaderByteCount);
                            _currentHeaderByteCount += bytesTaken;

                            if (bytesTaken == 0)
                            {
                                ReceiveAsync(ref packet);
                                return;
                            }
                        }

                        _currentHeader = new SNISMUXHeader()
                        {
                            SMID = _headerBytes[0],
                            flags = _headerBytes[1],
                            sessionId = BitConverter.ToUInt16(_headerBytes, 2),
                            length = BitConverter.ToUInt32(_headerBytes, 4) - SNISMUXHeader.HEADER_LENGTH,
                            sequenceNumber = BitConverter.ToUInt32(_headerBytes, 8),
                            highwater = BitConverter.ToUInt32(_headerBytes, 12)
                        };

                        _dataBytesLeft = (int)_currentHeader.length;
                        _currentPacket = new SNIPacket(null);
                        _currentPacket.Allocate((int)_currentHeader.length);

                        if (_currentHeader.flags == (byte)SNISMUXFlags.SMUX_FIN)
                        {
                            _sessions.Remove(_currentHeader.sessionId);
                            sessionRemoved = true;
                        }
                    }

                    currentHeader = _currentHeader;
                    currentPacket = _currentPacket;

                    if (_currentHeader.flags == (byte)SNISMUXFlags.SMUX_DATA)
                    {
                        if (_dataBytesLeft > 0)
                        {
                            int length = packet.TakeData(_currentPacket, _dataBytesLeft);
                            _dataBytesLeft -= length;

                            if (_dataBytesLeft > 0)
                            {
                                ReceiveAsync(ref packet);
                                return;
                            }
                        }
                    }

                    _currentHeaderByteCount = 0;

                    if (!sessionRemoved && !_sessions.TryGetValue(_currentHeader.sessionId, out currentSession))
                    {
                        sniError = new SNIError(SNIProviders.TCP_PROV, 0, 0, "Packet for unknown MARS session received");
                        HandleReceiveError(sniError);
                        _lowerHandle.Dispose();
                        _lowerHandle = null;
                        return;
                    }
                }

                if (currentSession != null)
                {
                    if (currentHeader.flags == (byte)SNISMUXFlags.SMUX_DATA)
                    {
                        currentSession.HandleReceiveComplete(currentPacket, currentHeader);
                    }
                    else if (_currentHeader.flags == (byte)SNISMUXFlags.SMUX_ACK)
                    {
                        currentSession.HandleAck(currentHeader.highwater);
                    }
                }

                lock (this)
                {
                    if (packet.DataLeft == 0)
                    {
                        ReceiveAsync(ref packet);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public SNIError EnableSsl(uint options)
        {
            return _lowerHandle.EnableSsl(options);
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public void DisableSsl()
        {
            _lowerHandle.DisableSsl();
        }

#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public void KillConnection()
        {
            _lowerHandle.KillConnection();
        }
#endif
    }
}