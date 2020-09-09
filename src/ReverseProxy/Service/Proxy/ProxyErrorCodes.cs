// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Proxy
{
    public enum ProxyErrorCode
    {
        None,
        // Failed to connect or send the request.
        Request,
        // A cancellation occurred while copying the response body.
        RequestBodyCanceled,
        // Failed reading the request body from the client.
        RequestBodyClient,
        // Failed writing the request body to the destination.
        RequestBodyDestination,
        // A cancellation occurred while copying the response body.
        ResponseBodyCanceled,
        // Failed when reading response data from the destination.
        ResponseBodyDestination,
        // Failed when writing response data to the client.
        ResponseBodyClient,
        UpgradeRequestCanceled,
        UpgradeRequestClient,
        UpgradeRequestDestination,
        UpgradeResponseCanceled,
        UpgradeResponseClient,
        UpgradeResponseDestination,
    }
}
