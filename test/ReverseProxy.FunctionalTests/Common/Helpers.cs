using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace Microsoft.ReverseProxy
{
    public static class Helpers
    {
        public static string GetAddress(this IHost server)
        {
            return server.Services.GetService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First();
        }
    }
}
