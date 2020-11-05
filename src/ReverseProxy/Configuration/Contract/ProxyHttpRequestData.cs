using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    public class ProxyHttpRequestData
    {
        public TimeSpan? RequestTimeout { get; set; }

        public string Version { get; set; }

#if NET
        public HttpVersionPolicy VersionPolicy { get; set; }
#endif
    }
}
