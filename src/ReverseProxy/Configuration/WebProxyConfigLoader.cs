using System;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace Yarp.ReverseProxy.Configuration
{
    internal class WebProxyConfigLoader : IWebProxyConfigLoader
    {
        public IWebProxy LoadWebProxy(IConfigurationSection webProxyConfig)
        {
            if (!webProxyConfig.Exists())
            {
                return null;
            }

            var config = new WebProxyConfigData();
            webProxyConfig.Bind(config);

            if (config.Address == null)
            {
                return null;
            }

            var webProxy = new WebProxy(config.Address);
            if (config.UseDefaultCredentials != null)
            {
                webProxy.UseDefaultCredentials = config.UseDefaultCredentials.Value;
            }

            if (config.BypassOnLocal != null)
            {
                webProxy.BypassProxyOnLocal = config.BypassOnLocal.Value;
            }

            return webProxy;
        }

        private class WebProxyConfigData
        {
            public Uri Address { get; init; }
            public bool? BypassOnLocal { get; init; }
            public bool? UseDefaultCredentials { get; init; }
        }
    }
}
