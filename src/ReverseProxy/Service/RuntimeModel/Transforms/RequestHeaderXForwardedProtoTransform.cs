// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderXForwardedProtoTransform : RequestHeaderTransform
    {
        // or Set
        private readonly bool _append;

        public RequestHeaderXForwardedProtoTransform(bool append)
        {
            _append = append;
        }

        public override StringValues Apply(HttpContext context, StringValues values)
        {
            var scheme = context.Request.Scheme;
            return _append ? StringValues.Concat(values, scheme) : new StringValues(scheme);
        }
    }
}
