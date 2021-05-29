// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing.Template;

namespace Yarp.ReverseProxy.Transforms
{
    /// <summary>
    /// Generates a new request path by plugging matched route parameters into the given pattern.
    /// </summary>
    public class PathRouteValuesTransform : RequestTransform
    {
        private readonly TemplateBinderFactory _binderFactory;

        /// <summary>
        /// Creates a new transform.
        /// </summary>
        /// <param name="pattern">The pattern used to create the new request path.</param>
        /// <param name="binderFactory">The factory used to bind route parameters to the given path pattern.</param>
        public PathRouteValuesTransform(string pattern, TemplateBinderFactory binderFactory)
        {
            _ = pattern ?? throw new ArgumentNullException(nameof(pattern));
            _binderFactory = binderFactory ?? throw new ArgumentNullException(nameof(binderFactory));
            Template = TemplateParser.Parse(pattern);
        }

        internal RouteTemplate Template { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var routeValues = context.HttpContext.Request.RouteValues;
            // Route values that are not considered defaults will be appended as query parameters. Make them all defaults.
            var binder = _binderFactory.Create(Template, defaults: routeValues);
            context.Path = binder.BindValues(acceptedValues: routeValues);

            return default;
        }
    }
}
