// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderXForwardedForTransform : RequestHeaderTransform
    {
        // or Set
        private readonly bool _append;

        public RequestHeaderXForwardedForTransform(bool append)
        {
            _append = append;
        }

        public override StringValues Apply(HttpContext context, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (remoteIp == null)
            {
                return _append ? values : StringValues.Empty;
            }

            return _append ? StringValues.Concat(values, remoteIp) : new StringValues(remoteIp);
        }
    }
}
