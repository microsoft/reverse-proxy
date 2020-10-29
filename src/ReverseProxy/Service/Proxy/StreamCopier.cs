// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Telemetry;

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
        public static Task<(StreamCopyResult, Exception)> CopyAsync(bool isRequest, Stream input, Stream output, CancellationToken cancellation)
        {
            _ = input ?? throw new ArgumentNullException(nameof(input));
            _ = output ?? throw new ArgumentNullException(nameof(output));

            // TODO: Consider System.IO.Pipelines for better perf (e.g. reads during writes)
            return ProxyTelemetry.Log.IsEnabled()
                ? CopyWithTelemetryAsync(isRequest, input, output, cancellation)
                : CopyAsyncCore(input, output, cancellation);
        }

        private static async Task<(StreamCopyResult, Exception)> CopyAsyncCore(Stream input, Stream output, CancellationToken cancellation)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            var reading = true;
            try
            {
                while (true)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        return (StreamCopyResult.Canceled, new OperationCanceledException(cancellation));
                    }

                    reading = true;
                    var read = await input.ReadAsync(buffer.AsMemory(), cancellation);

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
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
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
            }
        }

        private static async Task<(StreamCopyResult, Exception)> CopyWithTelemetryAsync(bool isRequest, Stream input, Stream output, CancellationToken cancellation)
        {
            ProxyTelemetry.Log.ProxyStage(isRequest ? ProxyStage.RequestContentTransferStart : ProxyStage.ResponseContentTransferStart);

            // TODO: Should this be configurable
            var stopwatchTicksBetweenTransferringEvents = Stopwatch.Frequency; // 1 second

            // TODO: Consider System.IO.Pipelines for better perf (e.g. reads during writes)
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            var reading = true;
            try
            {
                long contentLength = 0;
                long iops = 0;
                var lastTime = Stopwatch.GetTimestamp();
                var nextTransferringEvent = lastTime + stopwatchTicksBetweenTransferringEvents;
                long firstReadTime = -1;
                long readTime = 0;
                long writeTime = 0;

                while (true)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        return (StreamCopyResult.Canceled, new OperationCanceledException(cancellation));
                    }

                    iops++;
                    reading = true;

                    var read = await input.ReadAsync(buffer.AsMemory(), cancellation);
                    contentLength += read;

                    var readStop = Stopwatch.GetTimestamp();
                    var currentReadTime = readStop - lastTime;
                    lastTime = readStop;
                    readTime += currentReadTime;
                    if (firstReadTime == -1)
                    {
                        firstReadTime = currentReadTime;
                    }

                    // End of the source stream.
                    if (read == 0)
                    {
                        // PR REVIEW:
                        // Should this event be logged on failure as well (instead of XContentTransferStop)?
                        ProxyTelemetry.Log.ContentTransferred(
                            isRequest,
                            contentLength,
                            iops,
                            StopwatchTicksToDateTimeTicks(readTime),
                            StopwatchTicksToDateTimeTicks(writeTime),
                            StopwatchTicksToDateTimeTicks(firstReadTime));

                        return (StreamCopyResult.Success, null);
                    }

                    if (cancellation.IsCancellationRequested)
                    {
                        return (StreamCopyResult.Canceled, new OperationCanceledException(cancellation));
                    }

                    reading = false;

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellation);

                    var writeStop = Stopwatch.GetTimestamp();
                    writeTime += writeStop - lastTime;
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
                        lastTime = Stopwatch.GetTimestamp();
                        nextTransferringEvent = lastTime + stopwatchTicksBetweenTransferringEvents;
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

                ProxyTelemetry.Log.ProxyStage(isRequest ? ProxyStage.RequestContentTransferStop : ProxyStage.ResponseContentTransferStop);
            }

            static long StopwatchTicksToDateTimeTicks(long stopwatchTicks)
            {
                var dateTimeTicksPerStopwatchTick = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
                return (long)(stopwatchTicks * dateTimeTicksPerStopwatchTick);
            }
        }
    }
}
