// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.ServiceDiscovery;

/// <summary>
/// Implementation of <see cref="IDestinationResolver"/> which resolves host names to IP addresses using DNS.
/// </summary>
internal class DnsDestinationResolver : IDestinationResolver
{
    private readonly IOptionsMonitor<DnsDestinationResolverOptions> _options;
    private readonly ILogger<DnsDestinationResolver> _logger;

    /// <summary>
    /// Initializes a new <see cref="DnsDestinationResolver"/> instance.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>
    public DnsDestinationResolver(IOptionsMonitor<DnsDestinationResolverOptions> options, ILogger<DnsDestinationResolver> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<ResolvedDestinationCollection> ResolveDestinationsAsync(IReadOnlyDictionary<string, DestinationConfig> destinations, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        Dictionary<string, DestinationConfig> results = new();
        var tasks = new List<Task<List<(string Name, DestinationConfig Config)>>>(destinations.Count);
        foreach (var (destinationId, destinationConfig) in destinations)
        {
            tasks.Add(ResolveHostAsync(options, destinationId, destinationConfig, cancellationToken));
        }

        await Task.WhenAll(tasks);
        foreach (var task in tasks)
        {
            var configs = await task;
            foreach (var (name, config) in configs)
            {
                results[name] = config;
            }
        }

        var changeToken = options.RefreshPeriod switch
        {
            { } refreshPeriod when refreshPeriod > TimeSpan.Zero => new CancellationChangeToken(new CancellationTokenSource(refreshPeriod).Token),
            _ => null,
        };

        return new ResolvedDestinationCollection(results, changeToken);
    }

    private static async Task<List<(string Name, DestinationConfig Config)>> ResolveHostAsync(
        DnsDestinationResolverOptions options,
        string originalName,
        DestinationConfig originalConfig,
        CancellationToken cancellationToken)
    {
        var originalUri = new Uri(originalConfig.Address);
        var originalHost = originalConfig.Host is { Length: > 0 } host ? host : originalUri.Authority;
        var addresses = options.AddressFamily switch
        {
            { } addressFamily => await Dns.GetHostAddressesAsync(originalUri.DnsSafeHost, addressFamily, cancellationToken).ConfigureAwait(false),
            null => await Dns.GetHostAddressesAsync(originalUri.DnsSafeHost, cancellationToken).ConfigureAwait(false)
        };
        var results = new List<(string Name, DestinationConfig Config)>(addresses.Length);
        var uriBuilder = CreateUriBuilder(originalUri);
        var healthUri = originalConfig.Health is { Length: > 0 } health ? new Uri(health) : null;
        var healthUriBuilder = healthUri is { } ? CreateUriBuilder(healthUri) : null;
        foreach (var address in addresses)
        {
            var addressString = address.ToString();
            uriBuilder.Host = addressString;
            var resolvedAddress = uriBuilder.ToString();
            var healthAddress = originalConfig.Health;
            if (healthUriBuilder is not null)
            {
                healthUriBuilder.Host = addressString;
                healthAddress = healthUriBuilder.ToString();
            }

            var name = $"{originalName}[{addressString}]";
            var config = originalConfig with { Host = originalHost, Address = resolvedAddress, Health = healthAddress };
            results.Add((name, config));
        }

        return results;
    }

    /// <summary>
    /// Creates a <see cref="UriBuilder"/> from the provided address, only setting the Port property if it is not the default value.
    /// </summary>
    private static UriBuilder CreateUriBuilder(Uri uri)
    {
        var result = new UriBuilder()
        {
            Scheme = uri.Scheme,
            Host = uri.Host,
            Path = uri.AbsolutePath,
            Query = uri.Query,
            Fragment = uri.Fragment,
        };

        if (!uri.IsDefaultPort)
        {
            result.Port = uri.Port;
        }

        return result;
    }
}
