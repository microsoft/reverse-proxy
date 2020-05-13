// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Delegates to a wrapped stream and calls its
    /// <see cref="Stream.Flush"/> or <see cref="Stream.FlushAsync(CancellationToken)"/>
    /// on every write.
    /// </summary>
    /// <remarks>
    /// This is used by <see cref="StreamCopyHttpContent"/> to work around some undesirable behavior
    /// in the HttpClient machinery (as of .NET Core 3.1.201) where an internal buffer
    /// doesn't get written to the outgoing socket stream when we write to the outgoing stream.
    /// Calling Flush on that stream sends the bytes to the underlying socket,
    /// but does not call flush on the socket, so perf impact is expected to be small.
    /// </remarks>
    internal sealed class AutoFlushingStream : Stream
    {
        private readonly Stream _stream;

        public AutoFlushingStream(Stream stream)
        {
            Contracts.CheckValue(stream, nameof(stream));
            _stream = stream;
        }

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => _stream.CanSeek;

        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _stream.Length;

        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            _stream.Flush();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _stream.WriteAsync(buffer, offset, count, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _stream.WriteAsync(buffer, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _stream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }
    }
}
