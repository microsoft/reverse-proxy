// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public abstract class ResponseHeaderTransform
    {
        public abstract StringValues Apply(HttpContext context, HttpResponseMessage response, StringValues values);
    }
}
