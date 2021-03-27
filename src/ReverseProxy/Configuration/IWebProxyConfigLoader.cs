using System.Net;
using Microsoft.Extensions.Configuration;

namespace Yarp.ReverseProxy.Configuration
{
    internal interface IWebProxyConfigLoader
    {
        IWebProxy LoadWebProxy(IConfigurationSection webProxyConfig);
    }
}
