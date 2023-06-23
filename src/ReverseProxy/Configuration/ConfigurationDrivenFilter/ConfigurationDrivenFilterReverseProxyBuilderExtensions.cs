using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.Configuration;

public static class ConfigurationDrivenFilterReverseProxyBuilderExtensions
{
    public static IReverseProxyBuilder AddConfigurationDrivenProxyFilter(this IReverseProxyBuilder builder)
        => builder.AddConfigFilter<ConfigurationDrivenFilter>();
}
