// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// SNI Packet
    /// </summary>
    internal class SNIPacket : IEquatable<SNIPacket>
    {
        private byte[] _data;
        private int _length;
        private int _capacity;
        private int _offset;
#if DEBUG
        private string _description;
#endif
        private SNIAsyncCallback _completionCallback;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="handle">Owning SNI handle</param>
        public SNIPacket(SNIHandle handle)
        {
            _offset = 0;
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
        /// Data left to process
        /// </summary>
        public int DataLeft
        {
            get
            {
                return _length - _offset;
            }
        }

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
        /// Packet validity
        /// </summary>
        public bool IsInvalid
        {
            get
            {
                return _data == null;
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
            if (_capacity != capacity)
            {
                _capacity = capacity;
                _data = new byte[capacity];
            }
        }

        /// <summary>
        /// Clone packet
        /// </summary>
        /// <returns>Cloned packet</returns>
        public SNIPacket Clone()
        {
            SNIPacket packet = new SNIPacket(null);
            packet._data = new byte[_length];
            Buffer.BlockCopy(_data, 0, packet._data, 0, _length);
            packet._length = _length;

            return packet;
        }

        /// <summary>
        /// Get packet data
        /// </summary>
        /// <param name="inBuff">Buffer</param>
        /// <param name="dataSize">Data in packet</param>
        public void GetData(byte[] buffer, ref int dataSize)
        {
            Buffer.BlockCopy(_data, 0, buffer, 0, _length);
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
            _capacity = length;
            _offset = 0;
        }

        /// <summary>
        /// Take data from another packet
        /// </summary>
        /// <param name="packet">Packet</param>
        /// <param name="size">Data to take</param>
        /// <returns>Amount of data taken</returns>
        public int TakeData(SNIPacket packet, int size)
        {
            int dataSize = TakeData(packet._data, packet._length, size);
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
        /// <param name="dataOffset">Data offset</param>
        /// <param name="size">Size</param>
        /// <returns></returns>
        public int TakeData(byte[] buffer, int dataOffset, int size)
        {
            if (_offset >= _length)
            {
                return 0;
            }

            if (_offset + size > _length)
            {
                size = _length - _offset;
            }

            Buffer.BlockCopy(_data, _offset, buffer, dataOffset, size);
            _offset = _offset + size;
            return size;
        }

        /// <summary>
        /// Reset packet 
        /// </summary>
        public void Reset()
        {
            _length = 0;
            Array.Clear(_data, 0, _capacity);
        }

        /// <summary>
        /// Read data from a stream asynchronously
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <param name="callback">Completion callback</param>
        public void ReadFromStreamAsync(Stream stream, SNIAsyncCallback callback)
        {
            stream.ReadAsync(_data, 0, _capacity).ContinueWith(
                ReadFromStreamAsyncContinuation,
                callback,
                CancellationToken.None,
                TaskContinuationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        private void ReadFromStreamAsyncContinuation(Task<int> t, object state)
        {
            SNIAsyncCallback callback = (SNIAsyncCallback)state;
            SNIError error = null;

            Exception e = t.Exception != null ? t.Exception.InnerException : null;
            if (e != null)
            {
                error = new SNIError(SNIProviders.TCP_PROV, 0, 0, e.Message);
            }
            else
            {
                _length = t.Result;

                if (_length == 0)
                {
                    error = new SNIError(SNIProviders.TCP_PROV, 0, 0, "Connection was terminated");
                }
            }

            callback(this, error);
        }

        /// <summary>
        /// Read data from a stream synchronously
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public void ReadFromStream(Stream stream)
        {
            _length = stream.Read(_data, 0, _capacity);
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

        /// <summary>
        /// Get hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Check packet equality
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>true if equal</returns>
        public override bool Equals(object obj)
        {
            SNIPacket packet = obj as SNIPacket;

            if (packet != null)
            {
                return Equals(packet);
            }

            return false;
        }

        /// <summary>
        /// Check packet equality
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>true if equal</returns>
        public bool Equals(SNIPacket packet)
        {
            if (packet != null)
            {
                return object.ReferenceEquals(packet, this);
            }

            return false;
        }
    }
}