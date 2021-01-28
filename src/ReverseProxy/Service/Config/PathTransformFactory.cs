// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Config
{
    internal class PathTransformFactory : ITransformFactory
    {
        internal static readonly string PathSetKey = "PathSet";
        internal static readonly string PathPrefixKey = "PathPrefix";
        internal static readonly string PathRemovePrefixKey = "PathRemovePrefix";
        internal static readonly string PathPatternKey = "PathPattern";

        private readonly TemplateBinderFactory _binderFactory;

        public PathTransformFactory(TemplateBinderFactory binderFactory)
        {
            _binderFactory = binderFactory ?? throw new ArgumentNullException(nameof(binderFactory));
        }

        public bool Validate(TransformValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(PathSetKey, out var pathSet))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 1);
            }
            else if (transformValues.TryGetValue(PathPrefixKey, out var pathPrefix))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 1);
            }
            else if (transformValues.TryGetValue(PathRemovePrefixKey, out var pathRemovePrefix))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 1);
            }
            else if (transformValues.TryGetValue(PathPatternKey, out var pathPattern))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 1);
                // TODO: Validate the pattern format. Does it build?
            }
            else
            {
                return false;
            }

            return true;
        }

        public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(PathSetKey, out var pathSet))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
                var path = MakePathString(pathSet);
                context.AddPathSet(path);
            }
            else if (transformValues.TryGetValue(PathPrefixKey, out var pathPrefix))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
                var path = MakePathString(pathPrefix);
                context.AddPathPrefix(path);
            }
            else if (transformValues.TryGetValue(PathRemovePrefixKey, out var pathRemovePrefix))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
                var path = MakePathString(pathRemovePrefix);
                context.AddPathRemovePrefix(path);
            }
            else if (transformValues.TryGetValue(PathPatternKey, out var pathPattern))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
                var path = MakePathString(pathPattern);
                // We don't use the extension here because we want to avoid doing a DI lookup for the binder every time.
                context.RequestTransforms.Add(new PathRouteValuesTransform(path.Value, _binderFactory));
            }
            else
            {
                return false;
            }

            return true;
        }

        private static PathString MakePathString(string path)
        {
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }
            return new PathString(path);
        }
    }

    public static class PathTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform which sets the request path with the given value.
        /// </summary>
        public static ProxyRoute WithTransformPathSet(this ProxyRoute proxyRoute, PathString path)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[PathTransformFactory.PathSetKey] = path.Value;
            });
        }

        /// <summary>
        /// Adds the transform which sets the request path with the given value.
        /// </summary>
        public static TransformBuilderContext AddPathSet(this TransformBuilderContext context, PathString path)
        {
            context.RequestTransforms.Add(new PathStringTransform(PathStringTransform.PathTransformMode.Set, path));
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will prefix the request path with the given value.
        /// </summary>
        public static ProxyRoute WithTransformPathPrefix(this ProxyRoute proxyRoute, PathString prefix)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[PathTransformFactory.PathPrefixKey] = prefix.Value;
            });
        }

        /// <summary>
        /// Adds the transform which will prefix the request path with the given value.
        /// </summary>
        public static TransformBuilderContext AddPathPrefix(this TransformBuilderContext context, PathString prefix)
        {
            context.RequestTransforms.Add(new PathStringTransform(PathStringTransform.PathTransformMode.Prefix, prefix));
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will remove the matching prefix from the request path.
        /// </summary>
        public static ProxyRoute WithTransformPathRemovePrefix(this ProxyRoute proxyRoute, PathString prefix)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[PathTransformFactory.PathRemovePrefixKey] = prefix.Value;
            });
        }

        /// <summary>
        /// Adds the transform which will remove the matching prefix from the request path.
        /// </summary>
        public static TransformBuilderContext AddPathRemovePrefix(this TransformBuilderContext context, PathString prefix)
        {
            context.RequestTransforms.Add(new PathStringTransform(PathStringTransform.PathTransformMode.RemovePrefix, prefix));
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will set the request path with the given value.
        /// </summary>
        public static ProxyRoute WithTransformPathRouteValues(this ProxyRoute proxyRoute, string pattern)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[PathTransformFactory.PathPatternKey] = pattern;
            });
        }

        /// <summary>
        /// Clones the route and adds the transform which will set the request path with the given value.
        /// </summary>
        public static TransformBuilderContext AddPathRouteValues(this TransformBuilderContext context, string pattern)
        {
            var binder = context.Services.GetRequiredService<TemplateBinderFactory>();
            context.RequestTransforms.Add(new PathRouteValuesTransform(pattern, binder));
            return context;
        }
    }
}
