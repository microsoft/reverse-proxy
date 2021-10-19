// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder
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
    internal sealed class StreamCopyHttpContent : HttpContent
    {
        private readonly Stream _source;
        private readonly bool _autoFlushHttpClientOutgoingStream;
        private readonly IClock _clock;
        private readonly ActivityCancellationTokenSource _activityToken;
        private readonly TaskCompletionSource<(StreamCopyResult, Exception?)> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;

        public StreamCopyHttpContent(Stream source, bool autoFlushHttpClientOutgoingStream, IClock clock, ActivityCancellationTokenSource activityToken)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _autoFlushHttpClientOutgoingStream = autoFlushHttpClientOutgoingStream;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            _activityToken = activityToken;
        }

        /// <summary>
        /// Gets a <see cref="System.Threading.Tasks.Task"/> that completes in successful or failed state
        /// mimicking the result of <see cref="SerializeToStreamAsync"/>.
        /// </summary>
        public Task<(StreamCopyResult, Exception?)> ConsumptionTask => _tcs.Task;

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
        public bool Started => Volatile.Read(ref _started) == 1;

        public bool InProgress => Started && !ConsumptionTask.IsCompleted;

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
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return SerializeToStreamAsync(stream, context, CancellationToken.None);
        }

#if NET
        protected override
#else
        private
#endif
            async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                throw new InvalidOperationException("Stream was already consumed.");
            }

            // The cancellationToken that is passed to this method is:
            // On HTTP/1.1: Linked HttpContext.RequestAborted + Request Timeout
            // On HTTP/2.0: SocketsHttpHandler error / the server wants us to stop sending content / H2 connection closed
            // _cancellation will be the same as cancellationToken for HTTP/1.1, so we can avoid the overhead of linking them
            CancellationTokenRegistration registration = default;

#if NET
            if (_activityToken.Token != cancellationToken)
            {
                Debug.Assert(cancellationToken.CanBeCanceled);
                registration = cancellationToken.UnsafeRegister(ActivityCancellationTokenSource.LinkedTokenCancelDelegate, _activityToken);
            }
#else
            // On .NET Core 3.1, cancellationToken will always be CancellationToken.None
            Debug.Assert(!cancellationToken.CanBeCanceled);
#endif

            try
            {
                if (_autoFlushHttpClientOutgoingStream)
                {
                    // HttpClient's machinery keeps an internal buffer that doesn't get flushed to the socket on every write.
                    // Some protocols (e.g. gRPC) may rely on specific bytes being sent, and HttpClient's buffering would prevent it.
                    // AutoFlushingStream delegates to the provided stream, adding calls to FlushAsync on every WriteAsync.
                    // Note that HttpClient does NOT call Flush on the underlying socket, so the perf impact of this is expected to be small.
                    // This statement is based on current knowledge as of .NET Core 3.1.201.
                    stream = new AutoFlushingStream(stream);
                }

                // Immediately flush request stream to send headers
                // https://github.com/dotnet/corefx/issues/39586#issuecomment-516210081
                try
                {
                    await stream.FlushAsync(_activityToken.Token);
                }
                catch (OperationCanceledException oex)
                {
                    _tcs.TrySetResult((StreamCopyResult.Canceled, oex));
                    return;
                }
                catch (Exception ex)
                {
                    _tcs.TrySetResult((StreamCopyResult.OutputError, ex));
                    return;
                }

                var (result, error) = await StreamCopier.CopyAsync(isRequest: true, _source, stream, _clock, _activityToken);
                _tcs.TrySetResult((result, error));

                // Check for errors that weren't the result of the destination failing.
                // We have to throw something here so the transport knows the body is incomplete.
                // We can't re-throw the original exception since that would cause concurrency issues.
                // We need to wrap it.
                if (result == StreamCopyResult.InputError)
                {
                    throw new IOException("An error occurred when reading the request body from the client.", error);
                }
                if (result == StreamCopyResult.Canceled)
                {
                    throw new OperationCanceledException("The request body copy was canceled.", error);
                }
            }
            finally
            {
                registration.Dispose();
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
