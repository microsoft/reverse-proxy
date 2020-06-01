// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
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

        public void Build(IList<IDictionary<string, string>> transforms, out IReadOnlyList<RequestParametersTransform> requestParamterTransforms)
        {
            requestParamterTransforms = null;
            if (transforms == null || transforms.Count == 0)
            {
                return;
            }

            var builtTransforms = new List<RequestParametersTransform>();

            foreach (var transform in transforms)
            {
                // TODO: Ensure path string formats like starts with /
                if (transform.TryGetValue("PathPrefix", out var pathPrefix))
                {
                    builtTransforms.Add(new PathStringTransform(PathStringTransform.TransformMode.Prepend, transformPathBase: false, new PathString(pathPrefix)));
                }
                else if (transform.TryGetValue("PathRemovePrefix", out var pathRemovePrefix))
                {
                    builtTransforms.Add(new PathStringTransform(PathStringTransform.TransformMode.RemovePrefix, transformPathBase: false, new PathString(pathRemovePrefix)));
                }
                else if (transform.TryGetValue("PathSet", out var pathSet))
                {
                    builtTransforms.Add(new PathStringTransform(PathStringTransform.TransformMode.Set, transformPathBase: false, new PathString(pathSet)));
                }
                else if (transform.TryGetValue("PathPattern", out var pathPattern))
                {
                    builtTransforms.Add(new PathRouteValueTransform(pathPattern, _binderFactory));
                }
                else
                {
                    // TODO: Make this a route validation error?
                    throw new NotImplementedException(string.Join(';', transform.Keys));
                }
            }

            requestParamterTransforms = builtTransforms.AsReadOnly();
        }
    }
}
