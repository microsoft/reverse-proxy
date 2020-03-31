// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Service.Proxy
{
    /// <summary>
    /// Custom <see cref="HttpContent"/>
    /// used to proxy the incoming request body to the upstream server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By implementing a custom <see cref="HttpContent"/>, we are able to execute
    /// our custom code for all HTTP protocol versions.
    /// See the remarks section of <see cref="SerializeToStreamAsync(Stream, TransportContext)"/>
    /// for more details.
    /// </para>
    /// <para>
    /// <see cref="HttpContent"/> declares an internal property `AllowDuplex`
    /// which, when set to true, causes <see cref="HttpClient"/> and friends
    /// to NOT tie up the request body stream operations to the same cancellation token
    /// that is passed to <see cref="HttpClient.SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// When proxying duplex channels such as HTTP/2, gRPC,
    /// we need `HttpContent.AllowDuplex` to be true.
    /// It so happens to be by default on .NET Core 3.1. Should that ever change,
    /// this class will need to be updated.
    /// </para>
    /// </remarks>
    internal class StreamCopyHttpContent : HttpContent
    {
        private readonly Stream _source;
        private readonly IStreamCopier _streamCopier;
        private readonly CancellationToken _cancellation;
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public StreamCopyHttpContent(Stream source, IStreamCopier streamCopier, CancellationToken cancellation)
        {
            Contracts.CheckValue(source, nameof(source));
            Contracts.CheckValue(streamCopier, nameof(streamCopier));

            _source = source;
            _streamCopier = streamCopier;
            _cancellation = cancellation;
        }

        /// <summary>
        /// Gets a <see cref="System.Threading.Tasks.Task"/> that completes in successful or failed state
        /// mimicking the result of <see cref="SerializeToStreamAsync"/>.
        /// </summary>
        public Task ConsumptionTask => _tcs.Task;

        /// <summary>
        /// Gets a value indicating whether consumption of this content has begun.
        /// Property <see cref="ConsumptionTask"/> can be used to track the asynchronous outcome of the operation.
        /// </summary>
        /// <remarks>
        /// When used as an outgoing request content with <see cref="HttpClient"/>,
        /// this should always be true by the time the task returned by
        /// <see cref="HttpClient.SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)"/>
        /// completes, even when using <see cref="HttpCompletionOption.ResponseHeadersRead"/>.
        /// </remarks>
        public bool Started { get; private set; }

        /// <summary>
        /// Copies bytes from the stream provided in our constructor into the target <paramref name="stream"/>.
        /// </summary>
        /// <remarks>
        /// This is used internally by HttpClient.SendAsync to send the request body.
        /// Here's the sequence of events as of commit 17300169760c61a90cab8d913636c1058a30a8c1 (https://github.com/dotnet/corefx -- tag v3.1.1).
        ///
        /// <code>
        /// HttpClient.SendAsync -->
        /// HttpMessageInvoker.SendAsync -->
        /// HttpClientHandler.SendAsync -->
        /// SocketsHttpHandler.SendAsync -->
        /// HttpConnectionHandler.SendAsync -->
        /// HttpConnectionPoolManager.SendAsync -->
        /// HttpConnectionPool.SendAsync --> ... -->
        /// {
        ///     HTTP/1.1: HttpConnection.SendAsync -->
        ///               HttpConnection.SendAsyncCore -->
        ///               HttpConnection.SendRequestContentAsync -->
        ///               HttpContent.CopyToAsync
        ///
        ///     HTTP/2:   Http2Connection.SendAsync -->
        ///               Http2Stream.SendRequestBodyAsync -->
        ///               HttpContent.CopyToAsync
        ///
        ///     /* Only in .NET 5:
        ///     HTTP/3:   Http3Connection.SendAsync -->
        ///               Http3Connection.SendWithoutWaitingAsync -->
        ///               Http3RequestStream.SendAsync -->
        ///               Http3RequestStream.SendContentAsync -->
        ///               HttpContent.CopyToAsync
        ///     */
        /// }
        ///
        /// HttpContent.CopyToAsync -->
        /// HttpContent.SerializeToStreamAsync (bingo!)
        /// </code>
        ///
        /// Conclusion: by overriding HttpContent.SerializeToStreamAsync,
        /// we have full control over pumping bytes to the target stream for all protocols
        /// (except Web Sockets, which is handled separately).
        /// </remarks>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            if (Started)
            {
                throw new InvalidOperationException("Stream was already consumed.");
            }

            Started = true;
            try
            {
                // Immediately flush request stream to send headers
                // https://github.com/dotnet/corefx/issues/39586#issuecomment-516210081
                await stream.FlushAsync();

                await _streamCopier.CopyAsync(_source, stream, _cancellation);
                _tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
                throw;
            }
        }

        // this is used internally by HttpContent.ReadAsStreamAsync(...)
        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            // Nobody should be calling this...
            throw new NotImplementedException();
        }

        protected override bool TryComputeLength(out long length)
        {
            // We can't know the length of the content being pushed to the output stream.
            length = -1;
            return false;
        }
    }
}
