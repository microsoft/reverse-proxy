// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy;

public static class Helpers
{
    public static string GetAddress(this IHost server)
    {
        return server.Services.GetService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First();
    }

    public static int GetFreePort()
    {
        var openPorts = GetOpenPorts();

        int port;
        do
        {
            port = ThreadStaticRandom.Instance.Next(5000, 65535);
        } while (openPorts.Contains(port));

        return port;
    }

    private static HashSet<int> GetOpenPorts()
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var listeners = properties.GetActiveTcpListeners();

        return new HashSet<int>(listeners.Select(item => item.Port));
    }
}
