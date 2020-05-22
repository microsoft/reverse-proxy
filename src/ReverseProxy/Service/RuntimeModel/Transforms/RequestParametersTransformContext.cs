// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestParametersTransformContext
    {
        public HttpContext HttpContext { get; internal set; }
        public string Method { get; set; }
        public PathString PathBase { get; set; }
        public PathString Path { get; set; }
        public QueryString Query { get; set; }
    }
}
