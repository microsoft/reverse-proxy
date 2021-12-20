// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Default implementation of <see cref="IHttpForwarder"/>.
/// </summary>
internal sealed class HttpForwarder : IHttpForwarder
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);
    private static readonly Version DefaultVersion = HttpVersion.Version20;
#if NET
    private static readonly HttpVersionPolicy DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
#endif
    private readonly ILogger _logger;
    private readonly IClock _clock;

    public HttpForwarder(ILogger<HttpForwarder> logger, IClock clock)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Proxies the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <remarks>
    /// In what follows, as well as throughout in Reverse Proxy, we consider
    /// the following picture as illustrative of the Proxy.
    /// <code>
    ///      +-------------------+
    ///      |  Destination      +
    ///      +-------------------+
    ///            ▲       |
    ///        (b) |       | (c)
    ///            |       ▼
    ///      +-------------------+
    ///      |      Proxy        +
    ///      +-------------------+
    ///            ▲       |
    ///        (a) |       | (d)
    ///            |       ▼
    ///      +-------------------+
    ///      | Client            +
    ///      +-------------------+
    /// </code>
    ///
    /// (a) and (b) show the *request* path, going from the client to the target.
    /// (c) and (d) show the *response* path, going from the destination back to the client.
    ///
    /// Normal proxying comprises the following steps:
    ///    (0) Disable ASP .NET Core limits for streaming requests
    ///    (1) Create outgoing HttpRequestMessage
    ///    (2) Setup copy of request body (background)             Client --► Proxy --► Destination
    ///    (3) Copy request headers                                Client --► Proxy --► Destination
    ///    (4) Send the outgoing request using HttpMessageInvoker  Client --► Proxy --► Destination
    ///    (5) Copy response status line                           Client ◄-- Proxy ◄-- Destination
    ///    (6) Copy response headers                               Client ◄-- Proxy ◄-- Destination
    ///    (7-A) Check for a 101 upgrade response, this takes care of WebSockets as well as any other upgradeable protocol.
    ///        (7-A-1)  Upgrade client channel                     Client ◄--- Proxy ◄--- Destination
    ///        (7-A-2)  Copy duplex streams and return             Client ◄--► Proxy ◄--► Destination
    ///    (7-B) Copy (normal) response body                       Client ◄-- Proxy ◄-- Destination
    ///    (8) Copy response trailer headers and finish response   Client ◄-- Proxy ◄-- Destination
    ///    (9) Wait for completion of step 2: copying request body Client --► Proxy --► Destination
    ///
    /// ASP .NET Core (Kestrel) will finally send response trailers (if any)
    /// after we complete the steps above and relinquish control.
    /// </remarks>
    public async ValueTask<ForwarderError> SendAsync(
        HttpContext context,
        string destinationPrefix,
        HttpMessageInvoker httpClient,
        ForwarderRequestConfig requestConfig,
        HttpTransformer transformer)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
        _ = destinationPrefix ?? throw new ArgumentNullException(nameof(destinationPrefix));
        _ = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ = requestConfig ?? throw new ArgumentNullException(nameof(requestConfig));
        _ = transformer ?? throw new ArgumentNullException(nameof(transformer));

        // HttpClient overload for SendAsync changes response behavior to fully buffered which impacts performance
        // See discussion in https://github.com/microsoft/reverse-proxy/issues/458
        if (httpClient is HttpClient)
        {
            throw new ArgumentException($"The http client must be of type HttpMessageInvoker, not HttpClient", nameof(httpClient));
        }

        ForwarderTelemetry.Log.ForwarderStart(destinationPrefix);

        var activityCancellationSource = ActivityCancellationTokenSource.Rent(requestConfig?.ActivityTimeout ?? DefaultTimeout, context.RequestAborted);
        try
        {
            var isClientHttp2 = ProtocolHelper.IsHttp2(context.Request.Protocol);

            // NOTE: We heuristically assume gRPC-looking requests may require streaming semantics.
            // See https://github.com/microsoft/reverse-proxy/issues/118 for design discussion.
            var isStreamingRequest = isClientHttp2 && ProtocolHelper.IsGrpcContentType(context.Request.ContentType);

            // :: Step 1-3: Create outgoing HttpRequestMessage
            var (destinationRequest, requestContent) = await CreateRequestMessageAsync(
                context, destinationPrefix, transformer, requestConfig, isStreamingRequest, activityCancellationSource);

            // :: Step 4: Send the outgoing request using HttpClient
            HttpResponseMessage destinationResponse;
            try
            {
                ForwarderTelemetry.Log.ForwarderStage(ForwarderStage.SendAsyncStart);
                destinationResponse = await httpClient.SendAsync(destinationRequest, activityCancellationSource.Token);
                ForwarderTelemetry.Log.ForwarderStage(ForwarderStage.SendAsyncStop);

                // Reset the timeout since we received the response headers.
                activityCancellationSource.ResetTimeout();
            }
            catch (Exception requestException)
            {
                return await HandleRequestFailureAsync(context, requestContent, requestException, transformer, activityCancellationSource);
            }

            // Detect connection downgrade, which may be problematic for e.g. gRPC.
            if (isClientHttp2 && destinationResponse.Version.Major != 2)
            {
                // TODO: Do something on connection downgrade...
                Log.HttpDowngradeDetected(_logger);
            }

            try
            {
                // :: Step 5: Copy response status line Client ◄-- Proxy ◄-- Destination
                // :: Step 6: Copy response headers Client ◄-- Proxy ◄-- Destination
                var copyBody = await CopyResponseStatusAndHeadersAsync(destinationResponse, context, transformer);

                if (!copyBody)
                {
                    // The transforms callback decided that the response body should be discarded.
                    destinationResponse.Dispose();

                    if (requestContent is not null && requestContent.InProgress)
                    {
                        activityCancellationSource.Cancel();
                        await requestContent.ConsumptionTask;
                    }

                    return ForwarderError.None;
                }
            }
            catch (Exception ex)
            {
                destinationResponse.Dispose();

                if (requestContent is not null && requestContent.InProgress)
                {
                    activityCancellationSource.Cancel();
                    await requestContent.ConsumptionTask;
                }

                ReportProxyError(context, ForwarderError.ResponseHeaders, ex);
                // Clear the response since status code, reason and some headers might have already been copied and we want clean 502 response.
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return ForwarderError.ResponseHeaders;
            }

            // :: Step 7-A: Check for a 101 upgrade response, this takes care of WebSockets as well as any other upgradeable protocol.
            if (destinationResponse.StatusCode == HttpStatusCode.SwitchingProtocols)
            {
                Debug.Assert(requestContent?.Started != true);
                return await HandleUpgradedResponse(context, destinationResponse, activityCancellationSource);
            }

            // NOTE: it may *seem* wise to call `context.Response.StartAsync()` at this point
            // since it looks like we are ready to send back response headers
            // (and this might help reduce extra delays while we wait to receive the body from the destination).
            // HOWEVER, this would produce the wrong result if it turns out that there is no content
            // from the destination -- instead of sending headers and terminating the stream at once,
            // we would send headers thinking a body may be coming, and there is none.
            // This is problematic on gRPC connections when the destination server encounters an error,
            // in which case it immediately returns the response headers and trailing headers, but no content,
            // and clients misbehave if the initial headers response does not indicate stream end.

            // :: Step 7-B: Copy response body Client ◄-- Proxy ◄-- Destination
            var (responseBodyCopyResult, responseBodyException) = await CopyResponseBodyAsync(destinationResponse.Content, context.Response.Body, activityCancellationSource);

            if (responseBodyCopyResult != StreamCopyResult.Success)
            {
                return await HandleResponseBodyErrorAsync(context, requestContent, responseBodyCopyResult, responseBodyException!, activityCancellationSource);
            }

            // :: Step 8: Copy response trailer headers and finish response Client ◄-- Proxy ◄-- Destination
            await CopyResponseTrailingHeadersAsync(destinationResponse, context, transformer);

            if (isStreamingRequest)
            {
                // NOTE: We must call `CompleteAsync` so that Kestrel will flush all bytes to the client.
                // In the case where there was no response body,
                // this is also when headers and trailing headers are sent to the client.
                // Without this, the client might wait forever waiting for response bytes,
                // while we might wait forever waiting for request bytes,
                // leading to a stuck connection and no way to make progress.
                await context.Response.CompleteAsync();
            }

            // :: Step 9: Wait for completion of step 2: copying request body Client --► Proxy --► Destination
            // NOTE: It is possible for the request body to NOT be copied even when there was an incoming requet body,
            // e.g. when the request includes header `Expect: 100-continue` and the destination produced a non-1xx response.
            // We must only wait for the request body to complete if it actually started,
            // otherwise we run the risk of waiting indefinitely for a task that will never complete.
            if (requestContent is not null && requestContent.Started)
            {
                var (requestBodyCopyResult, requestBodyException) = await requestContent.ConsumptionTask;

                if (requestBodyCopyResult != StreamCopyResult.Success)
                {
                    // The response succeeded. If there was a request body error then it was probably because the client or destination decided
                    // to cancel it. Report as low severity.

                    var error = requestBodyCopyResult switch
                    {
                        StreamCopyResult.InputError => ForwarderError.RequestBodyClient,
                        StreamCopyResult.OutputError => ForwarderError.RequestBodyDestination,
                        StreamCopyResult.Canceled => ForwarderError.RequestBodyCanceled,
                        _ => throw new NotImplementedException(requestBodyCopyResult.ToString())
                    };
                    ReportProxyError(context, error, requestBodyException!);
                    return error;
                }
            }
        }
        finally
        {
            activityCancellationSource.Return();
            ForwarderTelemetry.Log.ForwarderStop(context.Response.StatusCode);
        }

        return ForwarderError.None;
    }

    private async ValueTask<(HttpRequestMessage, StreamCopyHttpContent?)> CreateRequestMessageAsync(HttpContext context, string destinationPrefix,
        HttpTransformer transformer, ForwarderRequestConfig? requestConfig, bool isStreamingRequest, ActivityCancellationTokenSource activityToken)
    {
        // "http://a".Length = 8
        if (destinationPrefix == null || destinationPrefix.Length < 8)
        {
            throw new ArgumentException("Invalid destination prefix.", nameof(destinationPrefix));
        }

        var destinationRequest = new HttpRequestMessage();

        destinationRequest.Method = RequestUtilities.GetHttpMethod(context.Request.Method);

        var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
        var upgradeHeader = context.Request.Headers[HeaderNames.Upgrade].ToString();
        var isUpgradeRequest = (upgradeFeature?.IsUpgradableRequest ?? false)
            // Mitigate https://github.com/microsoft/reverse-proxy/issues/255, IIS considers all requests upgradeable.
            && (string.Equals("WebSocket", upgradeHeader, StringComparison.OrdinalIgnoreCase)
                // https://github.com/microsoft/reverse-proxy/issues/467 for kubernetes APIs
                || upgradeHeader.StartsWith("SPDY/", StringComparison.OrdinalIgnoreCase));

        // Default to HTTP/1.1 for proxying upgradeable requests. This is already the default as of .NET Core 3.1
        // Otherwise request what's set in proxyOptions (e.g. default HTTP/2) and let HttpClient negotiate the protocol
        // based on VersionPolicy (for .NET 5 and higher). For example, downgrading to HTTP/1.1 if it cannot establish HTTP/2 with the target.
        // This is done without extra round-trips thanks to ALPN. We can detect a downgrade after calling HttpClient.SendAsync
        // (see Step 3 below). TBD how this will change when HTTP/3 is supported.
        destinationRequest.Version = isUpgradeRequest ? ProtocolHelper.Http11Version : (requestConfig?.Version ?? DefaultVersion);
#if NET
        destinationRequest.VersionPolicy = isUpgradeRequest ? HttpVersionPolicy.RequestVersionOrLower : (requestConfig?.VersionPolicy ?? DefaultVersionPolicy);
#endif

        // :: Step 2: Setup copy of request body (background) Client --► Proxy --► Destination
        // Note that we must do this before step (3) because step (3) may also add headers to the HttpContent that we set up here.
        var requestContent = SetupRequestBodyCopy(context.Request, isStreamingRequest, activityToken);
        destinationRequest.Content = requestContent;

        // :: Step 3: Copy request headers Client --► Proxy --► Destination
        await transformer.TransformRequestAsync(context, destinationRequest, destinationPrefix);

        if (isUpgradeRequest)
        {
            RestoreUpgradeHeaders(context, destinationRequest);
        }

        // Allow someone to custom build the request uri, otherwise provide a default for them.
        var request = context.Request;
        destinationRequest.RequestUri ??= RequestUtilities.MakeDestinationAddress(destinationPrefix, request.Path, request.QueryString);

        Log.Proxying(_logger, destinationRequest, isStreamingRequest);

        if (requestConfig?.AllowResponseBuffering != true)
        {
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        }

        // TODO: What if they replace the HttpContent object? That would mess with our tracking and error handling.
        return (destinationRequest, requestContent);
    }

    private static void RestoreUpgradeHeaders(HttpContext context, HttpRequestMessage request)
    {
        var connectionValues = context.Request.Headers.GetCommaSeparatedValues(HeaderNames.Connection);
        string? connectionUpgradeValue = null;
        foreach (var headerValue in connectionValues)
        {
            if (headerValue.Equals("upgrade", StringComparison.OrdinalIgnoreCase))
            {
                connectionUpgradeValue = headerValue;
                break;
            }
        }

        if (connectionUpgradeValue != null && context.Request.Headers.TryGetValue(HeaderNames.Upgrade, out var upgradeValue))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Connection, connectionUpgradeValue);
            request.Headers.TryAddWithoutValidation(HeaderNames.Upgrade, (IEnumerable<string>)upgradeValue);
        }
    }

    private StreamCopyHttpContent? SetupRequestBodyCopy(HttpRequest request, bool isStreamingRequest, ActivityCancellationTokenSource activityToken)
    {
        // If we generate an HttpContent without a Content-Length then for HTTP/1.1 HttpClient will add a Transfer-Encoding: chunked header
        // even if it's a GET request. Some servers reject requests containing a Transfer-Encoding header if they're not expecting a body.
        // Try to be as specific as possible about the client's intent to send a body. The one thing we don't want to do is to start
        // reading the body early because that has side-effects like 100-continue.
        var hasBody = true;
        var contentLength = request.Headers.ContentLength;
        var method = request.Method;

#if NET
        var canHaveBodyFeature = request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>();
        if (canHaveBodyFeature != null)
        {
            // 5.0 servers provide a definitive answer for us.
            hasBody = canHaveBodyFeature.CanHaveBody;
        }
        else
#endif
        // https://tools.ietf.org/html/rfc7230#section-3.3.3
        // All HTTP/1.1 requests should have Transfer-Encoding or Content-Length.
        // Http.Sys/IIS will even add a Transfer-Encoding header to HTTP/2 requests with bodies for back-compat.
        // HTTP/1.0 Connection: close bodies are only allowed on responses, not requests.
        // https://tools.ietf.org/html/rfc1945#section-7.2.2
        //
        // Transfer-Encoding overrides Content-Length per spec
        if (request.Headers.TryGetValue(HeaderNames.TransferEncoding, out var transferEncoding)
            && transferEncoding.Count == 1
            && string.Equals("chunked", transferEncoding.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            hasBody = true;
        }
        else if (contentLength.HasValue)
        {
            hasBody = contentLength > 0;
        }
        // Kestrel HTTP/2: There are no required headers that indicate if there is a request body so we need to sniff other fields.
        else if (!ProtocolHelper.IsHttp2OrGreater(request.Protocol))
        {
            hasBody = false;
        }
        // https://tools.ietf.org/html/rfc7231#section-4.3.1
        // A payload within a GET/HEAD/DELETE/CONNECT request message has no defined semantics; sending a payload body on a
        // GET/HEAD/DELETE/CONNECT request might cause some existing implementations to reject the request.
        // https://tools.ietf.org/html/rfc7231#section-4.3.8
        // A client MUST NOT send a message body in a TRACE request.
        else if (HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsDelete(method)
            || HttpMethods.IsConnect(method)
            || HttpMethods.IsTrace(method))
        {
            hasBody = false;
        }
        // else hasBody defaults to true

        if (hasBody)
        {
            if (isStreamingRequest)
            {
                DisableMinRequestBodyDataRateAndMaxRequestBodySize(request.HttpContext);
            }

            // Note on `autoFlushHttpClientOutgoingStream: isStreamingRequest`:
            // The.NET Core HttpClient stack keeps its own buffers on top of the underlying outgoing connection socket.
            // We flush those buffers down to the socket on every write when this is set,
            // but it does NOT result in calls to flush on the underlying socket.
            // This is necessary because we proxy http2 transparently,
            // and we are deliberately unaware of packet structure used e.g. in gRPC duplex channels.
            // Because the sockets aren't flushed, the perf impact of this choice is expected to be small.
            // Future: It may be wise to set this to true for *all* http2 incoming requests,
            // but for now, out of an abundance of caution, we only do it for requests that look like gRPC.
            return new StreamCopyHttpContent(
                source: request.Body,
                autoFlushHttpClientOutgoingStream: isStreamingRequest,
                clock: _clock,
                activityToken);
        }

        return null;
    }

    private ForwarderError HandleRequestBodyFailure(HttpContext context, StreamCopyResult requestBodyCopyResult, Exception requestBodyException, Exception additionalException)
    {
        ForwarderError requestBodyError;
        int statusCode;
        switch (requestBodyCopyResult)
        {
            // Failed while trying to copy the request body from the client. It's ambiguous if the request or response failed first.
            case StreamCopyResult.InputError:
                requestBodyError = ForwarderError.RequestBodyClient;
                statusCode = StatusCodes.Status400BadRequest;
                break;
            // Failed while trying to copy the request body to the destination. It's ambiguous if the request or response failed first.
            case StreamCopyResult.OutputError:
                requestBodyError = ForwarderError.RequestBodyDestination;
                statusCode = StatusCodes.Status502BadGateway;
                break;
            // Canceled while trying to copy the request body, either due to a client disconnect or a timeout. This probably caused the response to fail as a secondary error.
            case StreamCopyResult.Canceled:
                requestBodyError = ForwarderError.RequestBodyCanceled;
                // Timeouts (504s) are handled at the SendAsync call site.
                // The request body should only be canceled by the RequestAborted token.
                statusCode = StatusCodes.Status502BadGateway;
                break;
            default:
                throw new NotImplementedException(requestBodyCopyResult.ToString());
        }

        ReportProxyError(context, requestBodyError, new AggregateException(requestBodyException, additionalException));

        // We don't know if the client is still around to see this error, but set it for diagnostics to see.
        if (!context.Response.HasStarted)
        {
            // Nothing has been sent to the client yet, we can still send a good error response.
            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            return requestBodyError;
        }

        ResetOrAbort(context, isCancelled: requestBodyCopyResult == StreamCopyResult.Canceled);

        return requestBodyError;
    }

    private async ValueTask<ForwarderError> HandleRequestFailureAsync(HttpContext context, StreamCopyHttpContent? requestContent, Exception requestException, HttpTransformer transformer, CancellationTokenSource requestCancellationSource)
    {
        if (requestException is OperationCanceledException)
        {
            if (!context.RequestAborted.IsCancellationRequested && requestCancellationSource.IsCancellationRequested)
            {
                return await ReportErrorAsync(ForwarderError.RequestTimedOut, StatusCodes.Status504GatewayTimeout);
            }
            else
            {
                return await ReportErrorAsync(ForwarderError.RequestCanceled, StatusCodes.Status502BadGateway);
            }
        }

        // Check for request body errors, these may have triggered the response error.
        if (requestContent?.ConsumptionTask.IsCompleted == true)
        {
            var (requestBodyCopyResult, requestBodyException) = requestContent.ConsumptionTask.Result;

            if (requestBodyCopyResult != StreamCopyResult.Success)
            {
                var error = HandleRequestBodyFailure(context, requestBodyCopyResult, requestBodyException!, requestException);
                await transformer.TransformResponseAsync(context, proxyResponse: null);
                return error;
            }
        }

        // We couldn't communicate with the destination.
        return await ReportErrorAsync(ForwarderError.Request, StatusCodes.Status502BadGateway);

        async ValueTask<ForwarderError> ReportErrorAsync(ForwarderError error, int statusCode)
        {
            ReportProxyError(context, error, requestException);
            context.Response.StatusCode = statusCode;

            if (requestContent is not null && requestContent.InProgress)
            {
                requestCancellationSource.Cancel();
                await requestContent.ConsumptionTask;
            }

            await transformer.TransformResponseAsync(context, null);
            return error;
        }
    }

    private static ValueTask<bool> CopyResponseStatusAndHeadersAsync(HttpResponseMessage source, HttpContext context, HttpTransformer transformer)
    {
        context.Response.StatusCode = (int)source.StatusCode;

        if (!ProtocolHelper.IsHttp2OrGreater(context.Request.Protocol))
        {
            // Don't explicitly set the field if the default reason phrase is used
            if (source.ReasonPhrase != ReasonPhrases.GetReasonPhrase((int)source.StatusCode))
            {
                context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase = source.ReasonPhrase;
            }
        }

        // Copies headers
        return transformer.TransformResponseAsync(context, source);
    }

    private static void RestoreUpgradeHeaders(HttpContext context, HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues(HeaderNames.Connection, out var connectionValues)
            && response.Headers.TryGetValues(HeaderNames.Upgrade, out var upgradeValues))
        {
            var upgradeStringValues = StringValues.Empty;
            foreach (var value in upgradeValues)
            {
                upgradeStringValues = StringValues.Concat(upgradeStringValues, value);
            }
            context.Response.Headers.TryAdd(HeaderNames.Upgrade, upgradeStringValues);

            foreach (var value in connectionValues)
            {
                if (value.Equals("upgrade", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers.TryAdd(HeaderNames.Connection, value);
                    break;
                }
            }
        }
    }

    private async ValueTask<ForwarderError> HandleUpgradedResponse(HttpContext context, HttpResponseMessage destinationResponse,
        ActivityCancellationTokenSource activityCancellationSource)
    {
        ForwarderTelemetry.Log.ForwarderStage(ForwarderStage.ResponseUpgrade);

        // SocketHttpHandler and similar transports always provide an HttpContent object, even if it's empty.
        // Note as of 5.0 HttpResponse.Content never returns null.
        // https://github.com/dotnet/runtime/blame/8fc68f626a11d646109a758cb0fc70a0aa7826f1/src/libraries/System.Net.Http/src/System/Net/Http/HttpResponseMessage.cs#L46
        if (destinationResponse.Content == null)
        {
            throw new InvalidOperationException("A response content is required for upgrades.");
        }

        // :: Step 7-A-1: Upgrade the client channel. This will also send response headers.
        var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
        if (upgradeFeature == null)
        {
            var ex = new InvalidOperationException("Invalid 101 response when upgrades aren't supported.");
            destinationResponse.Dispose();
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            ReportProxyError(context, ForwarderError.UpgradeResponseDestination, ex);
            return ForwarderError.UpgradeResponseDestination;
        }

        RestoreUpgradeHeaders(context, destinationResponse);

        Stream upgradeResult;
        try
        {
            upgradeResult = await upgradeFeature.UpgradeAsync();
        }
        catch (Exception ex)
        {
            destinationResponse.Dispose();
            ReportProxyError(context, ForwarderError.UpgradeResponseClient, ex);
            return ForwarderError.UpgradeResponseClient;
        }
        using var clientStream = upgradeResult;

        // :: Step 7-A-2: Copy duplex streams
        using var destinationStream = await destinationResponse.Content.ReadAsStreamAsync();

        var requestTask = StreamCopier.CopyAsync(isRequest: true, clientStream, destinationStream, StreamCopier.UnknownLength, _clock, activityCancellationSource, activityCancellationSource.Token).AsTask();
        var responseTask = StreamCopier.CopyAsync(isRequest: false, destinationStream, clientStream, StreamCopier.UnknownLength, _clock, activityCancellationSource, activityCancellationSource.Token).AsTask();

        // Make sure we report the first failure.
        var firstTask = await Task.WhenAny(requestTask, responseTask);
        var requestFinishedFirst = firstTask == requestTask;
        var secondTask = requestFinishedFirst ? responseTask : requestTask;

        ForwarderError error;

        var (firstResult, firstException) = await firstTask;
        if (firstResult != StreamCopyResult.Success)
        {
            error = ReportResult(context, requestFinishedFirst, firstResult, firstException);
            // Cancel the other direction
            activityCancellationSource.Cancel();
            // Wait for this to finish before exiting so the resources get cleaned up properly.
            await secondTask;
        }
        else
        {
            var (secondResult, secondException) = await secondTask;
            if (secondResult != StreamCopyResult.Success)
            {
                error = ReportResult(context, !requestFinishedFirst, secondResult, secondException!);
            }
            else
            {
                error = ForwarderError.None;
            }
        }

        return error;

        ForwarderError ReportResult(HttpContext context, bool reqeuest, StreamCopyResult result, Exception exception)
        {
            var error = result switch
            {
                StreamCopyResult.InputError => reqeuest ? ForwarderError.UpgradeRequestClient : ForwarderError.UpgradeResponseDestination,
                StreamCopyResult.OutputError => reqeuest ? ForwarderError.UpgradeRequestDestination : ForwarderError.UpgradeResponseClient,
                StreamCopyResult.Canceled => reqeuest ? ForwarderError.UpgradeRequestCanceled : ForwarderError.UpgradeResponseCanceled,
                _ => throw new NotImplementedException(result.ToString()),
            };
            ReportProxyError(context, error, exception);
            return error;
        }
    }

    private async ValueTask<(StreamCopyResult, Exception?)> CopyResponseBodyAsync(HttpContent destinationResponseContent, Stream clientResponseStream,
        ActivityCancellationTokenSource activityCancellationSource)
    {
        // SocketHttpHandler and similar transports always provide an HttpContent object, even if it's empty.
        // In 3.1 this is only likely to return null in tests.
        // As of 5.0 HttpResponse.Content never returns null.
        // https://github.com/dotnet/runtime/blame/8fc68f626a11d646109a758cb0fc70a0aa7826f1/src/libraries/System.Net.Http/src/System/Net/Http/HttpResponseMessage.cs#L46
        if (destinationResponseContent != null)
        {
            using var destinationResponseStream = await destinationResponseContent.ReadAsStreamAsync();
            // The response content-length is enforced by the server.
            return await StreamCopier.CopyAsync(isRequest: false, destinationResponseStream, clientResponseStream, StreamCopier.UnknownLength, _clock, activityCancellationSource, activityCancellationSource.Token);
        }

        return (StreamCopyResult.Success, null);
    }

    private async ValueTask<ForwarderError> HandleResponseBodyErrorAsync(HttpContext context, StreamCopyHttpContent? requestContent, StreamCopyResult responseBodyCopyResult, Exception responseBodyException, CancellationTokenSource requestCancellationSource)
    {
        if (requestContent is not null && requestContent.Started)
        {
            var alreadyFinished = requestContent.ConsumptionTask.IsCompleted == true;

            if (!alreadyFinished)
            {
                requestCancellationSource.Cancel();
            }

            var (requestBodyCopyResult, requestBodyError) = await requestContent.ConsumptionTask;

            // Check for request body errors, these may have triggered the response error.
            if (alreadyFinished && requestBodyCopyResult != StreamCopyResult.Success)
            {
                return HandleRequestBodyFailure(context, requestBodyCopyResult, requestBodyError!, responseBodyException);
            }
        }

        var error = responseBodyCopyResult switch
        {
            StreamCopyResult.InputError => ForwarderError.ResponseBodyDestination,
            StreamCopyResult.OutputError => ForwarderError.ResponseBodyClient,
            StreamCopyResult.Canceled => ForwarderError.ResponseBodyCanceled,
            _ => throw new NotImplementedException(responseBodyCopyResult.ToString()),
        };
        ReportProxyError(context, error, responseBodyException);

        if (!context.Response.HasStarted)
        {
            // Nothing has been sent to the client yet, we can still send a good error response.
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return error;
        }

        // The response has already started, we must forcefully terminate it so the client doesn't get the
        // the mistaken impression that the truncated response is complete.
        ResetOrAbort(context, isCancelled: responseBodyCopyResult == StreamCopyResult.Canceled);

        return error;
    }

    private static ValueTask CopyResponseTrailingHeadersAsync(HttpResponseMessage source, HttpContext context, HttpTransformer transformer)
    {
        // Copies trailers
        return transformer.TransformResponseTrailersAsync(context, source);
    }


    /// <summary>
    /// Disable some ASP .NET Core server limits so that we can handle long-running gRPC requests unconstrained.
    /// Note that the gRPC server implementation on ASP .NET Core does the same for client-streaming and duplex methods.
    /// Since in Gateway we have no way to determine if the current request requires client-streaming or duplex comm,
    /// we do this for *all* incoming requests that look like they might be gRPC.
    /// </summary>
    /// <remarks>
    /// Inspired on
    /// <see href="https://github.com/grpc/grpc-dotnet/blob/3ce9b104524a4929f5014c13cd99ba9a1c2431d4/src/Grpc.AspNetCore.Server/Internal/CallHandlers/ServerCallHandlerBase.cs#L127"/>.
    /// </remarks>
    private void DisableMinRequestBodyDataRateAndMaxRequestBodySize(HttpContext httpContext)
    {
        var minRequestBodyDataRateFeature = httpContext.Features.Get<IHttpMinRequestBodyDataRateFeature>();
        if (minRequestBodyDataRateFeature != null)
        {
            minRequestBodyDataRateFeature.MinDataRate = null;
        }

        var maxRequestBodySizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxRequestBodySizeFeature != null)
        {
            if (!maxRequestBodySizeFeature.IsReadOnly)
            {
                maxRequestBodySizeFeature.MaxRequestBodySize = null;
            }
            else
            {
                // IsReadOnly could be true if middleware has already started reading the request body
                // In that case we can't disable the max request body size for the request stream
                _logger.LogWarning("Unable to disable max request body size.");
            }
        }
    }

    private void ReportProxyError(HttpContext context, ForwarderError error, Exception ex)
    {
        context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(error, ex));
        Log.ErrorProxying(_logger, error, ex);
        ForwarderTelemetry.Log.ForwarderFailed(error);
    }

    private static void ResetOrAbort(HttpContext context, bool isCancelled)
    {
        var resetFeature = context.Features.Get<IHttpResetFeature>();
        if (resetFeature != null)
        {
            // https://tools.ietf.org/html/rfc7540#section-7
            const int Cancelled = 2;
            const int InternalError = 8;
            resetFeature.Reset(isCancelled ? Cancelled : InternalError);
            return;
        }

        context.Abort();
    }

    private static class Log
    {
        private static readonly Action<ILogger, Exception?> _httpDowngradeDetected = LoggerMessage.Define(
            LogLevel.Debug,
            EventIds.HttpDowngradeDetected,
            "The request was downgraded from HTTP/2.");

        private static readonly Action<ILogger, string, string, string, string, Exception?> _proxying = LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information,
            EventIds.Forwarding,
            "Proxying to {targetUrl} {version} {versionPolicy} {isStreaming}");

        private static readonly Action<ILogger, ForwarderError, string, Exception> _proxyError = LoggerMessage.Define<ForwarderError, string>(
            LogLevel.Information,
            EventIds.ForwardingError,
            "{error}: {message}");

        public static void HttpDowngradeDetected(ILogger logger)
        {
            _httpDowngradeDetected(logger, null);
        }

        public static void Proxying(ILogger logger, HttpRequestMessage msg, bool isStreamingRequest)
        {
            // Avoid computing the AbsoluteUri unless logging is enabled
            if (logger.IsEnabled(LogLevel.Information))
            {
                var streaming = isStreamingRequest ? "streaming" : "no-streaming";
                var version = ProtocolHelper.GetHttpProtocol(msg.Version);
#if NET
                var versionPolicy = ProtocolHelper.GetVersionPolicy(msg.VersionPolicy);
#else
                var versionPolicy = "RequestVersionOrLower";
#endif
                _proxying(logger, msg.RequestUri!.AbsoluteUri, version, versionPolicy, streaming, null);
            }
        }

        public static void ErrorProxying(ILogger logger, ForwarderError error, Exception ex)
        {
            _proxyError(logger, error, GetMessage(error), ex);
        }

        private static string GetMessage(ForwarderError error)
        {
            return error switch
            {
                ForwarderError.None => throw new NotSupportedException("A more specific error must be used"),
                ForwarderError.Request => "An error was encountered before receiving a response.",
                ForwarderError.RequestTimedOut => "The request timed out before receiving a response.",
                ForwarderError.RequestCanceled => "The request was canceled before receiving a response.",
                ForwarderError.RequestBodyCanceled => "Copying the request body was canceled.",
                ForwarderError.RequestBodyClient => "The client reported an error when copying the request body.",
                ForwarderError.RequestBodyDestination => "The destination reported an error when copying the request body.",
                ForwarderError.ResponseBodyCanceled => "Copying the response body was canceled.",
                ForwarderError.ResponseBodyClient => "The client reported an error when copying the response body.",
                ForwarderError.ResponseBodyDestination => "The destination reported an error when copying the response body.",
                ForwarderError.ResponseHeaders => "The destination returned a response that cannot be proxied back to the client.",
                ForwarderError.UpgradeRequestCanceled => "Copying the upgraded request body was canceled.",
                ForwarderError.UpgradeRequestClient => "The client reported an error when copying the upgraded request body.",
                ForwarderError.UpgradeRequestDestination => "The destination reported an error when copying the upgraded request body.",
                ForwarderError.UpgradeResponseCanceled => "Copying the upgraded response body was canceled.",
                ForwarderError.UpgradeResponseClient => "The client reported an error when copying the upgraded response body.",
                ForwarderError.UpgradeResponseDestination => "The destination reported an error when copying the upgraded response body.",
                ForwarderError.NoAvailableDestinations => throw new NotImplementedException(), // Not used in this class
                _ => throw new NotImplementedException(error.ToString()),
            };
        }
    }
}
