// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Routing.Template;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Generates a new request path by plugging matched route parameters into the given pattern.
    /// </summary>
    internal class PathRouteValuesTransform : RequestParametersTransform
    {
        private readonly RouteTemplate _template;
        private readonly TemplateBinderFactory _binderFactory;

        public PathRouteValuesTransform(string pattern, TemplateBinderFactory binderFactory)
        {
            _ = pattern ?? throw new ArgumentNullException(nameof(pattern));
            _binderFactory = binderFactory ?? throw new ArgumentNullException(nameof(binderFactory));
            _template = TemplateParser.Parse(pattern);
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var routeValues = context.HttpContext.Request.RouteValues;
            // Route values that are not considered defaults will be appended as query parameters. Make them all defaults.
            var binder = _binderFactory.Create(_template, defaults: routeValues);
            context.Path = binder.BindValues(acceptedValues: routeValues);
        }
    }
}
