// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Service.Metrics;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Default implementation of <see cref="IStreamCopier"/>.
    /// </summary>
    internal class StreamCopier : IStreamCopier
    {
        // Taken from https://github.com/aspnet/Proxy/blob/816f65429b29d98e3ca98dd6b4d5e990f5cc7c02/src/Microsoft.AspNetCore.Proxy/ProxyAdvancedExtensions.cs#L19
        private const int DefaultBufferSize = 81920;

        private readonly StreamCopyTelemetryContext _context;
        private readonly ProxyMetrics _metrics;

        public StreamCopier(ProxyMetrics metrics, in StreamCopyTelemetryContext context)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _context = context;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Based on <c>Microsoft.AspNetCore.Http.StreamCopyOperationInternal.CopyToAsync</c>.
        /// See: <see href="https://github.com/dotnet/aspnetcore/blob/080660967b6043f731d4b7163af9e9e6047ef0c4/src/Http/Shared/StreamCopyOperationInternal.cs"/>.
        /// </remarks>
        public async Task<(StreamCopyResult, Exception)> CopyAsync(Stream source, Stream destination, CancellationToken cancellation)
        {
            _ = source ?? throw new ArgumentNullException(nameof(source));
            _ = destination ?? throw new ArgumentNullException(nameof(destination));

            // TODO: Consider System.IO.Pipelines for better perf (e.g. reads during writes)
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            long iops = 0;
            long totalBytes = 0;
            try
            {
                while (true)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        return (StreamCopyResult.Canceled, new OperationCanceledException(cancellation));
                    }

                    iops++;
                    int read;
                    try
                    {
                        read = await source.ReadAsync(buffer.AsMemory(), cancellation);
                    }
                    catch (OperationCanceledException oex)
                    {
                        return (StreamCopyResult.Canceled, oex);
                    }
                    catch (Exception ex)
                    {
                        return (StreamCopyResult.SourceError, ex);
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

                    try
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    }
                    catch (OperationCanceledException oex)
                    {
                        return (StreamCopyResult.Canceled, oex);
                    }
                    catch (Exception ex)
                    {
                        return (StreamCopyResult.DestionationError, ex);
                    }
                    totalBytes += read;
                }
            }
            finally
            {
                // We can afford the perf impact of clearArray == true since we only do this twice per request.
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);

                // TODO: Populate metric dimension `protocol`.
                _metrics.StreamCopyBytes(
                    value: totalBytes,
                    direction: _context.Direction,
                    clusterId: _context.ClusterId,
                    routeId: _context.RouteId,
                    destinationId: _context.DestinationId,
                    protocol: string.Empty);
                _metrics.StreamCopyIops(
                    value: iops,
                    direction: _context.Direction,
                    clusterId: _context.ClusterId,
                    routeId: _context.RouteId,
                    destinationId: _context.DestinationId,
                    protocol: string.Empty);
            }
        }
    }
}
