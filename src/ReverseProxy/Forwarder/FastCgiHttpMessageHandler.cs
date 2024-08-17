using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Yarp.ReverseProxy.Forwarder;


public sealed class FastCgiHttpMessageHandler(IOptions<SocketConnectionFactoryOptions> options, ILogger logger) : HttpMessageHandler
{
    private readonly SocketConnectionContextFactory _connectionFactory = new(options.Value, logger);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.RequestUri, nameof(request.RequestUri));

        // Disallow null bytes in the request path, because
        // PHP upstreams may do bad things, like execute a
        // non-PHP file as PHP code. See #4574
        // https://github.com/caddyserver/caddy/blob/840094ac65c2c27dbf0be63478d36969a57ce7e0/modules/caddyhttp/reverseproxy/fastcgi/fastcgi.go#L119
        if (request.RequestUri.PathAndQuery.Contains('\x00'))
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("invalid request path") };
        }

        //TODO: maybe implement static connect to endpoint specified by options when creating FastCgiHttpMessageHandler.
        var endPoint = new DnsEndPoint(request.RequestUri.Host, request.RequestUri.Port);

        //TODO: implement connection pooling
        await using var connection = await ConnectAsync(endPoint, noDelay: true, cancellationToken);

        var flushResult = await SendAsync(
            new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.BeginRequest, ContentLength: FastCgiRecordBeginRequestBody.ByteCount),
            new FastCgiRecordBeginRequestBody(Role: FastCgiRecordBeginRequestBody.RoleType.Responder, KeepConn: false),
            connection, cancellationToken);

        flushResult.ThrowIfCanceled();

        var fastCgiParams = BuildParams(connection.RemoteEndPoint, request);

        var paramsBody = new FastCgiRecordParamsBody(fastCgiParams);

        flushResult = await SendAsync(
            new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Params, ContentLength: (ushort)paramsBody.ByteCount()),
            paramsBody,
            connection, cancellationToken: cancellationToken);

        flushResult.ThrowIfCanceled();

        flushResult = await SendAsync(
            new FastCgiRecord(
                Header: new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Params),
                ContentData: RentedReadOnlyMemory<byte>.Empty),
            connection, cancellationToken);

        flushResult.ThrowIfCanceled();

        if (request.Content != null)
        {
            var contentStream = await request.Content.ReadAsStreamAsync(cancellationToken);
            var partBuffer = ArrayPool<byte>.Shared.Rent(FastCgiRecordHeader.MAX_CONTENT_SIZE);
            var partMemory = partBuffer.AsMemory(0, FastCgiRecordHeader.MAX_CONTENT_SIZE);

            try
            {
                while (true)
                {
                    var consumed = 0;
                    while ((consumed += await contentStream.ReadAsync(partMemory[consumed..], cancellationToken)) > 0 && consumed < partMemory.Length)
                    {

                    }

                    if (consumed == 0) { break; }

                    flushResult = await SendAsync(
                            new FastCgiRecord(
                                Header: new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Stdin, ContentLength: (ushort)consumed),
                                ContentData: new RentedReadOnlyMemory<byte>(partMemory[..consumed])),
                            connection, cancellationToken);

                    flushResult.ThrowIfCanceled();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(partBuffer);
            }
        }

        flushResult = await SendAsync(
        new FastCgiRecord(
            Header: new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Stdin),
            ContentData: RentedReadOnlyMemory<byte>.Empty),
        connection, cancellationToken);

        flushResult.ThrowIfCanceled();

        var response = new HttpResponseMessage() { RequestMessage = request, StatusCode = HttpStatusCode.OK };
        var httpResponseReader = new HttpResponseReaderFastcgiRecordHandler(response, logger);

        try
        {
            await FastCgiRecordReader.ProcessAsync(httpResponseReader, connection, cancellationToken);
            return response;
        }
        catch (BadFastCgiResponseException ex)
        {
            response.StatusCode = HttpStatusCode.InternalServerError;
            response.Content = new StringContent(ex.Message);
            return response;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connectionFactory.Dispose();
        }
        base.Dispose(disposing);
    }

    private async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, bool noDelay, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            // Value that specifies whether the stream System.Net.Sockets.Socket
            // is using the Nagle algorithm.
            NoDelay = noDelay,
        };

        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return _connectionFactory.Create(socket);
    }

    private static async ValueTask<FlushResult> SendAsync(FastCgiRecord fastcgiRecord, ConnectionContext connectionContext, CancellationToken cancellationToken)
    {
        fastcgiRecord.Header.WriteTo(connectionContext.Transport.Output);

        var result = await connectionContext.Transport.Output.WriteAsync(fastcgiRecord.ContentData.Memory, cancellationToken);
        result.ThrowIfCanceled();
        //TODO: probably flush is not needed but it does not hurt either
        return await connectionContext.Transport.Output.FlushAsync(cancellationToken);
    }

    private static ValueTask<FlushResult> SendAsync(FastCgiRecordHeader header, IFastCgiContentDataWriter contentData, ConnectionContext connectionContext, CancellationToken cancellationToken)
    {
        header.WriteTo(connectionContext.Transport.Output);
        contentData.WriteTo(connectionContext.Transport.Output);

        return connectionContext.Transport.Output.FlushAsync(cancellationToken);
    }

    private static ReadOnlyCollection<(string, string)> BuildParams(System.Net.EndPoint? connRemoteEndpoint, HttpRequestMessage request)
    {
        string remoteEndpoint;
        string remotePort;

        if (connRemoteEndpoint is IPEndPoint re)
        {
            remoteEndpoint = re.Address.ToString();
            remotePort = re.Port.ToString();
        }
        else
        {
            remotePort = remoteEndpoint = string.Empty;
        }

        if (!request.Options.TryGetValue(FastCgiHttpOptions.DOCUMENT_ROOT, out var documentRoot))
        {
            documentRoot = string.Empty;
        }

        var fpath = request.RequestUri?.LocalPath ?? string.Empty;

        var pathInfo = fpath;
        var documentURI = fpath;
        var scriptName = fpath;

        // Ensure the SCRIPT_NAME has a leading slash for compliance with RFC3875
        // Info: https://tools.ietf.org/html/rfc3875#section-4.1.13
        if (scriptName.Length > 0 && !scriptName.StartsWith('/'))
        {
            scriptName = $"/{scriptName}";
        }

        var scriptFilename = SanitizedPathJoin(documentRoot, pathInfo);

        var serverName = request.Headers.Host ?? string.Empty;
        if (serverName.Length > 0)
        {
            //TODO: write better splitting of host / port
            if (Uri.TryCreate($"tcp://{serverName}", default, out var u))
            {
                serverName = u.Host;
            }
        }

        //TODO: probably better https detection will be needed at some point
        var isHttps = false;
        if (request.RequestUri?.Scheme == "https")
        {
            isHttps = true;
        }


        //TODO: is list a good data structer here? maybe used some data structer from *pool,
        var fastCgiParams = new List<(string, string)>
        {
            (FastCgiCoreParams.CONTENT_LENGTH, request.Content?.Headers.ContentLength?.ToString() ?? string.Empty),
            // based on caddy implementation https://github.com/caddyserver/caddy/blob/9ddb78fadcdbec89a609127918604174121dcf42/modules/caddyhttp/reverseproxy/fastcgi/client.go#L289
            (FastCgiCoreParams.CONTENT_TYPE, request.Content?.Headers.ContentType?.ToString() ?? (request.Method == System.Net.Http.HttpMethod.Post ? "application/x-www-form-urlencoded" : string.Empty) ),

            (FastCgiCoreParams.GATEWAY_INTERFACE, FastCgiCoreParams.GATEWAY_INTERFACE_CGI11),

            (FastCgiCoreParams.DOCUMENT_ROOT, documentRoot),
            (FastCgiCoreParams.DOCUMENT_URI, documentURI),

            //(FastCgiCoreParams.HTTP_HOST, request.Headers.Host ?? string.Empty), // will be added later

            (FastCgiCoreParams.HTTPS, isHttps ? FastCgiCoreParams.HTTPS_ON : FastCgiCoreParams.HTTPS_OFF),

            (FastCgiCoreParams.PATH_INFO, pathInfo),

            (FastCgiCoreParams.QUERY_STRING, request.RequestUri?.Query?.ToString() ?? string.Empty),

            (FastCgiCoreParams.REMOTE_ADDR, remoteEndpoint),
            (FastCgiCoreParams.REMOTE_HOST, remoteEndpoint), // For performance, remote host lookup is disabled
            (FastCgiCoreParams.REMOTE_PORT, remotePort),

            //TODO: User after basic auth was performed by loadbalancer
            //(FastCgiCoreParams.REMOTE_USER, string.Empty),

            (FastCgiCoreParams.REQUEST_URI, request.RequestUri?.ToString() ?? string.Empty),
            (FastCgiCoreParams.REQUEST_METHOD, request.Method.Method),
            (FastCgiCoreParams.REQUEST_SCHEME, request.RequestUri?.Scheme ?? FastCgiCoreParams.REQUEST_SCHEME_HTTP),

            (FastCgiCoreParams.SERVER_NAME, serverName),
            (FastCgiCoreParams.SERVER_PORT, isHttps? FastCgiCoreParams.SERVER_PORT_443 : FastCgiCoreParams.SERVER_PORT_80),
            (FastCgiCoreParams.SERVER_PROTOCOL, HttpProtocol.GetHttpProtocol(request.Version)),
            (FastCgiCoreParams.SERVER_SOFTWARE, FastCgiCoreParams.SERVER_SOFTWARE_YARP_2),

            (FastCgiCoreParams.SCRIPT_FILENAME, scriptFilename),
            (FastCgiCoreParams.SCRIPT_NAME, scriptName),

            //TODO: would require to have orignal url connection info - but probably they are not needed in most cases
            //(FastCgiCoreParams.SSL_CIPHER, string.Empty), 
            //FastCgiCoreParams.SSL_PROTOCOL, string.Empty),
        };

        if (pathInfo.Length > 0)
        {
            fastCgiParams.Add((FastCgiCoreParams.PATH_TRANSLATED, SanitizedPathJoin(documentRoot, pathInfo)));
        }

        foreach (var header in request.Headers)
        {
            fastCgiParams.Add((header.Key.AddPrefixReplaceToUpper("HTTP_", '-', '_'), string.Join(", ", header.Value)));
        }

        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                fastCgiParams.Add((header.Key.AddPrefixReplaceToUpper("HTTP_", '-', '_'), string.Join(", ", header.Value)));
            }
        }
        return fastCgiParams.AsReadOnly();
    }

    private static string SanitizedPathJoin(string root, string reqPath)
    {
        if (root.Length == 0)
        {
            root = ".";
        }

        // TODO: its good enough for linux / unix but for windows it also needs finding of special devices names
        // https://github.com/golang/go/issues/56336#issuecomment-1416214885
        var relPath = PathClean("/" + reqPath);

        return Path.Join(root, relPath);
    }

    // Copied from: https://cs.opensource.google/go/go/+/refs/tags/go1.23.0:src/path/path.go;l=72
    private static string PathClean(string path)
    {
        if (path.Length == 0)
        {
            return ".";
        }

        var pathChars = path.AsSpan();

        var lb = new LazyBuf(path);

        var (r, dotdot, n, rooted) = (0, 0, path.Length, pathChars[0] == '/');

        if (rooted)
        {
            lb.Append('/');
            (r, dotdot) = (1, 1);
        }

        while (r < n)
        {
            switch (pathChars[r])
            {
                case '/':
                    // empty path element
                    r++;
                    break;
                case '.' when (r + 1 == n || pathChars[r + 1] == '/'):
                    // . element
                    r++;
                    break;
                case '.' when (pathChars[r + 1] == '.' && (r + 2 == n || pathChars[r + 2] == '/')):
                    // .. element: remove to last /
                    r += 2;

                    if (lb.W > dotdot)
                    {
                        // can backtrack
                        lb.W--;
                        while (lb.W > dotdot && lb.Index(lb.W) != '/')
                        {
                            lb.W--;
                        }
                    }
                    else if (!rooted)
                    {
                        // cannot backtrack, but not rooted, so append .. element.
                        if (lb.W > 0)
                        {
                            lb.Append('/');
                        }
                        lb.Append('.');
                        lb.Append('.');
                        dotdot = lb.W;
                    }
                    break;
                default:
                    // real path element.
                    // add slash if needed
                    if (rooted && lb.W != 1 || !rooted && lb.W != 0)
                    {
                        lb.Append('/');
                    }
                    // copy element
                    while (r < n && pathChars[r] != '/')
                    {
                        lb.Append(pathChars[r]);
                        r++;
                    }
                    break;
            }
        }

        if (lb.W == 0)
        {
            return ".";
        }

        return lb.ToString();
    }

    private ref struct LazyBuf(string s)
    {
        private char[]? _rented = default;
        public int W { get; set; } = 0;
        private readonly string _s = s;

        public new readonly string ToString()
        {
            if (_rented == null)
            {
                return _s;
            }
            var v = new string(_rented[..W]);
            ArrayPool<char>.Shared.Return(_rented);
            return v;
        }

        public void Append(char c)
        {
            if (_rented == null)
            {

                if (W < _s.Length && _s[W] == c)
                {
                    W++;
                    return;
                }

                _rented = ArrayPool<char>.Shared.Rent(_s.Length);
                try
                {
                    _s.CopyTo(0, _rented, 0, W);
                }
                catch
                {
                    ArrayPool<char>.Shared.Return(_rented);
                    throw;
                }
            }

            _rented[W] = c;
            W++;
        }

        public readonly char Index(int i)
        {
            if (_rented != null)
            {
                return _rented[W];
            }
            return _s[i];
        }
    }

    private static class FastCgiRecordReader
    {
        public static async Task ProcessAsync(FastCgiRecordHandler handler, ConnectionContext connectionContext, CancellationToken cancellationToken)
        {
            var input = connectionContext.Transport.Input;

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await input.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (result.IsCompleted && buffer.Length == 0)
                {
                    return;
                }

                try
                {
                    while (TryReadRecord(ref buffer, out var record))
                    {
                        try
                        {
                            if (!handler.TryOnFastcgiRecord(ref record))
                            {
                                return;
                            }
                        }
                        catch
                        {
                            record.Dispose();
                            throw;
                        }
                    }
                }
                finally
                {
                    input.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }

        private static bool TryReadRecord(ref ReadOnlySequence<byte> buffer, out FastCgiRecord fastCgiRecord)
        {
            if (!TryReadHeader(buffer, out var header))
            {
                fastCgiRecord = default;
                return false;
            }

            if (header.Version != FastCgiRecordHeader.RecordVersion.Version1)
            {
                FastCgiBadResponseException.Throw(FastCgiResponseRejectionReason.UnrecognizedFastCgiVersion);
            }

            if (header.Type < FastCgiRecordHeader.RecordType.BeginRequest || header.Type > FastCgiRecordHeader.RecordType.UnknownType)
            {
                FastCgiBadResponseException.Throw(FastCgiResponseRejectionReason.UnrecognizedRequestType);
            }

            if (header.ContentLength > FastCgiRecordHeader.MAX_CONTENT_SIZE)
            {
                FastCgiBadResponseException.Throw(FastCgiResponseRejectionReason.InvalidContentLength);

            }

            if (header.PaddingLength > FastCgiRecordHeader.MAX_PADDING_SIZE)
            {
                FastCgiBadResponseException.Throw(FastCgiResponseRejectionReason.InvalidPaddingLength);
            }

            var recordByteCount = FastCgiRecordHeader.FCGI_HEADER_LEN + header.ContentLength + header.PaddingLength;

            if (buffer.Length < recordByteCount)
            {
                fastCgiRecord = default;
                return false;
            }

            if (header.ContentLength == 0)
            {
                fastCgiRecord = new FastCgiRecord(
                    Header: header,
                    ContentData: RentedReadOnlyMemory<byte>.Empty);

                // Advance the buffer
                buffer = buffer.Slice(recordByteCount);
                return true;
            }

            var contentDataBytes = ArrayPool<byte>.Shared.Rent(header.ContentLength);
            var contentData = buffer.Slice(FastCgiRecordHeader.FCGI_HEADER_LEN, header.ContentLength);
            contentData.CopyTo(contentDataBytes);

            fastCgiRecord = new FastCgiRecord(
                Header: header,
                ContentData: new RentedReadOnlyMemory<byte>(contentDataBytes.AsMemory(0, header.ContentLength), contentDataBytes));

            // Advance the buffer
            buffer = buffer.Slice(recordByteCount);

            return true;
        }

        private static bool TryReadHeader(ReadOnlySequence<byte> buffer, out FastCgiRecordHeader header)
        {
            if (buffer.Length < FastCgiRecordHeader.FCGI_HEADER_LEN)
            {
                header = default;
                return false;
            }

            var data = buffer.First.Span;

            header = new FastCgiRecordHeader(
                Version: (FastCgiRecordHeader.RecordVersion)data[0],
                Type: (FastCgiRecordHeader.RecordType)data[1],
                RequestId: BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2, 2)),
                ContentLength: BinaryPrimitives.ReadUInt16BigEndian(data.Slice(4, 2)),
                PaddingLength: data[6],
                Reserved: data[7]);

            return true;
        }
    }

    private struct RentedReadOnlyMemory<T>(ReadOnlyMemory<T> memory, T[]? rented = default) : IDisposable
    {
        public T[]? Rented { get; private set; } = rented;
        public ReadOnlyMemory<T> Memory { get; private set; } = memory;

        public static RentedReadOnlyMemory<T> Empty = new(ReadOnlyMemory<T>.Empty);

        public void Dispose()
        {
            if (Rented is null)
            {
                return;
            }

            ArrayPool<T>.Shared.Return(Rented);

            Rented = default;
            Memory = default;
        }
    }

    private interface FastCgiRecordHandler
    {
        bool TryOnFastcgiRecord(ref FastCgiRecord fastcgiRecord);
    }

    private interface IFastCgiContentDataWriter
    {
        void WriteTo(IBufferWriter<byte> buffer);
    }

    private sealed class HttpResponseReaderFastcgiRecordHandler(HttpResponseMessage result, ILogger logger) : FastCgiRecordHandler
    {
        private readonly HttpResponseReader _reader = new(result);
        private bool _headersDone = false;
        private RentedMemorySegment<byte>? _start;
        private RentedMemorySegment<byte>? _end;
        public bool TryOnFastcgiRecord(ref FastCgiRecord fastcgiRecord)
        {
            switch (fastcgiRecord.Header.Type)
            {
                case FastCgiRecordHeader.RecordType.Stdout when _headersDone:
                    {
                        // TODO: Idea to optimize it further.
                        // After headers are done it could act as stream and lazy parse rest of the data on the fly.
                        // This would lower memory footprint to only 1 record (max 65535).
                        // Stream would stop at EndRequest Record.
                        if (fastcgiRecord.ContentData.Memory.Length == 0)
                        {
                            fastcgiRecord.Dispose();
                            return true;
                        }

                        if (_start is null)
                        {
                            _start = new RentedMemorySegment<byte>(fastcgiRecord.ContentData);
                        }
                        else if (_end is null)
                        {
                            _end = _start.Append(fastcgiRecord.ContentData);
                        }
                        else
                        {
                            _end.Append(fastcgiRecord.ContentData);
                        }

                        return true;
                    }
                case FastCgiRecordHeader.RecordType.Stdout:
                    {
                        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(fastcgiRecord.ContentData.Memory));
                        if (_reader.ParseHttpHeaders(ref reader))
                        {
                            _headersDone = true;

                            var left = fastcgiRecord.ContentData.Memory.Slice((int)reader.Consumed);
                            if (left.Length > 0)
                            {
                                _start = new RentedMemorySegment<byte>(new RentedReadOnlyMemory<byte>(left, fastcgiRecord.ContentData.Rented));
                                return true;
                            }
                        }

                        fastcgiRecord.Dispose();
                        return true;
                    }
                // TODO: how to treat errors - caddy & nginx are just logging them
                // stderr can interleve with stdout records so they do not interrupt connection
                case FastCgiRecordHeader.RecordType.Stderr:
                    {
                        logger.LogError(message: "stdErr {contentData}", Encoding.ASCII.GetString(fastcgiRecord.ContentData.Memory.Span));
                        fastcgiRecord.Dispose();
                        return true;
                    }
                case FastCgiRecordHeader.RecordType.EndRequest:
                    {
                        HttpContent content;
                        if (_start is null)
                        {
                            content = new EmptyHttpContent();
                        }
                        else
                        {
                            var stream = new RentedMemorySegmentStream(_start, _end);
                            content = new StreamContent(stream);
                        }

                        //TODO: content headers are "get" only so they need to be applied / copied to final content
                        //maybe there is another way to do it better?
                        var contentHeaders = result.Content.Headers;
                        foreach (var header in contentHeaders)
                        {
                            var added = content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            Debug.Assert(added);
                        }

                        result.Content = content;

                        fastcgiRecord.Dispose();
                        return false;
                    }
                default:
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Content = new StringContent($"recv unexpected fastcgi record type: {fastcgiRecord.Header.Type}");
                        fastcgiRecord.Dispose();
                        _start?.Dispose();
                        _end?.Dispose();
                        return false;
                    }
            }
        }

        private sealed class HttpResponseReader(HttpResponseMessage response) : IHttpHeadersHandler, IHttpRequestLineHandler
        {
            private readonly HttpParser<HttpResponseReader> _httpParser = new();

            public bool ParseHttpHeaders(ref SequenceReader<byte> reader)
            {
                var result = _httpParser.ParseHeaders(this, ref reader);
                return result;
            }

            public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
            {

                bool added;
                var valueValue = Encoding.ASCII.GetString(value);

                if (HttpResponseHeaders.TryGetStatusHeader(name, out var status))
                {
                    if (value.Length > 2)
                    {
                        var codePart = value.Slice(0, 3);
                        if (int.TryParse(Encoding.ASCII.GetString(codePart), out var code))
                        {
                            response.StatusCode = (HttpStatusCode)code;
                        }
                    }
                    added = response.Headers.TryAddWithoutValidation(status!, valueValue);
                }
                else if (HttpResponseHeaders.TryGetContentHeader(name, out var header))
                {
                    added = response.Content.Headers.TryAddWithoutValidation(header!, valueValue);
                }
                else
                {
                    var nameValue = Encoding.ASCII.GetString(name);
                    added = response.Headers.TryAddWithoutValidation(nameValue, valueValue);
                }

                Debug.Assert(added);
            }

            public void OnHeadersComplete(bool endStream)
            {

            }

            public void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
            {
                throw new NotImplementedException();
            }

            public void OnStaticIndexedHeader(int index)
            {
                throw new NotImplementedException();
            }

            public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value)
            {
                throw new NotImplementedException();
            }
        }
    }

    private sealed class RentedMemorySegment<T> : ReadOnlySequenceSegment<T>, IDisposable
    {
        private RentedReadOnlyMemory<T>? _rented;
        internal RentedMemorySegment(RentedReadOnlyMemory<T> rented)
        {
            Memory = rented.Memory;
            _rented = rented;
        }

        public void Dispose()
        {
            if (_rented is null)
            {
                return;
            }

            if (Next is RentedMemorySegment<T> next)
            {
                next.Dispose();
            }

            _rented.Value.Dispose();
            _rented = default;
        }

        internal RentedMemorySegment<T> Append(RentedReadOnlyMemory<T> memory)
        {
            var segment = new RentedMemorySegment<T>(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };

            Next = segment;

            return segment;
        }
    }

    private readonly record struct FastCgiRecordHeader(
        FastCgiRecordHeader.RecordVersion Version = FastCgiRecordHeader.RecordVersion.Version1,
        FastCgiRecordHeader.RecordType Type = FastCgiRecordHeader.RecordType.BeginRequest,
        ushort RequestId = 1,
        ushort ContentLength = 0,
        byte PaddingLength = 0,
        byte Reserved = 0) : IFastCgiContentDataWriter
    {
        public const uint FCGI_HEADER_LEN = 8;
        public const ushort MAX_CONTENT_SIZE = 65535;
        public const byte MAX_PADDING_SIZE = 255;

        public enum RecordVersion : byte
        {
            Version1 = 1,
        }

        public enum RecordType : byte
        {
            BeginRequest = 1,
            AbortRequest,
            EndRequest,
            Params,
            Stdin,
            Stdout,
            Stderr,
            Data,
            GetValues,
            GetValuesResult,
            UnknownType,
        }

        public readonly RecordVersion Version = Version;
        public readonly RecordType Type = Type;
        public readonly ushort RequestId = RequestId;
        public readonly ushort ContentLength = ContentLength;
        public readonly byte PaddingLength = PaddingLength;
        public readonly byte Reserved = Reserved;

        public void WriteTo(IBufferWriter<byte> buffer)
        {
            var span = buffer.GetSpan((int)FCGI_HEADER_LEN);

            span[0] = (byte)Version;
            span[1] = (byte)Type;
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(2, 2), RequestId);
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(4, 2), ContentLength);
            span[7] = PaddingLength;
            span[8] = Reserved;

            buffer.Advance((int)FCGI_HEADER_LEN);
        }
    }


    private readonly record struct FastCgiRecord(
        FastCgiRecordHeader Header,
        RentedReadOnlyMemory<byte> ContentData) : IDisposable
    {
        public FastCgiRecordHeader Header { get; } = Header;
        public readonly RentedReadOnlyMemory<byte> ContentData = ContentData;

        public void Dispose()
        {
            ContentData.Dispose();
        }
    }

    private readonly record struct FastCgiRecordBeginRequestBody(FastCgiRecordBeginRequestBody.RoleType Role, bool KeepConn) : IFastCgiContentDataWriter
    {
        public const int ByteCount = 8;
        public enum RoleType : ushort
        {
            Responder = 1,
            Authorizer,
            Filter,
        }

        public readonly RoleType Role = Role;
        public readonly bool KeepConn = KeepConn;

        public void WriteTo(IBufferWriter<byte> buffer)
        {
            var span = buffer.GetSpan(ByteCount);

            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(0, 2), (ushort)Role);
            span[2] = (byte)(KeepConn ? 1 : 0);
            span[3] = 0;
            span[4] = 0;
            span[5] = 0;
            span[6] = 0;
            span[7] = 0;

            buffer.Advance(ByteCount);
        }
    }
    private readonly record struct FastCgiRecordParamsBody(ReadOnlyCollection<(string, string)> FastCgiParams) : IFastCgiContentDataWriter
    {
        public readonly ReadOnlyCollection<(string, string)> FastCgiParams = FastCgiParams;

        public readonly uint ByteCount()
        {
            var count = 0;
            foreach (var (key, value) in FastCgiParams)
            {
                count += CalculateParamByteCount((uint)key.Length);
                count += CalculateParamByteCount((uint)value.Length);
                count += key.Length;
                count += value.Length;
            }
            return (uint)count;
        }

        public void WriteTo(IBufferWriter<byte> buffer)
        {
            foreach (var (key, value) in FastCgiParams)
            {
                var written = WriteSize((uint)key.Length, buffer);
                buffer.Advance(written);
                written = WriteSize((uint)value.Length, buffer);
                buffer.Advance(written);
                written = buffer.WriteAsciiString(key);
                buffer.Advance(written);
                written = buffer.WriteAsciiString(value);
                buffer.Advance(written);
            }
        }

        private static int WriteSize(uint size, IBufferWriter<byte> buffer)
        {
            if (size > 127)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer.GetSpan(4), size);
                return 4;
            }
            buffer.GetSpan(1)[0] = (byte)size;
            return 1;
        }

        private static int CalculateParamByteCount(uint size)
        {
            if (size > 127)
            {
                return 4;
            }
            return 1;
        }
    }

    private static class FastCgiCoreParams
    {
        internal static string CONTENT_LENGTH = "CONTENT_LENGTH";
        internal static string CONTENT_TYPE = "CONTENT_TYPE";

        internal static string DOCUMENT_ROOT = "DOCUMENT_ROOT";
        internal static string DOCUMENT_URI = "DOCUMENT_URI";

        internal static string GATEWAY_INTERFACE = "GATEWAY_INTERFACE";
        internal static string GATEWAY_INTERFACE_CGI11 = "CGI/1.1";

        internal static string HTTPS = "HTTPS";
        internal static string HTTPS_ON = "ON";
        internal static string HTTPS_OFF = "OFF";

        internal static string PATH_INFO = "PATH_INFO";
        internal static string PATH_TRANSLATED = "PATH_TRANSLATED";

        internal static string QUERY_STRING = "QUERY_STRING";

        internal static string REMOTE_ADDR = "REMOTE_ADDR";
        internal static string REMOTE_HOST = "REMOTE_HOST";
        internal static string REMOTE_PORT = "REMOTE_PORT";
        internal static string REMOTE_USER = "REMOTE_USER";

        internal static string REQUEST_METHOD = "REQUEST_METHOD";

        internal static string REQUEST_SCHEME = "REQUEST_SCHEME";
        internal static string REQUEST_SCHEME_HTTP = "HTTP";

        internal static string REQUEST_URI = "REQUEST_URI";

        internal static string SERVER_NAME = "SERVER_NAME";

        internal static string SCRIPT_FILENAME = "SCRIPT_FILENAME";
        internal static string SCRIPT_NAME = "SCRIPT_NAME";

        internal static string SERVER_PORT = "SERVER_PORT";
        internal static string SERVER_PORT_80 = "80";
        internal static string SERVER_PORT_443 = "443";

        internal static string SERVER_PROTOCOL = "SERVER_PROTOCOL";

        internal static string SERVER_SOFTWARE = "SERVER_SOFTWARE";
        internal static string SERVER_SOFTWARE_YARP_2 = "YARP2";

        internal static string SSL_PROTOCOL = "SSL_PROTOCOL";
        internal static string SSL_CIPHER = "SSL_CIPHER";
    }

    public static class FastCgiHttpOptions
    {
        public static readonly HttpRequestOptionsKey<string> DOCUMENT_ROOT = new("DOCUMENT_ROOT");
    }

    private static class FastCgiBadResponseException
    {
        [StackTraceHidden]
        internal static void Throw(FastCgiResponseRejectionReason reason)
        {
            throw GetException(reason);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static BadFastCgiResponseException GetException(FastCgiResponseRejectionReason reason)
        {
            BadFastCgiResponseException ex;
            switch (reason)
            {
                case FastCgiResponseRejectionReason.UnrecognizedFastCgiVersion:
                    ex = new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_UnrecognizedFastCgiVersion, reason);
                    break;
                case FastCgiResponseRejectionReason.UnrecognizedRequestType:
                    ex = new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_UnrecognizedRequestType, reason);
                    break;
                case FastCgiResponseRejectionReason.InvalidContentLength:
                    ex = new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_InvalidContentLength, reason);
                    break;
                case FastCgiResponseRejectionReason.InvalidPaddingLength:
                    ex = new BadFastCgiResponseException(FastCgiCoreExpStrings.BadRequest_InvalidPaddingLength, reason);
                    break;
                default:
                    ex = new BadFastCgiResponseException(FastCgiCoreExpStrings.BadRequest);
                    break;
            }
            return ex;
        }
    }

    private enum FastCgiResponseRejectionReason
    {
        UnrecognizedFastCgiVersion,
        UnrecognizedRequestType,
        InvalidContentLength,
        InvalidPaddingLength,
    }

    private sealed class BadFastCgiResponseException : IOException
    {
        public BadFastCgiResponseException(string? message) : base(message)
        {
        }

        internal BadFastCgiResponseException(string message, FastCgiResponseRejectionReason reason)
                    : base(message)
        {
            Reason = reason;
        }

        internal FastCgiResponseRejectionReason Reason { get; }
    }


    private static class FastCgiCoreExpStrings
    {
        internal static string BadRequest = "BadRequest";
        internal static string BadResponse_UnrecognizedFastCgiVersion = "BadRequest_UnrecognizedFastCgiVersion";
        internal static string BadResponse_UnrecognizedRequestType = "BadRequest_UnrecognizedRequestType";
        internal static string BadResponse_InvalidContentLength = "BadRequest_InvalidContentLength";
        internal static string BadRequest_InvalidPaddingLength = "BadRequest_InvalidPaddingLength";
    }

    // COPIED FROM https://github.com/dotnet/Nerdbank.Streams/blob/main/src/Nerdbank.Streams/ReadOnlySequenceStream.cs
    // Copyright (c) Andrew Arnott. All rights reserved.
    // Licensed under the MIT license. See LICENSE file in the project root for full license information.
    private class RentedMemorySegmentStream : Stream
    {
        private static readonly Task<int> _taskOfZero = Task.FromResult(0);
        private RentedMemorySegment<byte>? _start;

        /// <summary>
        /// A reusable task if two consecutive reads return the same number of bytes.
        /// </summary>
        private Task<int>? _lastReadTask;

        private readonly ReadOnlySequence<byte> _readOnlySequence;

        private SequencePosition _position;

        internal RentedMemorySegmentStream(RentedMemorySegment<byte> start, RentedMemorySegment<byte>? end = default)
        {
            if (end is not null)
            {
                _readOnlySequence = new ReadOnlySequence<byte>(start, 0, end, end.Memory.Length);

            }
            else
            {
                _readOnlySequence = new ReadOnlySequence<byte>(start.Memory);
            }
            _position = _readOnlySequence.Start;
            _start = start;
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                if (_start != null)
                {
                    _start.Dispose();
                    _start = null;
                }
            }
            base.Dispose(disposing);
        }

        public override bool CanRead => !IsDisposed;

        public override bool CanSeek => !IsDisposed;

        public override bool CanWrite => false;

        public override long Length => _readOnlySequence.Length;

        public override long Position
        {
            get => _readOnlySequence.Slice(0, _position).Length;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _position = _readOnlySequence.GetPosition(value, _readOnlySequence.Start);
            }
        }

        public bool IsDisposed { get; private set; }

        public override void Flush() => throw new NotSupportedException();

        public override Task FlushAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _readOnlySequence.Slice(_position);
            var toCopy = remaining.Slice(0, Math.Min(count, remaining.Length));
            _position = toCopy.End;
            toCopy.CopyTo(buffer.AsSpan(offset, count));
            return (int)toCopy.Length;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytesRead = Read(buffer, offset, count);
            if (bytesRead == 0)
            {
                return _taskOfZero;
            }

            if (_lastReadTask?.Result == bytesRead)
            {
                return _lastReadTask;
            }
            else
            {
                return _lastReadTask = Task.FromResult(bytesRead);
            }
        }

        public override int ReadByte()
        {
            var remaining = _readOnlySequence.Slice(_position);
            if (remaining.Length > 0)
            {
                var result = remaining.First.Span[0];
                _position = _readOnlySequence.GetPosition(1, _position);
                return result;
            }
            else
            {
                return -1;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            SequencePosition relativeTo;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    relativeTo = _readOnlySequence.Start;
                    break;
                case SeekOrigin.Current:
                    if (offset >= 0)
                    {
                        relativeTo = _position;
                    }
                    else
                    {
                        relativeTo = _readOnlySequence.Start;
                        offset += Position;
                    }

                    break;
                case SeekOrigin.End:
                    if (offset >= 0)
                    {
                        relativeTo = _readOnlySequence.End;
                    }
                    else
                    {
                        relativeTo = _readOnlySequence.Start;
                        offset += Length;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            _position = _readOnlySequence.GetPosition(offset, relativeTo);
            return Position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void WriteByte(byte value) => throw new NotSupportedException();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            foreach (var segment in _readOnlySequence)
            {
                await destination.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
            }
        }


        public override int Read(Span<byte> buffer)
        {
            var remaining = _readOnlySequence.Slice(_position);
            var toCopy = remaining.Slice(0, Math.Min(buffer.Length, remaining.Length));
            _position = toCopy.End;
            toCopy.CopyTo(buffer);
            return (int)toCopy.Length;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<int>(Read(buffer.Span));
        }

        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static class HttpResponseHeaders
    {
        public static readonly string Status = "Status";
        public static unsafe bool TryGetStatusHeader(ReadOnlySpan<byte> name, out string? header)
        {
            ref var nameStart = ref MemoryMarshal.GetReference(name);

            if (name.Length == 6 && ((ReadUnalignedLittleEndian_uint(ref nameStart) & 0xdfdfdfdfu) == 0x54415453u) && ((ReadUnalignedLittleEndian_ushort(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(2 * sizeof(ushort)))) & 0xdfdfu) == 0x5355u))
            {
                header = Status;
                return true;
            }
            header = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe bool TryGetContentHeader(ReadOnlySpan<byte> name, out string? header)
        {
            ref var nameStart = ref MemoryMarshal.GetReference(name);

            switch (name.Length)
            {
                case 5:
                    if (((ReadUnalignedLittleEndian_uint(ref nameStart) & 0xdfdfdfdfu) == 0x4f4c4c41u) && ((Unsafe.AddByteOffset(ref nameStart, (IntPtr)4) & 0xdfu) == 0x57u))
                    {
                        header = HeaderNames.Allow;
                        return true;
                    }

                    break;
                case 7:
                    if (((ReadUnalignedLittleEndian_uint(ref nameStart) & 0xdfdfdfdfu) == 0x49505845u) && ((ReadUnalignedLittleEndian_ushort(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(2 * sizeof(ushort)))) & 0xdfdfu) == 0x4552u) && ((Unsafe.AddByteOffset(ref nameStart, (IntPtr)6) & 0xdfu) == 0x53u))
                    {
                        header = HeaderNames.Expires;
                        return true;
                    }

                    break;
                case 11:
                    if (((ReadUnalignedLittleEndian_ulong(ref nameStart) & 0xdfdfdfdfdfdfdfdfuL) == 0xd544e45544e4f43uL) && ((ReadUnalignedLittleEndian_ushort(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(4 * sizeof(ushort)))) & 0xdfdfu) == 0x444du) && ((Unsafe.AddByteOffset(ref nameStart, (IntPtr)10) & 0xdfu) == 0x15u))
                    {
                        header = HeaderNames.ContentMD5;
                        return true;
                    }
                    break;
                case 12:
                    if (((ReadUnalignedLittleEndian_ulong(ref nameStart) & 0xffdfdfdfdfdfdfdfuL) == 0x2d544e45544e4f43uL) && ((ReadUnalignedLittleEndian_uint(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(2 * sizeof(uint)))) & 0xdfdfdfdfu) == 0x45505954u))
                    {
                        header = HeaderNames.ContentType;
                        return true;
                    }
                    break;
                case 13:
                    var firstTerm13 = (ReadUnalignedLittleEndian_ulong(ref nameStart) & 0xdfdfdfdfdfdfdfdfuL);
                    if ((firstTerm13 == 0x444f4d0d5453414cuL) && ((ReadUnalignedLittleEndian_uint(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(2 * sizeof(uint)))) & 0xdfdfdfdfu) == 0x45494649u) && ((Unsafe.AddByteOffset(ref nameStart, (IntPtr)12) & 0xdfu) == 0x44u))
                    {
                        header = HeaderNames.LastModified;
                        return true;
                    }
                    else if ((firstTerm13 == 0xd544e45544e4f43uL) && ((ReadUnalignedLittleEndian_uint(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(2 * sizeof(uint)))) & 0xdfdfdfdfu) == 0x474e4152u) && ((Unsafe.AddByteOffset(ref nameStart, (IntPtr)12) & 0xdfu) == 0x45u))
                    {
                        header = HeaderNames.ContentRange;
                        return true;
                    }
                    break;
                case 14:
                    if (((ReadUnalignedLittleEndian_ulong(ref nameStart) & 0xffdfdfdfdfdfdfdfuL) == 0x2d544e45544e4f43uL) && ((ReadUnalignedLittleEndian_uint(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(2 * sizeof(uint)))) & 0xdfdfdfdfu) == 0x474e454cu) && ((ReadUnalignedLittleEndian_ushort(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(6 * sizeof(ushort)))) & 0xdfdfu) == 0x4854u))
                    {
                        header = HeaderNames.ContentLength;
                        return true;
                    }
                    break;
                case 16:
                    var firstTerm16 = (ReadUnalignedLittleEndian_ulong(ref nameStart) & 0xdfdfdfdfdfdfdfdfuL);

                    if ((firstTerm16 == 0xd544e45544e4f43uL) && ((ReadUnalignedLittleEndian_ulong(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)sizeof(ulong))) & 0xdfdfdfdfdfdfdfdfuL) == 0x474e49444f434e45uL))
                    {
                        header = HeaderNames.ContentEncoding;
                        return true;
                    }
                    else if ((firstTerm16 == 0xd544E45544e4f43uL) && ((ReadUnalignedLittleEndian_ulong(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)sizeof(ulong))) & 0xdfdfdfdfdfdfdfdfuL) == 0x45474155474e414cuL))
                    {
                        header = HeaderNames.ContentLanguage;
                        return true;
                    }
                    else if ((firstTerm16 == 0xd544e45544e4f43uL) && ((ReadUnalignedLittleEndian_ulong(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)sizeof(ulong))) & 0xdfdfdfdfdfdfdfdfuL) == 0x4e4f495441434f4cuL))
                    {
                        header = HeaderNames.ContentLocation;
                        return true;
                    }
                    break;
                case 19:
                    if (((ReadUnalignedLittleEndian_ulong(ref nameStart) & 0xdfdfdfdfdfdfdfdfuL) == 0xd544e45544e4f43uL) && ((ReadUnalignedLittleEndian_ulong(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)sizeof(ulong))) & 0xdfdfdfdfffdfdfdfuL) == 0x5449534f70534944uL) && ((ReadUnalignedLittleEndian_ushort(ref Unsafe.AddByteOffset(ref nameStart, (IntPtr)(8 * sizeof(ushort)))) & 0xdfdfu) == 0x4f49u) && ((Unsafe.AddByteOffset(ref nameStart, (IntPtr)18) & 0xdfu) == 0x4eu))
                    {
                        header = HeaderNames.ContentDisposition;
                        return true;
                    }
                    break;
            }
            header = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint ReadUnalignedLittleEndian_uint(ref byte source)
        {
            var result = Unsafe.ReadUnaligned<uint>(ref source);
            if (!BitConverter.IsLittleEndian)
            {
                result = BinaryPrimitives.ReverseEndianness(result);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ushort ReadUnalignedLittleEndian_ushort(ref byte source)
        {
            var result = Unsafe.ReadUnaligned<ushort>(ref source);
            if (!BitConverter.IsLittleEndian)
            {
                result = BinaryPrimitives.ReverseEndianness(result);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ulong ReadUnalignedLittleEndian_ulong(ref byte source)
        {
            var result = Unsafe.ReadUnaligned<ulong>(ref source);
            if (!BitConverter.IsLittleEndian)
            {
                result = BinaryPrimitives.ReverseEndianness(result);
            }
            return result;
        }
    }
}

internal static class FastCgiExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfCanceled(this FlushResult result)
    {
        if (result.IsCanceled)
        {
            throw new OperationCanceledException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string AddPrefixReplaceToUpper(this string s, string prefix, char oldChar, char newChar)
    {
        var chars = ArrayPool<char>.Shared.Rent(s.Length + prefix.Length);
        try
        {
            prefix.CopyTo(0, chars, 0, prefix.Length);

            var i = prefix.Length;
            foreach (var c in s)
            {
                if (c == oldChar)
                {
                    chars[i] = newChar;
                }
                else
                {
                    chars[i] = char.ToUpper(c);
                }
                i++;
            }
            return new string(chars, 0, s.Length + prefix.Length);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteAsciiString(this IBufferWriter<byte> buffer, string s)
    {
        // works only in .net 8
        // Debug.Assert(Ascii.IsValid(s));
        // var status = Ascii.FromUtf16(s, span, out int bytesWritten);
        // Debug.Assert(status == OperationStatus.Done);
        var span = buffer.GetSpan(s.Length);

        // TODO: kestrel has implemented better conversions compatibile with 6,7
        var bytesWritten = Encoding.ASCII.GetBytes(s, span);

        Debug.Assert(bytesWritten == s.Length);

        return bytesWritten;
    }
}

