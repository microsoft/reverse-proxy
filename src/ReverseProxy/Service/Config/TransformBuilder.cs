// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Config
{
    public class TransformBuilder : ITransformBuilder
    {
        private readonly TemplateBinderFactory _binderFactory;

        public TransformBuilder(TemplateBinderFactory binderFactory)
        {
            _binderFactory = binderFactory;
        }

        public void Build(IList<IDictionary<string, string>> rawTransforms, out Transforms transforms)
        {
            transforms = null;
            if (rawTransforms == null || rawTransforms.Count == 0)
            {
                return;
            }

            bool? copyRequestHeaders = null;
            bool? useOriginalHost = null;
            var requestTransforms = new List<RequestParametersTransform>();
            var requestHeaderTransforms = new Dictionary<string, RequestHeaderTransform>();
            var responseHeaderTransforms = new Dictionary<string, ResponseHeaderTransform>();

            foreach (var rawTransform in rawTransforms)
            {
                // TODO: Ensure path string formats like starts with /
                if (rawTransform.TryGetValue("PathPrefix", out var pathPrefix))
                {
                    requestTransforms.Add(new PathStringTransform(PathStringTransform.TransformMode.Prepend, transformPathBase: false, new PathString(pathPrefix)));
                }
                else if (rawTransform.TryGetValue("PathRemovePrefix", out var pathRemovePrefix))
                {
                    requestTransforms.Add(new PathStringTransform(PathStringTransform.TransformMode.RemovePrefix, transformPathBase: false, new PathString(pathRemovePrefix)));
                }
                else if (rawTransform.TryGetValue("PathSet", out var pathSet))
                {
                    requestTransforms.Add(new PathStringTransform(PathStringTransform.TransformMode.Set, transformPathBase: false, new PathString(pathSet)));
                }
                else if (rawTransform.TryGetValue("PathPattern", out var pathPattern))
                {
                    requestTransforms.Add(new PathRouteValueTransform(pathPattern, _binderFactory));
                }
                else if (rawTransform.TryGetValue("RequestHeadersCopy", out var copyHeaders))
                {
                    copyRequestHeaders = string.Equals("True", copyHeaders, StringComparison.OrdinalIgnoreCase);
                }
                else if (rawTransform.TryGetValue("RequestHeaderOriginalHost", out var originalHost))
                {
                    useOriginalHost = string.Equals("True", originalHost, StringComparison.OrdinalIgnoreCase);
                }
                else if (rawTransform.TryGetValue("RequestHeader", out var headerName))
                {
                    if (rawTransform.TryGetValue("Set", out var setValue))
                    {
                        requestHeaderTransforms[headerName] = new RequestHeaderValueTransform(setValue, append: false);
                    }
                    else if (rawTransform.TryGetValue("Append", out var appendValue))
                    {
                        requestHeaderTransforms[headerName] = new RequestHeaderValueTransform(appendValue, append: true);
                    }
                    else
                    {
                        throw new NotImplementedException(string.Join(';', rawTransform.Keys));
                    }
                }
                else if (rawTransform.TryGetValue("ResponseHeader", out var responseHeaderName))
                {
                    var always = false;
                    if (rawTransform.TryGetValue("When", out var whenValue) && string.Equals("always", whenValue, StringComparison.OrdinalIgnoreCase))
                    {
                        always = true;
                    }

                    if (rawTransform.TryGetValue("Set", out var setValue))
                    {
                        responseHeaderTransforms[responseHeaderName] = new ResponseHeaderValueTransform(setValue, append: false, always);
                    }
                    else if (rawTransform.TryGetValue("Append", out var appendValue))
                    {
                        responseHeaderTransforms[responseHeaderName] = new ResponseHeaderValueTransform(appendValue, append: true, always);
                    }
                    else
                    {
                        throw new NotImplementedException(string.Join(';', rawTransform.Keys));
                    }
                }
                else
                {
                    // TODO: Make this a route validation error?
                    throw new NotImplementedException(string.Join(';', rawTransform.Keys));
                }
            }

            // If there's no transform defined for Host, suppress the host by default
            if (!requestHeaderTransforms.ContainsKey(HeaderNames.Host) && !(useOriginalHost ?? false))
            {
                requestHeaderTransforms[HeaderNames.Host] = new RequestHeaderValueTransform(null, append: false);
            }

            transforms = new Transforms(requestTransforms, copyRequestHeaders, requestHeaderTransforms, responseHeaderTransforms);
        }
    }
}
