namespace Microsoft.ReverseProxy.Telemetry
{
    /// <summary>
    /// Defines names of DiagnosticListener and Write events for WinHttpHandler, CurlHandler, and HttpHandlerToFilter.
    /// </summary>
    internal static class DiagnosticsHandlerLoggingStrings
    {
        public const string DiagnosticListenerName = "HttpHandlerDiagnosticListener";
        public const string ExceptionEventName = "System.Net.Http.Exception";
        public const string ActivityName = "System.Net.Http.HttpRequestOut";
        public const string ActivityStartName = "System.Net.Http.HttpRequestOut.Start";

        public const string RequestIdHeaderName = "Request-Id";
        public const string CorrelationContextHeaderName = "Correlation-Context";

        public const string TraceParentHeaderName = "traceparent";
        public const string TraceStateHeaderName = "tracestate";
    }
}
