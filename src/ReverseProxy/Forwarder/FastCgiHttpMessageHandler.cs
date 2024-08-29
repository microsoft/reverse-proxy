// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
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

    private static readonly HashSet<string> _skipHeaders = new(StringComparer.OrdinalIgnoreCase) { "Content-Length", "Content-Type" };

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

        Send(
            new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.BeginRequest, ContentLength: FastCgiRecordBeginRequestBody.ByteCount),
            new FastCgiRecordBeginRequestBody(Role: FastCgiRecordBeginRequestBody.RoleType.Responder, KeepConn: false),
            connection);

        using var paramBuffer = new RentedArrayBufferWriter<byte>(FastCgiRecordHeader.MAX_CONTENT_SIZE);
        {
            foreach (var fcgiParam in BuildFastCgiParams(connection.RemoteEndPoint, request))
            {
                Debug.Assert(fcgiParam.ByteCount <= paramBuffer.Capacity);

                if (paramBuffer.WrittenCount + fcgiParam.ByteCount > paramBuffer.Capacity)
                {

                    await SendAsync(
                        new FastCgiRecord(
                            Header: new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Params, ContentLength: (ushort)paramBuffer.WrittenCount),
                            ContentData: new RentedReadOnlyMemory<byte>(paramBuffer.GetWrittenMemory())),
                        connection, cancellationToken: cancellationToken);

                    paramBuffer.Clear();
                }

                fcgiParam.CopyTo(paramBuffer);
            }

            await SendAsync(
                new FastCgiRecord(
                    Header: new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Params, ContentLength: (ushort)paramBuffer.WrittenCount),
                    ContentData: new RentedReadOnlyMemory<byte>(paramBuffer.GetWrittenMemory())),
                connection, cancellationToken: cancellationToken);
        }


        await SendAsync(
            new FastCgiRecord(
                Header: new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Params),
                ContentData: RentedReadOnlyMemory<byte>.Empty),
            connection, cancellationToken);

        await FlushAsync(connection, cancellationToken);

        if (request.Content != null)
        {
            await using var stream = new FastCgiStdinStream(connection);
            await request.Content.CopyToAsync(stream, cancellationToken);
        }

        await SendAsync(
            new FastCgiRecord(
                Header: new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Stdin),
                ContentData: RentedReadOnlyMemory<byte>.Empty),
            connection, cancellationToken);

        await FlushAsync(connection, cancellationToken);

        var httpResponseReader = new HttpResponseReaderFastCgiRecordHandler(request, logger);

        try
        {
            await FastCgiRecordReader.ProcessAsync(httpResponseReader, connection, cancellationToken);
            return httpResponseReader.Result;
        }
        catch (BadFastCgiResponseException ex)
        {
            throw new HttpRequestException("Could not process FastCGI Response", ex, HttpStatusCode.InternalServerError);
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

    private static async ValueTask SendAsync(FastCgiRecord fastcgiRecord, ConnectionContext connectionContext, CancellationToken cancellationToken)
    {
        fastcgiRecord.Header.CopyTo(connectionContext.Transport.Output);

        var result = await connectionContext.Transport.Output.WriteAsync(fastcgiRecord.ContentData.Memory, cancellationToken);
        result.ThrowIfCanceled();
    }

    private static void Send(FastCgiRecordHeader header, IFastCgiContentData contentData, ConnectionContext connectionContext)
    {
        header.CopyTo(connectionContext.Transport.Output);
        contentData.CopyTo(connectionContext.Transport.Output);
    }

    private static void Send(FastCgiRecordHeader header, ReadOnlySpan<byte> contentData, ConnectionContext connectionContext)
    {
        header.CopyTo(connectionContext.Transport.Output);
        connectionContext.Transport.Output.Write(contentData);
    }

    private static async ValueTask FlushAsync(ConnectionContext connectionContext, CancellationToken cancellationToken)
    {
        var result = await connectionContext.Transport.Output.FlushAsync(cancellationToken);
        result.ThrowIfCanceled();
    }

    private interface FastCgiRecordHandler
    {
        bool TryOnFastCgiRecord(ref FastCgiRecord fastCgiRecord);
        void OnEndOfData();
    }

    private interface IFastCgiContentData
    {
        void CopyTo(IBufferWriter<byte> buffer);
    }

    private static class FastCgiRecordReader
    {
        public static async Task ProcessAsync(FastCgiRecordHandler handler, ConnectionContext connectionContext, CancellationToken cancellationToken)
        {
            var input = connectionContext.Transport.Input;

            long? prevBuffLenOnCompleted = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await input.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (result.IsCompleted)
                {
                    if (buffer.Length == 0)
                    {
                        handler.OnEndOfData();
                        return;
                    }

                    if (prevBuffLenOnCompleted is {} prevLen && prevLen == buffer.Length)
                    {
                        throw new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_IncompleteRecord, FastCgiResponseRejectionReason.IncompleteRecord);
                    }

                    prevBuffLenOnCompleted = buffer.Length;
                }

                try
                {
                    while (TryReadRecord(ref buffer, out var record))
                    {
                        try
                        {
                            if (!handler.TryOnFastCgiRecord(ref record))
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
                throw new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_UnrecognizedFastCgiVersion, FastCgiResponseRejectionReason.UnrecognizedFastCgiVersion);
            }

            if (header.Type < FastCgiRecordHeader.RecordType.BeginRequest || header.Type > FastCgiRecordHeader.RecordType.UnknownType)
            {
                throw new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_UnrecognizedRequestType, FastCgiResponseRejectionReason.UnrecognizedRequestType);
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

    private sealed class HttpResponseReaderFastCgiRecordHandler : FastCgiRecordHandler
    {
        public readonly HttpResponseMessage Result;
        private readonly HttpResponseReader _reader;
        private readonly ILogger _logger;

        private StateType _state = StateType.Headers;
        private RentedMemorySegment<byte>? _start;
        private RentedMemorySegment<byte>? _end;

        private enum StateType
        {
            Headers,
            Body,
            Finished,
        }

        public HttpResponseReaderFastCgiRecordHandler(HttpRequestMessage request, ILogger logger)
        {
            Result = new HttpResponseMessage { RequestMessage = request, StatusCode = HttpStatusCode.OK };
            _reader = new HttpResponseReader(Result);
            _logger = logger;
        }

        public bool TryOnFastCgiRecord(ref FastCgiRecord fastCgiRecord)
        {
            switch (fastCgiRecord.Header.Type)
            {
                case FastCgiRecordHeader.RecordType.Stdout when _state == StateType.Body:
                    {
                        // TODO: Idea to optimize it further.
                        // After headers are done it could act as stream and lazy parse rest of the data on the fly.
                        // This would lower memory footprint to only 1 record (max 65535).
                        // Stream would stop at EndRequest Record.
                        if (fastCgiRecord.ContentData.Memory.Length == 0)
                        {
                            fastCgiRecord.Dispose();
                            return true;
                        }

                        if (_start is null)
                        {
                            _start = new RentedMemorySegment<byte>(fastCgiRecord.ContentData);
                        }
                        else if (_end is null)
                        {
                            _end = _start.Append(fastCgiRecord.ContentData);
                        }
                        else
                        {
                            _end.Append(fastCgiRecord.ContentData);
                        }

                        return true;
                    }
                case FastCgiRecordHeader.RecordType.Stdout when _state == StateType.Headers:
                    {
                        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(fastCgiRecord.ContentData.Memory));
                        if (_reader.ParseHttpHeaders(ref reader))
                        {
                            _state = StateType.Body;

                            var left = fastCgiRecord.ContentData.Memory.Slice((int)reader.Consumed);
                            if (left.Length > 0)
                            {
                                _start = new RentedMemorySegment<byte>(new RentedReadOnlyMemory<byte>(left, fastCgiRecord.ContentData.Rented));
                                return true;
                            }
                        }

                        fastCgiRecord.Dispose();
                        return true;
                    }
                // TODO: how to treat errors - caddy & nginx are just logging them
                // stderr can interleave with stdout records so they do not interrupt connection
                case FastCgiRecordHeader.RecordType.Stderr:
                    {
                        _logger.LogError(message: "stdErr {contentData}", Encoding.ASCII.GetString(fastCgiRecord.ContentData.Memory.Span));
                        fastCgiRecord.Dispose();
                        return true;
                    }
                case FastCgiRecordHeader.RecordType.EndRequest when _state == StateType.Body:
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
                        var contentHeaders = Result.Content.Headers;
                        foreach (var header in contentHeaders)
                        {
                            var added = content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            Debug.Assert(added);
                        }

                        Result.Content = content;

                        fastCgiRecord.Dispose();
                        _state = StateType.Finished;
                        return false;
                    }
                default:
                    {
                        fastCgiRecord.Dispose();
                        _start?.Dispose();
                        _end?.Dispose();
                        _logger.LogDebug(message: "received unexpected fastcgi record {recordType}", fastCgiRecord.Header.Type);
                        throw new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_UnexpectedRecord, FastCgiResponseRejectionReason.UnexpectedRecord);
                    }
            }
        }

        public void OnEndOfData()
        {

            if (_state == StateType.Finished) { return;}
            _logger.LogDebug(message: "response finished when in wrong {state}", _state);
            throw new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_EndRequestNotReceived, FastCgiResponseRejectionReason.EndRequestNotReceived);
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
                        if (!int.TryParse(Encoding.ASCII.GetString(codePart), out var code))
                        {
                            throw new BadFastCgiResponseException(FastCgiCoreExpStrings.BadResponse_UnrecognizedStatusCode, FastCgiResponseRejectionReason.UnrecognizedStatusCode);
                        }
                        response.StatusCode = (HttpStatusCode)code;
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


    private static IEnumerable<FastCgiParam> BuildFastCgiParams(EndPoint? connRemoteEndpoint, HttpRequestMessage request)
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

        if (!request.Options.TryGetValue(FastCgiHttpOptions.SCRIPT_FILENAME, out var scriptFilename))
        {
            scriptFilename = string.Empty;
        }

        var fpath = request.RequestUri?.LocalPath ?? string.Empty;

        var scriptName = fpath;
        // Ensure the SCRIPT_NAME has a leading slash for compliance with RFC3875
        // Info: https://tools.ietf.org/html/rfc3875#section-4.1.13
        if (scriptName.Length > 0 && !scriptName.StartsWith('/'))
        {
            scriptName = $"/{scriptName}";
        }

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
        var isHttps = request.RequestUri?.Scheme == "https";

        if (request.Content?.Headers.ContentLength is { } cl)
        {
            yield return new(FastCgiCoreParams.CONTENT_LENGTH, cl.ToString());
        }

        if (request.Content?.Headers.ContentType is { } ct)
        {
            yield return new(FastCgiCoreParams.CONTENT_TYPE, ct.ToString());
        }

        yield return new(FastCgiCoreParams.DOCUMENT_ROOT, documentRoot);
        yield return new(FastCgiCoreParams.DOCUMENT_URI, fpath);

        yield return new(FastCgiCoreParams.GATEWAY_INTERFACE, FastCgiCoreParamValues.GATEWAY_INTERFACE_CGI11);

        yield return new(FastCgiCoreParams.HTTPS, isHttps ? FastCgiCoreParamValues.HTTPS_ON : FastCgiCoreParamValues.HTTPS_OFF);

        yield return new(FastCgiCoreParams.QUERY_STRING, request.RequestUri?.Query ?? string.Empty);

        yield return new(FastCgiCoreParams.REMOTE_ADDR, remoteEndpoint);
        yield return new(FastCgiCoreParams.REMOTE_PORT, remotePort);

        yield return new(FastCgiCoreParams.REQUEST_METHOD, request.Method.Method);
        yield return new(FastCgiCoreParams.REQUEST_SCHEME, request.RequestUri?.Scheme ?? FastCgiCoreParamValues.REQUEST_SCHEME_HTTP);
        yield return new(FastCgiCoreParams.REQUEST_URI, request.RequestUri?.AbsoluteUri ?? string.Empty);

        yield return new(FastCgiCoreParams.SCRIPT_FILENAME, scriptFilename);
        yield return new(FastCgiCoreParams.SCRIPT_NAME, scriptName);

        yield return new(FastCgiCoreParams.SERVER_NAME, serverName);
        yield return new(FastCgiCoreParams.SERVER_PORT, isHttps ? FastCgiCoreParamValues.SERVER_PORT_443 : FastCgiCoreParamValues.SERVER_PORT_80);
        yield return new(FastCgiCoreParams.SERVER_PROTOCOL, HttpProtocol.GetHttpProtocol(request.Version));
        yield return new(FastCgiCoreParams.SERVER_SOFTWARE, FastCgiCoreParamValues.SERVER_SOFTWARE_YARP_2);

        foreach (var header in request.Headers)
        {
            foreach (var value in header.Value)
            {
                yield return new(header.Key.ReplaceToUpperAscii('-', '_'), value, FastCgiCoreParams.PREFIX);
            }
        }

        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                if (_skipHeaders.Contains(header.Key)) { continue; }

                foreach (var value in header.Value)
                {
                    yield return new(header.Key.ReplaceToUpperAscii('-', '_'), value, FastCgiCoreParams.PREFIX);
                }
            }
        }
    }

        private readonly record struct FastCgiRecordHeader(
        FastCgiRecordHeader.RecordVersion Version = FastCgiRecordHeader.RecordVersion.Version1,
        FastCgiRecordHeader.RecordType Type = FastCgiRecordHeader.RecordType.BeginRequest,
        ushort RequestId = 1,
        ushort ContentLength = 0,
        byte PaddingLength = 0,
        byte Reserved = 0) : IFastCgiContentData
    {
        public const uint FCGI_HEADER_LEN = 8;
        public const ushort MAX_CONTENT_SIZE = 65535;
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

        public void CopyTo(IBufferWriter<byte> buffer)
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
        public RentedReadOnlyMemory<byte> ContentData { get; } = ContentData ;

        public void Dispose()
        {
            ContentData.Dispose();
        }
    }

    private readonly record struct FastCgiRecordBeginRequestBody(FastCgiRecordBeginRequestBody.RoleType Role, bool KeepConn) : IFastCgiContentData
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

        public void CopyTo(IBufferWriter<byte> buffer)
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

    private readonly record struct FastCgiParam(byte[] Key, string Value, byte[]? KeyPrefix = default) : IFastCgiContentData
    {
        public readonly int ByteCount =
            CalculateParamByteCount((KeyPrefix?.Length ?? 0) + Key.Length)
            + CalculateParamByteCount(Value.Length)
            + (KeyPrefix?.Length ?? 0) + Key.Length + Value.Length;

        private static int CalculateParamByteCount(int size)
        {
            if (size > 127)
            {
                return 4;
            }
            return 1;
        }

        public readonly void CopyTo(IBufferWriter<byte> buffer)
        {
            var written = WriteSize((uint)Key.Length + (uint)(KeyPrefix?.Length ?? 0), buffer);
            buffer.Advance(written);
            written = WriteSize((uint)Value.Length, buffer);
            buffer.Advance(written);
            if (KeyPrefix != null)
            {
                buffer.Write(new(KeyPrefix));
            }
            buffer.Write(new(Key));
            written = buffer.WriteAsciiString(Value);
            buffer.Advance(written);
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
    };

    private static class FastCgiCoreParams
    {
        internal static readonly byte[] CONTENT_LENGTH = "CONTENT_LENGTH"u8.ToArray();
        internal static readonly byte[] CONTENT_TYPE = "CONTENT_TYPE"u8.ToArray();

        internal static readonly byte[] DOCUMENT_ROOT = "DOCUMENT_ROOT"u8.ToArray();
        internal static readonly byte[] DOCUMENT_URI = "DOCUMENT_URI"u8.ToArray();

        internal static readonly byte[] GATEWAY_INTERFACE = "GATEWAY_INTERFACE"u8.ToArray();

        internal static readonly byte[] HTTPS = "HTTPS"u8.ToArray();

        internal static readonly byte[] QUERY_STRING = "QUERY_STRING"u8.ToArray();

        internal static readonly byte[] REMOTE_ADDR = "REMOTE_ADDR"u8.ToArray();
        internal static readonly byte[] REMOTE_PORT = "REMOTE_PORT"u8.ToArray();

        internal static readonly byte[] REQUEST_METHOD = "REQUEST_METHOD"u8.ToArray();
        internal static readonly byte[] REQUEST_SCHEME = "REQUEST_SCHEME"u8.ToArray();
        internal static readonly byte[] REQUEST_URI = "REQUEST_URI"u8.ToArray();

        internal static readonly byte[] SCRIPT_FILENAME = "SCRIPT_FILENAME"u8.ToArray();
        internal static readonly byte[] SCRIPT_NAME = "SCRIPT_NAME"u8.ToArray();

        internal static readonly byte[] SERVER_NAME = "SERVER_NAME"u8.ToArray();
        internal static readonly byte[] SERVER_PORT = "SERVER_PORT"u8.ToArray();
        internal static readonly byte[] SERVER_PROTOCOL = "SERVER_PROTOCOL"u8.ToArray();
        internal static readonly byte[] SERVER_SOFTWARE = "SERVER_SOFTWARE"u8.ToArray();

        internal static readonly byte[] PREFIX = "HTTP_"u8.ToArray();
    }

    private static class FastCgiCoreParamValues
    {
        internal static readonly string GATEWAY_INTERFACE_CGI11 = "CGI/1.1";

        internal static readonly string HTTPS_ON = "ON";
        internal static readonly string HTTPS_OFF = "OFF";

        internal static readonly string REQUEST_SCHEME_HTTP = "HTTP";

        internal static readonly string SERVER_PORT_80 = "80";
        internal static readonly string SERVER_PORT_443 = "443";

        internal static readonly string SERVER_SOFTWARE_YARP_2 = "YARP2";
    }

    public static class FastCgiHttpOptions
    {
        public static readonly HttpRequestOptionsKey<string> DOCUMENT_ROOT = new("DOCUMENT_ROOT");
        public static readonly HttpRequestOptionsKey<string> SCRIPT_FILENAME = new("SCRIPT_FILENAME");
    }

    private enum FastCgiResponseRejectionReason
    {
        UnrecognizedFastCgiVersion,
        UnrecognizedRequestType,
        UnrecognizedStatusCode,
        UnexpectedRecord,
        EndRequestNotReceived,
        IncompleteRecord,
    }

    private sealed class BadFastCgiResponseException : IOException
    {
        internal BadFastCgiResponseException(string message, FastCgiResponseRejectionReason reason)
                    : base(message)
        {
            Reason = reason;
        }

        internal FastCgiResponseRejectionReason Reason { get; }
    }


    private static class FastCgiCoreExpStrings
    {
        internal static readonly string BadResponse_UnrecognizedFastCgiVersion = "BadResponse_UnrecognizedFastCgiVersion";
        internal static readonly string BadResponse_UnrecognizedRequestType = "BadResponse_UnrecognizedRequestType";
        internal static readonly string BadResponse_UnrecognizedStatusCode = "BadResponse_UnrecognizedStatusCode";
        internal static readonly string BadResponse_UnexpectedRecord = "BadResponse_UnexpectedRecord";
        internal static readonly string BadResponse_EndRequestNotReceived = "BadResponse_EndRequestNotReceived";
        internal static readonly string BadResponse_IncompleteRecord = "BadResponse_IncompleteRecord";
    }

        private struct RentedReadOnlyMemory<T>(ReadOnlyMemory<T> memory, T[]? rented = default) : IDisposable
    {
        public T[]? Rented { get; private set; } = rented;
        public ReadOnlyMemory<T> Memory { get; private set; } = memory;

        public static readonly RentedReadOnlyMemory<T> Empty = new(ReadOnlyMemory<T>.Empty);

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


    private sealed class RentedArrayBufferWriter<T>(int maxCapacity) : IBufferWriter<T>, IDisposable
    {
        private T[]? _rented = ArrayPool<T>.Shared.Rent(maxCapacity);

        public readonly int Capacity = maxCapacity;
        public int WrittenCount { get; private set; }

        public ReadOnlyMemory<T> GetWrittenMemory() { return _rented.AsMemory(0, WrittenCount); }

        public void Advance(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            ThrowIfNotEnoughSpace(count);
            WrittenCount += count;
        }

        public void Clear()
        {
            WrittenCount = 0;
        }

        public void Dispose()
        {
            if (_rented != null)
            {
                ArrayPool<T>.Shared.Return(_rented);
                _rented = default;
            }
        }

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            ThrowIfNotEnoughSpace(sizeHint);
            return _rented.AsMemory(WrittenCount);
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            ThrowIfNotEnoughSpace(sizeHint);
            return _rented.AsSpan(WrittenCount);
        }

        private void ThrowIfNotEnoughSpace(int count)
        {
            if (WrittenCount + count > Capacity)
            {
                throw new OutOfMemoryException(nameof(count));
            }
        }
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
            _readOnlySequence = end is not null ?
                new ReadOnlySequence<byte>(start, 0, end, end.Memory.Length):
                new ReadOnlySequence<byte>(start.Memory);
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

        private bool IsDisposed { get; set; }

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

    private sealed class FastCgiStdinStream(ConnectionContext connection): Stream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {

                var written = count < FastCgiRecordHeader.MAX_CONTENT_SIZE ? count : FastCgiRecordHeader.MAX_CONTENT_SIZE;

                FastCgiHttpMessageHandler.Send(
                    new FastCgiRecordHeader(Type: FastCgiRecordHeader.RecordType.Stdin, ContentLength: (ushort)written),
                    buffer.AsSpan(offset, written),connection);

                count -= written;
                offset += written;
            }
        }

        public override bool CanRead { get; } = false;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite { get; } = true;
        public override long Length { get; } = 0;

        public override long Position { get; set; }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
    }

    private static class HttpResponseHeaders
    {
        private const string Status = "Status";

        public static bool TryGetStatusHeader(ReadOnlySpan<byte> name, out string? header)
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
        public static bool TryGetContentHeader(ReadOnlySpan<byte> name, out string? header)
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
        private static uint ReadUnalignedLittleEndian_uint(ref byte source)
        {
            var result = Unsafe.ReadUnaligned<uint>(ref source);
            if (!BitConverter.IsLittleEndian)
            {
                result = BinaryPrimitives.ReverseEndianness(result);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUnalignedLittleEndian_ushort(ref byte source)
        {
            var result = Unsafe.ReadUnaligned<ushort>(ref source);
            if (!BitConverter.IsLittleEndian)
            {
                result = BinaryPrimitives.ReverseEndianness(result);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadUnalignedLittleEndian_ulong(ref byte source)
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
    public static byte[] ReplaceToUpperAscii(this string s, char oldChar, char newChar)
    {
        var chars = Encoding.ASCII.GetBytes(s);
        Debug.Assert(s.Length == chars.Length);

        var i = 0;
        foreach (var c in s)
        {
            if (c == oldChar)
            {
                chars[i] = (byte)newChar;
            }
            else
            {
                chars[i] = (byte)char.ToUpper(c);
            }
            i++;
        }
        return chars;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteAsciiString(this IBufferWriter<byte> buffer, string s)
    {
        // works only in .net 8
        // Debug.Assert(Ascii.IsValid(s));
        // var status = Ascii.FromUtf16(s, span, out int bytesWritten);
        // Debug.Assert(status == OperationStatus.Done);
        var span = buffer.GetSpan(s.Length);

        // TODO: kestrel has implemented better conversions compatible with 6,7
        var bytesWritten = Encoding.ASCII.GetBytes(s, span);

        Debug.Assert(bytesWritten == s.Length);

        return bytesWritten;
    }
}

