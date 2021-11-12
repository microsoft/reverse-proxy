// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.ServiceFabric;

/// <summary>
/// A wrapper class for the service fabric client SDK.
/// See Microsoft documentation: https://docs.microsoft.com/en-us/dotnet/api/system.fabric.fabricclient.propertymanagementclient?view=azure-dotnet .
/// </summary>
internal sealed class PropertyManagementClientWrapper : IPropertyManagementClientWrapper
{
    // Represents the property management client used to perform management of names and properties.
    private readonly FabricClient.PropertyManagementClient _propertyManagementClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyManagementClientWrapper"/> class.
    /// </summary>
    public PropertyManagementClientWrapper(IFabricClientWrapper fabricClientWrapper)
    {
        _propertyManagementClient = fabricClientWrapper.FabricClient.PropertyManager;
    }

    /// <summary>
    /// Get the specified NamedProperty.
    /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
    /// </summary>
    public async Task<string> GetPropertyAsync(Uri parentName, string propertyName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var result = await ExceptionsHelper.TranslateCancellations(
            () => _propertyManagementClient.GetPropertyAsync(parentName, propertyName, timeout, cancellationToken),
            cancellationToken);

        if (result != null)
        {
            // Transform Nameproperty type to plain string, Nameproperty is a sealed class that is not unit-testable.
            return result.GetValue<string>();
        }

        return null;
    }

    /// <summary>
    /// Enumerates all Service Fabric properties under a given name.
    /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
    /// </summary>
    public async Task<IDictionary<string, string>> EnumeratePropertiesAsync(Uri parentName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var namedProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        PropertyEnumerationResult previousResult = null;

        // Set up the counter that record the time lapse.
        var stopWatch = ValueStopwatch.StartNew();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = timeout - stopWatch.Elapsed;
            if (remaining.Ticks < 0)
            {
                // If the passing time is longer than the timeout duration.
                throw new TimeoutException($"Unable to enumerate all property pages in the allotted time budget of {timeout.TotalSeconds} seconds");
            }

            previousResult = await ExceptionsHelper.TranslateCancellations(
                () => _propertyManagementClient.EnumeratePropertiesAsync(
                    name: parentName,
                    includeValues: true,
                    previousResult: previousResult,
                    timeout: remaining,
                    cancellationToken: cancellationToken),
                cancellationToken);
            foreach (var p in previousResult)
            {
                if (!namedProperties.TryAdd(p.Metadata.PropertyName, p.GetValue<string>()))
                {
                    // TODO: Add warning message such as "$PropertyName already exist"
                }
            }
        }
        while (previousResult.HasMoreData);
        return namedProperties;
    }
}
