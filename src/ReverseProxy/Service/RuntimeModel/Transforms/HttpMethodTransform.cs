// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class HttpMethodTransform : RequestParametersTransform
    {
        public HttpMethodTransform(string fromMethod, string toMethod)
        {
#if NET5_0 || NETCOREAPP5_0
            FromMethod = HttpMethods.GetCanonicalizedValue(fromMethod);
            ToMethod = HttpMethods.GetCanonicalizedValue(toMethod);
#elif NETCOREAPP3_1
            FromMethod = GetCanonicalizedValue(fromMethod);
            ToMethod = GetCanonicalizedValue(toMethod);
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
        }

        internal string ToMethod { get; }

        internal string FromMethod { get; }

        public override void Apply(RequestParametersTransformContext context)
        {
#if NET5_0 || NETCOREAPP5_0
            if (HttpMethods.Equals(FromMethod, context.Method))
#elif NETCOREAPP3_1
            if (HttpMethodEquals(FromMethod, context.Method))
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
            {
                context.Method = ToMethod;
            }
        }

#if NETCOREAPP3_1
        private static string GetCanonicalizedValue(string method) => method switch
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

        private static bool HttpMethodEquals(string methodA, string methodB)
        {
            return object.ReferenceEquals(methodA, methodB) || StringComparer.OrdinalIgnoreCase.Equals(methodA, methodB);
        }
#endif

    }
}
