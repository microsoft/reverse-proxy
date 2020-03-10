// <copyright file="StreamCopier.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Service.Metrics;
using IslandGateway.CoreServicesBorrowed;

namespace IslandGateway.Core.Service.Proxy
{
    /// <summary>
    /// Default implementation of <see cref="IStreamCopier"/>.
    /// </summary>
    internal class StreamCopier : IStreamCopier
    {
        // Taken from https://github.com/aspnet/Proxy/blob/816f65429b29d98e3ca98dd6b4d5e990f5cc7c02/src/Microsoft.AspNetCore.Proxy/ProxyAdvancedExtensions.cs#L19
        private const int DefaultBufferSize = 81920;

        private readonly StreamCopyTelemetryContext context;
        private readonly GatewayMetrics metrics;

        public StreamCopier(GatewayMetrics metrics, in StreamCopyTelemetryContext context)
        {
            Contracts.CheckValue(metrics, nameof(metrics));
            this.metrics = metrics;
            this.context = context;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Based on <c>Microsoft.AspNetCore.Http.StreamCopyOperationInternal.CopyToAsync</c>.
        /// See: <see href="https://github.com/dotnet/aspnetcore/blob/080660967b6043f731d4b7163af9e9e6047ef0c4/src/Http/Shared/StreamCopyOperationInternal.cs"/>.
        /// </remarks>
        public async Task CopyAsync(Stream source, Stream destination, CancellationToken cancellation)
        {
            Contracts.CheckValue(source, nameof(source));
            Contracts.CheckValue(destination, nameof(destination));

            // TODO: Consider System.IO.Pipelines for better perf (e.g. reads during writes)
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            long iops = 0;
            long totalBytes = 0;
            try
            {
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    iops++;
                    int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellation);

                    // End of the source stream.
                    if (read == 0)
                    {
                        return;
                    }

                    cancellation.ThrowIfCancellationRequested();
                    await destination.WriteAsync(buffer, 0, read, cancellation);
                    totalBytes += read;
                }
            }
            finally
            {
                // We can afford the perf impact of clearArray == true since we only do this twice per request.
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);

                // TODO: Populate metric dimension `protocol`.
                this.metrics.StreamCopyBytes(
                    value: totalBytes,
                    direction: this.context.Direction,
                    backendId: this.context.BackendId,
                    routeId: this.context.RouteId,
                    endpointId: this.context.EndpointId,
                    protocol: string.Empty);
                this.metrics.StreamCopyIops(
                    value: iops,
                    direction: this.context.Direction,
                    backendId: this.context.BackendId,
                    routeId: this.context.RouteId,
                    endpointId: this.context.EndpointId,
                    protocol: string.Empty);
            }
        }
    }
}
