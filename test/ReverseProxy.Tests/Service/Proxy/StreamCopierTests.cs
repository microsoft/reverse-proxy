// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Common.Tests;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
{
    public class StreamCopierTests : TestAutoMockBase
    {
        [Fact]
        public async Task CopyAsync_Works()
        {
            const int SourceSize = (128 * 1024) - 3;
            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            await StreamCopier.CopyAsync(source, destination, CancellationToken.None);

            Assert.Equal(sourceBytes, destination.ToArray());
        }

        [Fact]
        public async Task SourceThrows_Reported()
        {
            var source = new ThrowStream();
            var destination = new MemoryStream();

            var (result, error) = await StreamCopier.CopyAsync(source, destination, CancellationToken.None);
            Assert.Equal(StreamCopyResult.InputError, result);
            Assert.IsAssignableFrom<IOException>(error);
        }

        [Fact]
        public async Task DestinationThrows_Reported()
        {
            var source = new MemoryStream(new byte[10]);
            var destination = new ThrowStream();

            var (result, error) = await StreamCopier.CopyAsync(source, destination, CancellationToken.None);
            Assert.Equal(StreamCopyResult.OutputError, result);
            Assert.IsAssignableFrom<IOException>(error);
        }

        [Fact]
        public async Task Cancelled_Reported()
        {
            var source = new MemoryStream(new byte[10]);
            var destination = new MemoryStream();

            var (result, error) = await StreamCopier.CopyAsync(source, destination, new CancellationToken(canceled: true));
            Assert.Equal(StreamCopyResult.Canceled, result);
            Assert.IsAssignableFrom<OperationCanceledException>(error);
        }

        private class ThrowStream : Stream
        {
            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                throw new IOException("Fake connection issue");
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new IOException("Fake connection issue");
            }
        }
    }
}
