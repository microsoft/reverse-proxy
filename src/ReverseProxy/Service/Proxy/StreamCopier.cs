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
        // Taken from https://github.com/aspnet/Proxy/blob/816f65429b29d98e3ca98dd6b4d5e990f5cc7c02/src/Microsoft.AspNetCore.Proxy/ProxyAdvancedExtensions.cs#L19
        private const int DefaultBufferSize = 81920;

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

            // TODO: Consider System.IO.Pipelines for better perf (e.g. reads during writes)
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            var reading = true;

            long contentLength = 0;
            long iops = 0;
            long readTime = 0;
            long writeTime = 0;
            long firstReadTime = -1;

            try
            {
                var lastTime = TimeSpan.Zero;
                var nextTransferringEvent = TimeSpan.Zero;
                var stopwatchTicksBetweenTransferringEvents = TimeSpan.Zero;

                if (telemetryEnabled)
                {
                    ProxyTelemetry.Log.ProxyStage(isRequest ? ProxyStage.RequestContentTransferStart : ProxyStage.ResponseContentTransferStart);

                    stopwatchTicksBetweenTransferringEvents = TimeSpan.FromSeconds(1);
                    lastTime = clock.GetStopwatchTime();
                    nextTransferringEvent = lastTime + stopwatchTicksBetweenTransferringEvents;
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
                            var currentReadTime = (readStop - lastTime).Ticks;
                            lastTime = readStop;
                            readTime += currentReadTime;
                            if (firstReadTime == -1)
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
                            writeTime += (writeStop - lastTime).Ticks;
                            lastTime = writeStop;
                            if (lastTime >= nextTransferringEvent)
                            {
                                ProxyTelemetry.Log.ContentTransferring(
                                    isRequest,
                                    contentLength,
                                    iops,
                                    StopwatchTicksToDateTimeTicks(readTime),
                                    StopwatchTicksToDateTimeTicks(writeTime));

                                // Avoid attributing the time taken by logging ContentTransferring to the next read call
                                lastTime = clock.GetStopwatchTime();
                                nextTransferringEvent = lastTime + stopwatchTicksBetweenTransferringEvents;
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
                        StopwatchTicksToDateTimeTicks(readTime),
                        StopwatchTicksToDateTimeTicks(writeTime),
                        StopwatchTicksToDateTimeTicks(Math.Max(0, firstReadTime)));
                }
            }

            static long StopwatchTicksToDateTimeTicks(long stopwatchTicks)
            {
                var dateTimeTicksPerStopwatchTick = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
                return (long)(stopwatchTicks * dateTimeTicksPerStopwatchTick);
            }
        }
    }
}
