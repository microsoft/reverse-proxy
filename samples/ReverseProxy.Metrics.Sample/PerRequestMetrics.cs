using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Service.Proxy;
using System.Text.Json;

namespace Yarp.Sample
{
    public class PerRequestMetrics
    {
        private static readonly AsyncLocal<PerRequestMetrics> _local = new AsyncLocal<PerRequestMetrics>();

        /// <summary>
        /// Factory to instantiate or restore the metrics from AsyncLocal storage
        /// </summary>
        public static PerRequestMetrics Current
        {
            get {
                var current = _local.Value;
                if ( current == null)
                {
                    current = new PerRequestMetrics();
                    _local.Value = current;
                }
                return current;
            }
        }

        // Ensure we are only fetched via the factory
        private PerRequestMetrics() { }

        // Time the request was started via the pipeline
        public DateTime StartTime { get; set; }


        // Offset Tics for each part of the proxy operation
        public long RouteInvokeOffset { get; set; }
        public long ProxyStartOffset { get; set; }
        public long HttpRequestStartOffset { get; set; }
        public long HttpConnectionEstablishedOffset { get; set; }
        public long HttpRequestLeftQueueOffset { get; set; }

        public long HttpRequestHeadersStartOffset { get; set; }
        public long HttpRequestHeadersStopOffset { get; set; }
        public long HttpRequestContentStartOffset { get; set; }
        public long HttpRequestContentStopOffset { get; set; }

        public long HttpResponseHeadersStartOffset { get; set; }
        public long HttpResponseHeadersStopOffset { get; set; }
        public long HttpResponseContentStopOffset { get; set; }

        public long ProxyStopOffset { get; set; }


        //Info about the request
        public ProxyError Error { get; set; }
        public long RequestBodyLength { get; set; }
        public long ResponseBodyLength { get; set; }
        public long RequestContentIops { get; set; }
        public long ResponseContentIops { get; set; }
        public string DestinationId { get; set; }
        public string ClusterId { get; set; }
        public string RouteId { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

