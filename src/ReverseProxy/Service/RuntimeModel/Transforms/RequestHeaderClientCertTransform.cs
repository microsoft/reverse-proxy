// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderClientCertTransform : RequestHeaderTransform
    {
        public override StringValues Apply(HttpContext context, StringValues values)
        {
            var clientCert = context.Connection.ClientCertificate;
            if (clientCert == null)
            {
                return StringValues.Empty;
            }

            return Convert.ToBase64String(clientCert.RawData);
        }
    }
}
