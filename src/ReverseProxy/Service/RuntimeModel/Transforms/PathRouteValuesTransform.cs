// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Generates a new request path by plugging matched route parameters into the given pattern.
    /// </summary>
    public class PathRouteValuesTransform : RequestParametersTransform
    {
        private readonly TemplateBinder _binder;

        public PathRouteValuesTransform(string pattern, TemplateBinderFactory binderFactory)
        {
            if (pattern is null)
            {
                throw new System.ArgumentNullException(nameof(pattern));
            }

            if (binderFactory is null)
            {
                throw new System.ArgumentNullException(nameof(binderFactory));
            }

            _binder = binderFactory.Create(RoutePatternFactory.Parse(pattern));
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            context.Path = _binder.BindValues(context.HttpContext.Request.RouteValues);
        }
    }
}
