// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Client;

public class LineSeparatedHttpContent : HttpContent
{
    private readonly HttpContent _originContent;
    private readonly CancellationToken _cancellationToken;
    private Stream _originStream;

    public LineSeparatedHttpContent(HttpContent originContent, CancellationToken cancellationToken)
    {
        _originContent = originContent;
        _cancellationToken = cancellationToken;
    }

    public TextReader StreamReader { get; private set; }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        _originStream = await _originContent.ReadAsStreamAsync().ConfigureAwait(false);

        var reader = new PeekableStreamReader(new CancelableStream(_originStream, _cancellationToken));
        StreamReader = reader;

        var firstLine = await reader.PeekLineAsync().ConfigureAwait(false);

        var writer = new StreamWriter(stream);

        await writer.WriteAsync(firstLine).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    internal class CancelableStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly CancellationToken _cancellationToken;

        public CancelableStream(Stream innerStream, CancellationToken cancellationToken)
        {
            _innerStream = innerStream;
            _cancellationToken = cancellationToken;
        }

        public override void Flush() =>
            _innerStream.FlushAsync(_cancellationToken).GetAwaiter().GetResult();

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            using (var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken))
            {
                await _innerStream.FlushAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            }
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _innerStream.ReadAsync(buffer, offset, count, _cancellationToken).GetAwaiter().GetResult();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            using (var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken))
            {
                return await _innerStream.ReadAsync(buffer, offset, count, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

        public override void SetLength(long value) => _innerStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _innerStream.WriteAsync(buffer, offset, count, _cancellationToken).GetAwaiter().GetResult();

        public override async Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            using (var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken))
            {
                await _innerStream.WriteAsync(buffer, offset, count, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }
        }

        public override bool CanRead => _innerStream.CanRead;

        public override bool CanSeek => _innerStream.CanSeek;

        public override bool CanWrite => _innerStream.CanWrite;

        public override long Length => _innerStream.Length;

        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        private LinkedCancellationTokenSource CreateCancellationTokenSource(CancellationToken userCancellationToken)
        {
            return new LinkedCancellationTokenSource(_cancellationToken, userCancellationToken);
        }

        private readonly struct LinkedCancellationTokenSource : IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource;

            public LinkedCancellationTokenSource(CancellationToken token1, CancellationToken token2)
            {
                if (token1.CanBeCanceled && token2.CanBeCanceled)
                {
                    _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token1, token2);
                    Token = _cancellationTokenSource.Token;
                }
                else
                {
                    _cancellationTokenSource = null;
                    Token = token1.CanBeCanceled ? token1 : token2;
                }
            }

            public CancellationToken Token { get; }

            public void Dispose()
            {
                _cancellationTokenSource?.Dispose();
            }
        }
    }

    internal class PeekableStreamReader : TextReader
    {
        private readonly Queue<string> _buffer;
        private readonly StreamReader _inner;

        public PeekableStreamReader(Stream stream)
        {
            _buffer = new Queue<string>();
            _inner = new StreamReader(stream);
        }

        public override string ReadLine() => throw new NotImplementedException();

        public override Task<string> ReadLineAsync()
        {
            if (_buffer.Count > 0)
            {
                return Task.FromResult(_buffer.Dequeue());
            }

            return _inner.ReadLineAsync();
        }

        public async Task<string> PeekLineAsync()
        {
            var line = await ReadLineAsync().ConfigureAwait(false);
            _buffer.Enqueue(line);
            return line;
        }

        public override int Read() => throw new NotImplementedException();

        public override int Read(char[] buffer, int index, int count) => throw new NotImplementedException();

        public override Task<int> ReadAsync(char[] buffer, int index, int count) =>
            throw new NotImplementedException();

        public override int ReadBlock(char[] buffer, int index, int count) => throw new NotImplementedException();

        public override Task<int> ReadBlockAsync(char[] buffer, int index, int count) =>
            throw new NotImplementedException();

        public override string ReadToEnd() => throw new NotImplementedException();

        public override Task<string> ReadToEndAsync() => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
