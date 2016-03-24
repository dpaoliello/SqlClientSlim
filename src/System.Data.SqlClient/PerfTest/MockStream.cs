using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PerfTest
{
    /// <summary>
    /// A mocked read-only stream of a fixed size.
    /// </summary>
    public sealed class MockStream : Stream
    {
        private readonly int _totalBytes;
        private Task<int> _lastTask;

        public MockStream(int totalBytes)
        {
            _totalBytes = totalBytes;
            Position = 0;
            _lastTask = null;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _totalBytes;

        public override long Position { get; set; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = Math.Min(count, _totalBytes - (int)Position);
            Position += bytesRead;
            return bytesRead;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int bytesRead = Read(buffer, offset, count);
            if (_lastTask?.Result != bytesRead)
            {
                _lastTask = Task.FromResult(bytesRead);
            }
            return _lastTask;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
