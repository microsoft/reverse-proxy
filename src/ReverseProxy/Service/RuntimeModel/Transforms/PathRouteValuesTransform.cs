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
        private readonly TemplateBinderFactory _binderFactory;

        public PathRouteValuesTransform(string pattern, TemplateBinderFactory binderFactory)
        {
            _ = pattern ?? throw new ArgumentNullException(nameof(pattern));
            _binderFactory = binderFactory ?? throw new ArgumentNullException(nameof(binderFactory));
            Template = TemplateParser.Parse(pattern);
        }

        internal RouteTemplate Template { get; }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var routeValues = context.HttpContext.Request.RouteValues;
            // Route values that are not considered defaults will be appended as query parameters. Make them all defaults.
            var binder = _binderFactory.Create(Template, defaults: routeValues);
            context.Path = binder.BindValues(acceptedValues: routeValues);
        }
    }
}
