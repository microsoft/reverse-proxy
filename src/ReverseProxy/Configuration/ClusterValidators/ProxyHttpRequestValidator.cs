using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

internal sealed class ProxyHttpRequestValidator : IClusterValidator
{
    private readonly ILogger<ProxyHttpRequestValidator> _logger;

    public ProxyHttpRequestValidator(ILogger<ProxyHttpRequestValidator> logger)
    {
        _logger = logger;
    }

    public void AddValidationErrors(ClusterConfig cluster, IList<Exception> errors)
    {
        if (cluster.HttpRequest is null)
        {
            // Proxy http request options are not set.
            return;
        }

        if (cluster.HttpRequest.Version is not null &&
            cluster.HttpRequest.Version != HttpVersion.Version10 &&
            cluster.HttpRequest.Version != HttpVersion.Version11 &&
            cluster.HttpRequest.Version != HttpVersion.Version20 &&
            cluster.HttpRequest.Version != HttpVersion.Version30)
        {
            errors.Add(new ArgumentException($"Outgoing request version '{cluster.HttpRequest.Version}' is not any of supported HTTP versions (1.0, 1.1, 2 and 3)."));
        }

        if (cluster.HttpRequest.Version == HttpVersion.Version10)
        {
            Log.Http10Version(_logger);
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, Exception?> _http10RequestVersionDetected = LoggerMessage.Define(
            LogLevel.Warning,
            EventIds.Http10RequestVersionDetected,
            "The HttpRequest version is set to 1.0 which can result in poor performance and port exhaustion. Use 1.1, 2, or 3 instead.");

        public static void Http10Version(ILogger logger)
        {
            _http10RequestVersionDetected(logger, null);
        }
    }
}
