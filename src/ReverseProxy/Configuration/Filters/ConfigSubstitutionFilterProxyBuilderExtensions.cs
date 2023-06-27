using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.Configuration;

public static class ConfigSubstitutionFilterProxyBuilderExtensions
{
    public static IReverseProxyBuilder AddConfigurationDrivenProxyFilter(this IReverseProxyBuilder builder)
        => builder.AddConfigFilter<ConfigSubstitutionFilter>();
}
