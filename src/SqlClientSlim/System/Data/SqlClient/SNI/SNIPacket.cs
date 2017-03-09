// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// SNI Packet
    /// </summary>
    internal sealed class SNIPacket
    {
        private byte[] _data;
        private int _length;
#if DEBUG
        private string _description;
#endif
        private SNIAsyncCallback _completionCallback;

        /// <summary>
        /// Constructor
        /// </summary>
        public SNIPacket()
        {
        }

        public SNIPacket(byte[] buffer)
        {
            _data = buffer;
        }

        public SNIPacket(byte[] buffer, int length)
        {
            _data = buffer;
            _length = length;
        }

#if DEBUG
        /// <summary>
        /// Packet description (used for debugging)
        /// </summary>
        public string Description
        {
            get
            {
                return _description;
            }

            set
            {
                _description = value;
            }
        }
#endif

        /// <summary>
        /// Length of data
        /// </summary>
        public int Length
        {
            get
            {
                return _length;
            }
        }

        /// <summary>
        /// Set async completion callback
        /// </summary>
        /// <param name="completionCallback">Completion callback</param>
        public void SetCompletionCallback(SNIAsyncCallback completionCallback)
        {
            _completionCallback = completionCallback;
        }

        /// <summary>
        /// Invoke the completion callback 
        /// </summary>
        /// <param name="sniError">SNI error</param>
        public void InvokeCompletionCallback(SNIError sniError)
        {
            _completionCallback(this, sniError);
        }

        /// <summary>
        /// Allocate space for data
        /// </summary>
        /// <param name="capacity">Bytes to allocate</param>
        public void Allocate(int capacity)
        {
            if ((_data == null) || (_data.Length < capacity))
            {
                _data = new byte[capacity];
            }
        }

        /// <summary>
        /// Get packet data
        /// </summary>
        /// <param name="inBuff">Buffer</param>
        /// <param name="dataSize">Data in packet</param>
        public void GetData(byte[] buffer, ref int dataSize)
        {
            if (_data != buffer)
            {
                Buffer.BlockCopy(_data, 0, buffer, 0, _length);
            }
            dataSize = _length;
        }

        /// <summary>
        /// Set packet data
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="length">Length</param>
        public void SetData(byte[] data, int length)
        {
            _data = data;
            _length = length;
        }

        /// <summary>
        /// Take data from another packet
        /// </summary>
        /// <param name="packet">Packet</param>
        /// <param name="size">Data to take</param>
        /// <returns>Amount of data taken</returns>
        public int TakeData(int offset, SNIPacket packet, int size)
        {
            int dataSize = TakeData(offset, packet._data, packet._length, size);
            packet._length += dataSize;
            return dataSize;
        }

        /// <summary>
        /// Append data
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="size">Size</param>
        public void AppendData(byte[] data, int size)
        {
            Buffer.BlockCopy(data, 0, _data, _length, size);
            _length += size;
        }

        /// <summary>
        /// Append another packet
        /// </summary>
        /// <param name="packet">Packet</param>
        public void AppendPacket(SNIPacket packet)
        {
            Buffer.BlockCopy(packet._data, 0, _data, _length, packet._length);
            _length += packet._length;
        }

        /// <summary>
        /// Take data from packet and advance offset
        /// </summary>
        /// <param name="buffer">Buffer</param>
        /// <param name="bufferOffset">Data offset</param>
        /// <param name="size">Size</param>
        /// <returns></returns>
        public int TakeData(int packetOffset, byte[] buffer, int bufferOffset, int size)
        {
            if (packetOffset >= _length)
            {
                return 0;
            }

            if (packetOffset + size > _length)
            {
                size = _length - packetOffset;
            }

            Buffer.BlockCopy(_data, packetOffset, buffer, bufferOffset, size);
            return size;
        }

        /// <summary>
        /// Read data from a stream asynchronously
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <param name="callback">Completion callback</param>
        public void ReadFromStreamAsync(Stream stream, SNIAsyncCallback callback)
        {
            _completionCallback = callback;
            stream.ReadAsync(_data, 0, _data.Length).ContinueWith(
                ReadFromStreamAsyncContinuation,
                this,
                CancellationToken.None,
                TaskContinuationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        private static void ReadFromStreamAsyncContinuation(Task<int> t, object state)
        {
            SNIPacket packet = (SNIPacket)state;
            SNIError error = null;

            Exception e = t.Exception?.InnerException;
            if (e != null)
            {
                error = new SNIError(SNIProviders.TCP_PROV, 0, 0, e.Message);
            }
            else
            {
                packet._length = t.Result;

                if (packet._length == 0)
                {
                    error = new SNIError(SNIProviders.TCP_PROV, 0, SNICommon.ConnTerminatedError, string.Empty);
                }
            }

            SNIAsyncCallback callback = packet._completionCallback;
            packet._completionCallback = null;
            callback(packet, error);
        }

        /// <summary>
        /// Read data from a stream synchronously
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public void ReadFromStream(Stream stream)
        {
            _length = stream.Read(_data, 0, _data.Length);
        }

        /// <summary>
        /// Write data to a stream synchronously
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        public void WriteToStream(Stream stream)
        {
            stream.Write(_data, 0, _length);
        }

        /// <summary>
        /// Write data to a stream asynchronously
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        public Task WriteToStreamAsync(Stream stream)
        {
            return stream.WriteAsync(_data, 0, _length);
        }
    }
}
