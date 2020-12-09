// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class HttpMethodTransform : RequestParametersTransform
    {
        public HttpMethodTransform(string fromMethod, string toMethod)
        {
            FromMethod = GetCanonicalizedValue(fromMethod);
            ToMethod = HttpUtilities.GetHttpMethod(toMethod);
        }

        internal HttpMethod ToMethod { get; }

        internal string FromMethod { get; }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (HttpMethodEquals(FromMethod, context.Request.Method.Method))
            {
                context.Request.Method = ToMethod;
            }
        }

        private static string GetCanonicalizedValue(string method)
        {
#if NET5_0
            return HttpMethods.GetCanonicalizedValue(method);
#elif NETCOREAPP3_1
            return method switch
            {
                string _ when HttpMethods.IsGet(method) => HttpMethods.Get,
                string _ when HttpMethods.IsPost(method) => HttpMethods.Post,
                string _ when HttpMethods.IsPut(method) => HttpMethods.Put,
                string _ when HttpMethods.IsDelete(method) => HttpMethods.Delete,
                string _ when HttpMethods.IsOptions(method) => HttpMethods.Options,
                string _ when HttpMethods.IsHead(method) => HttpMethods.Head,
                string _ when HttpMethods.IsPatch(method) => HttpMethods.Patch,
                string _ when HttpMethods.IsTrace(method) => HttpMethods.Trace,
                string _ when HttpMethods.IsConnect(method) => HttpMethods.Connect,
                string _ => method
            };
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
        }

        private static bool HttpMethodEquals(string methodA, string methodB)
        {
#if NET5_0
            return HttpMethods.Equals(methodA, methodB);
#elif NETCOREAPP3_1
            return object.ReferenceEquals(methodA, methodB) || System.StringComparer.OrdinalIgnoreCase.Equals(methodA, methodB);
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
        }
    }
}
