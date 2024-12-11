using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration;

public class ConfigExtensionsOptions
{
    public Dictionary<string, Type> RouteExtensions { get; } = new();
    public Dictionary<string, Type> ClusterExtensions { get; } = new();
}
