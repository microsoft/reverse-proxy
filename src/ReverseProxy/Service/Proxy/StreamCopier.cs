// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Telemetry;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// A stream copier that captures errors.
    /// </summary>
    internal static class StreamCopier
    {
        // Based on performance investigations, see https://github.com/microsoft/reverse-proxy/pull/330#issuecomment-758851852.
        private const int DefaultBufferSize = 65536;

        private static readonly TimeSpan TimeBetweenTransferringEvents = TimeSpan.FromSeconds(1);

        /// <inheritdoc/>
        /// <remarks>
        /// Based on <c>Microsoft.AspNetCore.Http.StreamCopyOperationInternal.CopyToAsync</c>.
        /// See: <see href="https://github.com/dotnet/aspnetcore/blob/080660967b6043f731d4b7163af9e9e6047ef0c4/src/Http/Shared/StreamCopyOperationInternal.cs"/>.
        /// </remarks>
        public static async Task<(StreamCopyResult, Exception)> CopyAsync(bool isRequest, Stream input, Stream output, IClock clock, CancellationToken cancellation)
        {
            _ = input ?? throw new ArgumentNullException(nameof(input));
            _ = output ?? throw new ArgumentNullException(nameof(output));

            var telemetryEnabled = ProxyTelemetry.Log.IsEnabled();

            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            var reading = true;

            long contentLength = 0;
            long iops = 0;
            var readTime = TimeSpan.Zero;
            var writeTime = TimeSpan.Zero;
            var firstReadTime = TimeSpan.FromMilliseconds(-1);

            try
            {
                var lastTime = TimeSpan.Zero;
                var nextTransferringEvent = TimeSpan.Zero;

                if (telemetryEnabled)
                {
                    ProxyTelemetry.Log.ProxyStage(isRequest ? ProxyStage.RequestContentTransferStart : ProxyStage.ResponseContentTransferStart);

                    lastTime = clock.GetStopwatchTime();
                    nextTransferringEvent = lastTime + TimeBetweenTransferringEvents;
                }

                while (true)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        return (StreamCopyResult.Canceled, new OperationCanceledException(cancellation));
                    }

                    reading = true;
                    var read = 0;
                    try
                    {
                        read = await input.ReadAsync(buffer.AsMemory(), cancellation);
                    }
                    finally
                    {
                        if (telemetryEnabled)
                        {
                            contentLength += read;
                            iops++;

                            var readStop = clock.GetStopwatchTime();
                            var currentReadTime = readStop - lastTime;
                            lastTime = readStop;
                            readTime += currentReadTime;
                            if (firstReadTime.Ticks < 0)
                            {
                                firstReadTime = currentReadTime;
                            }
                        }
                    }

                    // End of the source stream.
                    if (read == 0)
                    {
                        return (StreamCopyResult.Success, null);
                    }

                    if (cancellation.IsCancellationRequested)
                    {
                        return (StreamCopyResult.Canceled, new OperationCanceledException(cancellation));
                    }

                    reading = false;
                    try
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    }
                    finally
                    {
                        if (telemetryEnabled)
                        {
                            var writeStop = clock.GetStopwatchTime();
                            writeTime += writeStop - lastTime;
                            lastTime = writeStop;
                            if (lastTime >= nextTransferringEvent)
                            {
                                ProxyTelemetry.Log.ContentTransferring(
                                    isRequest,
                                    contentLength,
                                    iops,
                                    readTime.Ticks,
                                    writeTime.Ticks);

                                // Avoid attributing the time taken by logging ContentTransferring to the next read call
                                lastTime = clock.GetStopwatchTime();
                                nextTransferringEvent = lastTime + TimeBetweenTransferringEvents;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException oex)
            {
                return (StreamCopyResult.Canceled, oex);
            }
            catch (Exception ex)
            {
                return (reading ? StreamCopyResult.InputError : StreamCopyResult.OutputError, ex);
            }
            finally
            {
                // We can afford the perf impact of clearArray == true since we only do this twice per request.
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);

                if (telemetryEnabled)
                {
                    ProxyTelemetry.Log.ContentTransferred(
                        isRequest,
                        contentLength,
                        iops,
                        readTime.Ticks,
                        writeTime.Ticks,
                        Math.Max(0, firstReadTime.Ticks));
                }
            }
        }
    }
}
