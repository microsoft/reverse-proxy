// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using FluentAssertions;
using Microsoft.ServiceFabric.Services.Communication;
using Xunit;

namespace Yarp.ReverseProxy.ServiceFabric.Tests;

public class FabricServiceEndpointSelectorTests
{
    [Fact]
    public void FabricServiceEndpointSelector_SelectsNamedEndpoint()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerName = "ServiceEndpoint";
        var allowedScheme = "https";
        var endpointAddress = "https://localhost:123/valid";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': 'https://localhost:123/query',
                'DifferentServiceEndpoint2': 'https://loopback:123/query',
                '{listenerName}': '{endpointAddress}',
                'DifferentServiceEndpoint3': 'https://localhost:456/query',
                'DifferentServiceEndpoint4': 'https://loopback:456/query'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeTrue("There should be a matching endpoint");
        endpointUri.ToString().Should().BeEquivalentTo(endpointAddress);
    }

    [Fact]
    public void FabricServiceEndpointSelector_SelectsEndpointInOrdinalStringOrder_EmptyListenerName()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var allowedScheme = "https";

        var endpoints = $@"{{
            'Endpoints': {{
                'SelectedServiceEndpoint': 'https://localhost:123/selected',
                'notSelected1': 'https://loopback:123/query',
                'notSelected2': 'https://localhost:456/query',
                'notSelected3': 'https://loopback:456/query'
            }}
        }}".Replace("'", "\"");
        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { string.Empty },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeTrue("There should be a matching endpoint");
        endpointUri.ToString().Should().BeEquivalentTo("https://localhost:123/selected");
    }

    [Fact]
    public void FabricServiceEndpointSelector_SelectsEmptyListenerEndpoint_EmptyListenerName()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = false;
        var listenerName = string.Empty;
        var allowedScheme = "https";
        var endpointAddress = "https://localhost:123/valid";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': 'https://localhost:123/query',
                'DifferentServiceEndpoint2': 'https://loopback:123/query',
                '{listenerName}': '{endpointAddress}',
                'DifferentServiceEndpoint3': 'https://localhost:456/query',
                'DifferentServiceEndpoint4': 'https://loopback:456/query'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeTrue("There should be a matching endpoint");
        endpointUri.ToString().Should().BeEquivalentTo(endpointAddress);
    }

    [Fact]
    public void FabricServiceEndpointSelector_NoMatchingListenerName()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerName = "ServiceEndpoint";
        var allowedScheme = "https";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': 'https://localhost:123/query',
                'DifferentServiceEndpoint2': 'https://loopback:123/query',
                'DifferentServiceEndpoint3': 'https://localhost:456/query',
                'DifferentServiceEndpoint4': 'https://loopback:456/query'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeFalse("No matching named endpoint.");
        endpointUri.Should().BeNull();
    }

    [Fact]
    public void FabricServiceEndpointSelector_NoMatchingEndpointScheme()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerName = "ServiceEndpoint";
        var allowedScheme = "http";
        var endpointAddress = "https://localhost:123/valid";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': 'https://localhost:123/query',
                'DifferentServiceEndpoint2': 'https://loopback:123/query',
                '{listenerName}': '{endpointAddress}',
                'DifferentServiceEndpoint3': 'https://localhost:456/query',
                'DifferentServiceEndpoint4': 'https://loopback:456/query'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeFalse("No matching endpoint with specified scheme.");
        endpointUri.Should().BeNull();
    }

    [Fact]
    public void FabricServiceEndpointSelector_NoExceptionOnMalformedUri_NamedListener()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerName = "ServiceEndpoint";
        var allowedScheme = "https";
        var endpointAddress = "https://localhost:123/valid";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': '/malformed',
                'DifferentServiceEndpoint2': '/malformed',
                '{listenerName}': '{endpointAddress}',
                'DifferentServiceEndpoint3': '/malformed',
                'DifferentServiceEndpoint4': '/malformed'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeTrue("There should be a matching endpoint");
        endpointUri.ToString().Should().BeEquivalentTo(endpointAddress);
    }

    [Fact]
    public void FabricServiceEndpointSelector_NoExceptionOnMalformedUri_EmptyListener()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerName = string.Empty;
        var allowedScheme = "https";
        var endpointAddress = "https://localhost:123/valid";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': '/malformed',
                'DifferentServiceEndpoint2': '/malformed',
                'ValidServiceEndpoint': '{endpointAddress}',
                'DifferentServiceEndpoint3': '/malformed',
                'DifferentServiceEndpoint4': '/malformed'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeTrue("There should be a matching endpoint");
        endpointUri.ToString().Should().BeEquivalentTo(endpointAddress);
    }

    [Fact]
    public void FabricServiceEndpointSelector_SelectsEndpointBasedOnScheme_MultipleRequestedListeners()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerNames = new[] { "ServiceEndpointSecure", string.Empty };
        var allowedScheme = "https";
        var endpointAddress = "https://localhost:123/valid";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': '/malformed',
                'ServiceEndpointSecure': 'http://localhost/invalidScheme',
                'ValidServiceEndpoint': '{endpointAddress}'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: listenerNames,
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeTrue("There should be a matching endpoint");
        endpointUri.ToString().Should().BeEquivalentTo(endpointAddress);
    }

    [Fact]
    public void FabricServiceEndpointSelector_NoValidEndpointBasedOnScheme_NamedListener()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerName = "ServiceEndpointSecure";
        var allowedScheme = "https";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': '/malformed',
                '{listenerName}': 'http://localhost/invalidScheme',
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeFalse("There should be no matching endpoint because of scheme mismatch.");
        endpointUri.Should().BeNull();
    }

    [Fact]
    public void FabricServiceEndpointSelector_ReturnsFalseOnMalformedUri_NamedListener()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerName = "ServiceEndpoint";
        var allowedScheme = "https";
        var endpointAddress = "/alsoMalformed";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': '/malformed',
                'DifferentServiceEndpoint2': '/malformed',
                '{listenerName}': '{endpointAddress}',
                'DifferentServiceEndpoint3': '/malformed',
                'DifferentServiceEndpoint4': '/malformed'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeFalse("There should be no matching endpoint");
        endpointUri.Should().BeNull();
    }

    [Fact]
    public void FabricServiceEndpointSelector_ReturnsFalseOnMalformedUri_EmptyListenerName()
    {
        var serviceName = new Uri("fabric:/Application/Service");
        var emptyStringMatchesAnyListener = true;
        var listenerName = string.Empty;
        var allowedScheme = "https";
        var endpointAddress = "/alsoMalformed";

        var endpoints = $@"{{
            'Endpoints': {{
                'DifferentServiceEndpoint1': '/malformed',
                'DifferentServiceEndpoint2': '/malformed',
                '{listenerName}': '{endpointAddress}',
                'DifferentServiceEndpoint3': '/malformed',
                'DifferentServiceEndpoint4': '/malformed'
            }}
        }}".Replace("'", "\"");

        var fabricServiceEndpoint = new FabricServiceEndpoint(
            listenerNames: new[] { listenerName },
            allowedSchemePredicate: (scheme) => scheme == allowedScheme,
            emptyStringMatchesAnyListener: emptyStringMatchesAnyListener);
        ServiceEndpointCollection.TryParseEndpointsString(endpoints, out var serviceEndpointCollection);

        FabricServiceEndpointSelector.TryGetEndpoint(fabricServiceEndpoint, serviceEndpointCollection, out var endpointUri)
            .Should().BeFalse("There should be no matching endpoint");
        endpointUri.Should().BeNull();
    }
}
