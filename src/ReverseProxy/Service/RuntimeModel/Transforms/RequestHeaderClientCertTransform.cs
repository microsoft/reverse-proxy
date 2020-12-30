// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Base64 encodes the client certificate (if any) and sets it as the header value.
    /// </summary>
    public class RequestHeaderClientCertTransform : RequestTransform
    {
        public RequestHeaderClientCertTransform(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        internal string Name { get; }

        /// <inheritdoc/>
        public override void Apply(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var proxyRequestHeaders = context.ProxyRequest.Headers;
            proxyRequestHeaders.Remove(Name);

            var clientCert = context.HttpContext.Connection.ClientCertificate;
            if (clientCert != null)
            {
                var encoded = Convert.ToBase64String(clientCert.RawData);
                var added = proxyRequestHeaders.TryAddWithoutValidation(Name, encoded);
                Debug.Assert(added); // Why wouldn't it be added?
            }
        }
    }
}
