// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.ServiceFabric.Services.Communication;

namespace Yarp.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// Provides a method <see cref="TryGetEndpoint"/> to select an appropriate endpoint from a Service Fabric service replica endpoints collection.
    /// </summary>
    internal static class FabricServiceEndpointSelector
    {
        /// <summary>
        /// Selects and endpoint (aka "listener") from a presented <paramref name="endpoints"/> collection
        /// that satisfies all constraints of <paramref name="fabricServiceEndpoint"/>.
        /// </summary>
        /// <param name="fabricServiceEndpoint">User-defined info and constraints for the selecting an endpoint from <paramref name="fabricServiceEndpoint"/>.</param>
        /// <param name="endpoints">Collection of endpoints to choose from.</param>
        /// <param name="endpointUri">The endpoint URI to extract.</param>
        /// <returns>Boolean indicating whether an endpoint URI was successfully retrieved.</returns>
        public static bool TryGetEndpoint(
            FabricServiceEndpoint fabricServiceEndpoint,
            ServiceEndpointCollection endpoints,
            out Uri endpointUri)
        {
            _ = fabricServiceEndpoint ?? throw new ArgumentNullException(nameof(fabricServiceEndpoint));
            _ = endpoints ?? throw new ArgumentNullException(nameof(endpoints));

            endpointUri = null;
            string endpointAddress = null;

            foreach (var listenerName in fabricServiceEndpoint.ListenerNames)
            {
                // SF Reverse Proxy endpoint selection logic: https://github.com/microsoft/service-fabric/blob/1e118f02294c99b61e676c07ac97283ee12197d4/src/prod/src/Management/ApplicationGateway/Http/ServiceEndpointsList.cpp#L52
                if (listenerName == string.Empty && fabricServiceEndpoint.EmptyStringMatchesAnyListener)
                {
                    endpointUri = endpoints.ToReadOnlyDictionary()

                        // NOTE: Ordinal comparison used to match sort order of Service Fabric Reverse Proxy
                        .OrderBy(listenerAddressPair => listenerAddressPair.Key, StringComparer.Ordinal)

                        // From the endpoints above, select endpoints with valid URIs
                        .Select(listenerAddressPair =>
                        {
                            if (Uri.TryCreate(listenerAddressPair.Value, UriKind.Absolute, out var uri))
                            {
                                return uri;
                            }

                            return null;
                        })
                        .Where(replicaAddress => replicaAddress != null)

                        // Pick first endpoint that matches scheme predicate.
                        .FirstOrDefault(replicaAddress =>
                        {
                            if (fabricServiceEndpoint.AllowedSchemePredicate(replicaAddress.Scheme))
                            {
                                return true;
                            }

                            return false;
                        });

                    // Bail as soon as first valid endpoint is found.
                    if (endpointUri != null)
                    {
                        // CoreFrameworkFabricTrace.Instance.TraceVerbose("Located endpoint URI is '{0}'", endpointUri);
                        return true;
                    }
                }
                else
                {
                    // Pick named listener endpoint
                    if (endpoints.TryGetEndpointAddress(listenerName: listenerName, endpointAddress: out endpointAddress))
                    {
                        if (!Uri.TryCreate(endpointAddress, UriKind.Absolute, out var endpointUri_))
                        {
                            continue;
                        }

                        // Match the Uri against allowed scheme predicate.
                        if (!fabricServiceEndpoint.AllowedSchemePredicate(endpointUri_.Scheme))
                        {
                            continue;
                        }

                        endpointUri = endpointUri_;

                        // CoreFrameworkFabricTrace.Instance.TraceVerbose("Located endpoint URI is '{0}'", endpointUri);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
