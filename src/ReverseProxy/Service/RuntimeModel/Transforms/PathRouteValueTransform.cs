// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Generates a new request path by plugging matched route parameters into the given pattern.
    /// </summary>
    public class PathRouteValueTransform : RequestParametersTransform
    {
        private readonly TemplateBinder _binder;

        public PathRouteValueTransform(string pattern, TemplateBinderFactory binderFactory)
        {
            // TODO: Config validation
            _binder = binderFactory.Create(RoutePatternFactory.Parse(pattern));
        }

        public override void Run(RequestParametersTransformContext context)
        {
            context.Path = _binder.BindValues(context.HttpContext.Request.RouteValues);
        }
    }
}
