// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public abstract class RequestHeaderTransform
    {
        public abstract StringValues Apply(HttpContext context, StringValues values);
    }
}
