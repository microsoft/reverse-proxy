// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderXForwardedPathBaseTransform : RequestHeaderTransform
    {
        // or Set
        private readonly bool _append;

        public RequestHeaderXForwardedPathBaseTransform(bool append)
        {
            _append = append;
        }

        public override StringValues Apply(HttpContext context, StringValues values)
        {
            var pathBase = context.Request.PathBase;
            if (!pathBase.HasValue)
            {
                return _append ? values : StringValues.Empty;
            }

            var encodedPathBase = pathBase.ToUriComponent();
            return _append ? StringValues.Concat(values, encodedPathBase) : new StringValues(encodedPathBase);
        }
    }
}
