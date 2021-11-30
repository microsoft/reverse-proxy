// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// A stream copier that captures errors.
/// </summary>
internal static class StreamCopier
{
    // Based on performance investigations, see https://github.com/microsoft/reverse-proxy/pull/330#issuecomment-758851852.
    private const int DefaultBufferSize = 65536;

    public static ValueTask<(StreamCopyResult, Exception?)> CopyAsync(bool isRequest, Stream input, Stream output, IClock clock, ActivityCancellationTokenSource activityToken, CancellationToken cancellation)
    {
        Debug.Assert(input is not null);
        Debug.Assert(output is not null);
        Debug.Assert(clock is not null);
        Debug.Assert(activityToken is not null);

        // Avoid capturing 'isRequest' and 'clock' in the state machine when telemetry is disabled
        var telemetry = ForwarderTelemetry.Log.IsEnabled(EventLevel.Informational, EventKeywords.All)
            ? new StreamCopierTelemetry(isRequest, clock)
            : null;

        return CopyAsync(input, output, telemetry, activityToken, cancellation);
    }

    private static async ValueTask<(StreamCopyResult, Exception?)> CopyAsync(Stream input, Stream output, StreamCopierTelemetry? telemetry, ActivityCancellationTokenSource activityToken, CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        var read = 0;
        try
        {
            while (true)
            {
                read = 0;

                // Issue a zero-byte read to the input stream to defer buffer allocation until data is available.
                // Note that if the underlying stream does not supporting blocking on zero byte reads, then this will
                // complete immediately and won't save any memory, but will still function correctly.
                var zeroByteReadTask = input.ReadAsync(Memory<byte>.Empty, cancellation);
                if (zeroByteReadTask.IsCompletedSuccessfully)
                {
                    // Consume the ValueTask's result in case it is backed by an IValueTaskSource
                    _ = zeroByteReadTask.Result;
                }
                else
                {
                    // Take care not to return the same buffer to the pool twice in case zeroByteReadTask throws
                    var bufferToReturn = buffer;
                    buffer = null;
                    ArrayPool<byte>.Shared.Return(bufferToReturn);

                    await zeroByteReadTask;

                    buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                }

                read = await input.ReadAsync(buffer.AsMemory(), cancellation);

                telemetry?.AfterRead(read);

                // Success, reset the activity monitor.
                activityToken.ResetTimeout();

                // End of the source stream.
                if (read == 0)
                {
                    return (StreamCopyResult.Success, null);
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellation);

                telemetry?.AfterWrite();

                // Success, reset the activity monitor.
                activityToken.ResetTimeout();
            }
        }
        catch (Exception ex)
        {
            if (read == 0)
            {
                telemetry?.AfterRead(0);
            }
            else
            {
                telemetry?.AfterWrite();
            }

            var result = ex is OperationCanceledException ? StreamCopyResult.Canceled :
                (read == 0 ? StreamCopyResult.InputError : StreamCopyResult.OutputError);

            return (result, ex);
        }
        finally
        {
            if (buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            telemetry?.Stop();
        }
    }

    private sealed class StreamCopierTelemetry
    {
        private static readonly TimeSpan _timeBetweenTransferringEvents = TimeSpan.FromSeconds(1);

        private readonly bool _isRequest;
        private readonly IClock _clock;
        private long _contentLength;
        private long _iops;
        private TimeSpan _readTime;
        private TimeSpan _writeTime;
        private TimeSpan _firstReadTime;
        private TimeSpan _lastTime;
        private TimeSpan _nextTransferringEvent;

        public StreamCopierTelemetry(bool isRequest, IClock clock)
        {
            _isRequest = isRequest;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _firstReadTime = new TimeSpan(-1);

            ForwarderTelemetry.Log.ForwarderStage(isRequest ? ForwarderStage.RequestContentTransferStart : ForwarderStage.ResponseContentTransferStart);

            _lastTime = clock.GetStopwatchTime();
            _nextTransferringEvent = _lastTime + _timeBetweenTransferringEvents;
        }

        public void AfterRead(int read)
        {
            _contentLength += read;
            _iops++;

            var readStop = _clock.GetStopwatchTime();
            var currentReadTime = readStop - _lastTime;
            _lastTime = readStop;
            _readTime += currentReadTime;
            if (_firstReadTime.Ticks < 0)
            {
                _firstReadTime = currentReadTime;
            }
        }

        public void AfterWrite()
        {
            var writeStop = _clock.GetStopwatchTime();
            _writeTime += writeStop - _lastTime;
            _lastTime = writeStop;

            if (writeStop >= _nextTransferringEvent)
            {
                ForwarderTelemetry.Log.ContentTransferring(
                    _isRequest,
                    _contentLength,
                    _iops,
                    _readTime.Ticks,
                    _writeTime.Ticks);

                // Avoid attributing the time taken by logging ContentTransferring to the next read call
                _lastTime = _clock.GetStopwatchTime();
                _nextTransferringEvent = _lastTime + _timeBetweenTransferringEvents;
            }
        }

        public void Stop()
        {
            ForwarderTelemetry.Log.ContentTransferred(
                _isRequest,
                _contentLength,
                _iops,
                _readTime.Ticks,
                _writeTime.Ticks,
                Math.Max(0, _firstReadTime.Ticks));
        }
    }
}
