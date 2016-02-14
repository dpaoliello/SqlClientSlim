// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// MARS handle
    /// </summary>
    internal class SNIMarsHandle : SNIHandle
    {
        private const uint ACK_THRESHOLD = 2;

        private readonly SNIMarsConnection _connection;
        private readonly Queue<SNIPacket> _receivedPacketQueue = new Queue<SNIPacket>();
        private readonly Queue<SNIMarsQueuedPacket> _sendPacketQueue = new Queue<SNIMarsQueuedPacket>();
        private readonly TdsParserStateObject _callbackObject;
        private readonly Guid _connectionId = Guid.NewGuid();
        private readonly ushort _sessionId;
        private readonly ManualResetEventSlim _packetEvent = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _ackEvent = new ManualResetEventSlim(false);
        private readonly SNISMUXHeader _currentHeader = new SNISMUXHeader();

        private uint _sendHighwater = 4;
        private int _asyncReceives = 0;
        private uint _receiveHighwater = 4;
        private uint _receiveHighwaterLastAck = 4;
        private uint _sequenceNumber;
        private SNIError _connectionError;

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
        /// Dispose object
        /// </summary>
        public override void Dispose()
        {
            SendControlPacket(SNISMUXFlags.SMUX_FIN, false);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connection">MARS connection</param>
        /// <param name="sessionId">MARS session ID</param>
        /// <param name="callbackObject">Callback object</param>
        /// <param name="async">true if connection is asynchronous</param>
        public SNIMarsHandle(SNIMarsConnection connection, ushort sessionId, TdsParserStateObject callbackObject, out SNIError sniError)
        {
            _sessionId = sessionId;
            _connection = connection;
            _callbackObject = callbackObject;
            sniError = SendControlPacket(SNISMUXFlags.SMUX_SYN, true);
        }

        /// <summary>
        /// Send control packet
        /// </summary>
        /// <param name="flags">SMUX header flags</param>
        /// <param name="async">true if packet should be sent asynchronously</param>
        /// <returns>True if completed successfully, otherwise false</returns>
        private SNIError SendControlPacket(SNISMUXFlags flags, bool async)
        {
            byte[] headerBytes = null;

            lock (this)
            {
                GetSMUXHeaderBytes(0, (byte)flags, ref headerBytes);
            }

            SNIPacket packet = new SNIPacket(null);
            packet.SetData(headerBytes, SNISMUXHeader.HEADER_LENGTH);

            if (async)
            {
                SNIError sniError;
                _connection.SendAsync(packet, (sentPacket, error) => { }, false, out sniError);
                return sniError;
            }
            else
            {
                return _connection.Send(packet);
            }
        }

        /// <summary>
        /// Generate SMUX header 
        /// </summary>
        /// <param name="length">Packet length</param>
        /// <param name="flags">Packet flags</param>
        /// <param name="headerBytes">Header in bytes</param>
        private void GetSMUXHeaderBytes(int length, byte flags, ref byte[] headerBytes)
        {
            headerBytes = new byte[SNISMUXHeader.HEADER_LENGTH];

            _currentHeader.SMID = 83;
            _currentHeader.flags = flags;
            _currentHeader.sessionId = _sessionId;
            _currentHeader.length = (uint)SNISMUXHeader.HEADER_LENGTH + (uint)length;
            _currentHeader.sequenceNumber = ((flags == (byte)SNISMUXFlags.SMUX_FIN) || (flags == (byte)SNISMUXFlags.SMUX_ACK)) ? _sequenceNumber - 1 : _sequenceNumber++;
            _currentHeader.highwater = _receiveHighwater;
            _receiveHighwaterLastAck = _currentHeader.highwater;

            BitConverter.GetBytes(_currentHeader.SMID).CopyTo(headerBytes, 0);
            BitConverter.GetBytes(_currentHeader.flags).CopyTo(headerBytes, 1);
            BitConverter.GetBytes(_currentHeader.sessionId).CopyTo(headerBytes, 2);
            BitConverter.GetBytes(_currentHeader.length).CopyTo(headerBytes, 4);
            BitConverter.GetBytes(_currentHeader.sequenceNumber).CopyTo(headerBytes, 8);
            BitConverter.GetBytes(_currentHeader.highwater).CopyTo(headerBytes, 12);
        }

        /// <summary>
        /// Generate a packet with SMUX header
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>Encapsulated SNI packet</returns>
        private SNIPacket GetSMUXEncapsulatedPacket(SNIPacket packet)
        {
            uint xSequenceNumber = _sequenceNumber;
            byte[] headerBytes = null;
            GetSMUXHeaderBytes(packet.Length, (byte)SNISMUXFlags.SMUX_DATA, ref headerBytes);

            SNIPacket smuxPacket = new SNIPacket(null);
#if DEBUG
            smuxPacket.Description = string.Format("({0}) SMUX packet {1}", packet.Description == null ? "" : packet.Description, xSequenceNumber);
#endif
            smuxPacket.Allocate(16 + packet.Length);
            smuxPacket.AppendData(headerBytes, 16);
            smuxPacket.AppendPacket(packet);

            return smuxPacket;
        }

        /// Send a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <returns>SNI error code</returns>
        public override SNIError Send(SNIPacket packet)
        {
            while (true)
            {
                lock (this)
                {
                    if (_sequenceNumber < _sendHighwater)
                    {
                        break;
                    }
                }

                _ackEvent.Wait();

                lock (this)
                {
                    _ackEvent.Reset();
                }
            }

            return _connection.Send(GetSMUXEncapsulatedPacket(packet));
        }

        /// <summary>
        /// Send packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>True if completed successfully, otherwise false</returns>
        private bool InternalSendAsync(SNIPacket packet, SNIAsyncCallback callback)
        {
            SNIPacket encapsulatedPacket = null;

            lock (this)
            {
                if (_sequenceNumber >= _sendHighwater)
                {
                    return false;
                }

                encapsulatedPacket = GetSMUXEncapsulatedPacket(packet);

                if (callback != null)
                {
                    encapsulatedPacket.SetCompletionCallback(callback);
                }
                else
                {
                    encapsulatedPacket.SetCompletionCallback(HandleSendComplete);
                }

                SNIError sniError;
                bool completedSync = _connection.SendAsync(encapsulatedPacket, callback, true, out sniError);
                Debug.Assert(!completedSync && (sniError == null), "Should not have completed synchronously");
                return true;
            }
        }

        /// <summary>
        /// Send pending packets
        /// </summary>
        /// <returns>True if all packets finished sending sync or an error occurred, otherwise false</returns>
        private void SendPendingPackets()
        {
            SNIMarsQueuedPacket packet = null;

            while (true)
            {
                lock (this)
                {
                    if (_sequenceNumber < _sendHighwater)
                    {
                        if (_sendPacketQueue.Count != 0)
                        {
                            packet = _sendPacketQueue.Peek();
                            if (!InternalSendAsync(packet.Packet, packet.Callback))
                            {
                                return;
                            }

                            _sendPacketQueue.Dequeue();
                            continue;
                        }
                        else
                        {
                            _ackEvent.Set();
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Send a packet asynchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        /// <returns>SNI error code</returns>
        public override bool SendAsync(SNIPacket packet, SNIAsyncCallback callback, bool forceCallback, out SNIError sniError)
        {
            lock (this)
            {
                _sendPacketQueue.Enqueue(new SNIMarsQueuedPacket(packet, callback != null ? callback : HandleSendComplete));
            }

            SendPendingPackets();
            sniError = null;
            return false;
        }

        /// <summary>
        /// Receive a packet asynchronously
        /// </summary>
        /// <returns>True if completed synchronously, otherwise false</returns>
        public override bool ReceiveAsync(bool forceCallback, ref SNIPacket packet, out SNIError sniError)
        {
            lock (_receivedPacketQueue)
            {
                int queueCount = _receivedPacketQueue.Count;

                if (_connectionError != null)
                {
                    sniError = _connectionError;
                    return true;
                }

                if (queueCount == 0)
                {
                    _asyncReceives++;
                    sniError = null;
                    return false;
                }

                packet = _receivedPacketQueue.Dequeue();

                if (queueCount == 1)
                {
                    _packetEvent.Reset();
                }
            }

            lock (this)
            {
                _receiveHighwater++;
            }

            sniError = SendAckIfNecessary();
            return true;
        }

        /// <summary>
        /// Handle receive error
        /// </summary>
        public void HandleReceiveError(SNIPacket packet, SNIError sniError)
        {
            lock (_receivedPacketQueue)
            {
                _connectionError = sniError;
                _packetEvent.Set();
            }

            _callbackObject.ReadAsyncCallback(packet, _connectionError);
        }

        /// <summary>
        /// Handle send completion
        /// </summary>
        public void HandleSendComplete(SNIPacket packet, SNIError sniError)
        {
            lock (this)
            {
                Debug.Assert(_callbackObject != null);

                _callbackObject.WriteAsyncCallback(packet, sniError);
            }
        }

        /// <summary>
        /// Handle SMUX acknowledgement
        /// </summary>
        /// <param name="highwater">Send highwater mark</param>
        public void HandleAck(uint highwater)
        {
            lock (this)
            {
                if (_sendHighwater != highwater)
                {
                    _sendHighwater = highwater;
                    SendPendingPackets();
                }
            }
        }

        /// <summary>
        /// Handle receive completion
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="header">SMUX header</param>
        public void HandleReceiveComplete(SNIPacket packet, SNISMUXHeader header)
        {
            lock (this)
            {
                if (_sendHighwater != header.highwater)
                {
                    HandleAck(header.highwater);
                }

                lock (_receivedPacketQueue)
                {
                    if (_asyncReceives == 0)
                    {
                        _receivedPacketQueue.Enqueue(packet);
                        _packetEvent.Set();
                        return;
                    }

                    _asyncReceives--;

                    _callbackObject.ReadAsyncCallback(packet, null);
                }
            }

            lock (this)
            {
                _receiveHighwater++;
            }

            SendAckIfNecessary();
        }

        /// <summary>
        /// Send ACK if we've hit highwater threshold
        /// </summary>
        private SNIError SendAckIfNecessary()
        {
            uint receiveHighwater;
            uint receiveHighwaterLastAck;

            lock (this)
            {
                receiveHighwater = _receiveHighwater;
                receiveHighwaterLastAck = _receiveHighwaterLastAck;
            }

            if (receiveHighwater - receiveHighwaterLastAck > ACK_THRESHOLD)
            {
                return SendControlPacket(SNISMUXFlags.SMUX_ACK, true);
            }
            return null;
        }

        /// <summary>
        /// Receive a packet synchronously
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="timeout">Timeout</param>
        /// <returns>SNI error code</returns>
        public override SNIError Receive(ref SNIPacket packet, int timeout)
        {
            int queueCount;
            uint result = TdsEnums.SNI_SUCCESS_IO_PENDING;

            while (true)
            {
                lock (_receivedPacketQueue)
                {
                    if (_connectionError != null)
                    {
                        return _connectionError;
                    }

                    queueCount = _receivedPacketQueue.Count;

                    if (queueCount > 0)
                    {
                        packet = _receivedPacketQueue.Dequeue();

                        if (queueCount == 1)
                        {
                            _packetEvent.Reset();
                        }

                        result = TdsEnums.SNI_SUCCESS;
                    }
                }

                if (result == TdsEnums.SNI_SUCCESS)
                {
                    lock (this)
                    {
                        _receiveHighwater++;
                    }

                    return SendAckIfNecessary();
                }

                if (!_packetEvent.Wait(timeout))
                {
                    return new SNIError(SNIProviders.TCP_PROV, 0, 0, "Timeout error");
                }
            }
        }

        /// <summary>
        /// Check SNI handle connection
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>SNI error status</returns>
        public override SNIError CheckConnection()
        {
            return _connection.CheckConnection();
        }

        /// <summary>
        /// Set async callbacks
        /// </summary>
        /// <param name="receiveCallback">Receive callback</param>
        /// <param name="sendCallback">Send callback</param>
        public override void SetAsyncCallbacks(SNIAsyncCallback receiveCallback, SNIAsyncCallback sendCallback)
        {
        }

        /// <summary>
        /// Enable SSL
        /// </summary>
        public override SNIError EnableSsl(uint options)
        {
            return _connection.EnableSsl(options);
        }

        /// <summary>
        /// Disable SSL
        /// </summary>
        public override void DisableSsl()
        {
            _connection.DisableSsl();
        }

#if DEBUG
        /// <summary>
        /// Test handle for killing underlying connection
        /// </summary>
        public override void KillConnection()
        {
            _connection.KillConnection();
        }
#endif
    }
}