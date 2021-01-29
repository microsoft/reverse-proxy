// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Config
{
    internal class QueryTransformFactory : ITransformFactory
    {
        internal static readonly string QueryValueParameterKey = "QueryValueParameter";
        internal static readonly string QueryRouteParameterKey = "QueryRouteParameter";
        internal static readonly string QueryRemoveParameterKey = "QueryRemoveParameter";
        internal static readonly string AppendKey = "Append";
        internal static readonly string SetKey = "Set";

        public bool Validate(TransformValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(QueryValueParameterKey, out var queryValueParameter))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 2);
                if (!transformValues.TryGetValue(AppendKey, out var _) && !transformValues.TryGetValue(SetKey, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected parameters for QueryValueParameter: {string.Join(';', transformValues.Keys)}. Expected 'Append' or 'Set'."));
                }
            }
            else if (transformValues.TryGetValue(QueryRouteParameterKey, out var queryRouteParameter))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 2);
                if (!transformValues.TryGetValue(AppendKey, out var _) && !transformValues.TryGetValue(SetKey, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected parameters for QueryRouteParameter: {string.Join(';', transformValues.Keys)}. Expected 'Append' or 'Set'."));
                }
            }
            else if (transformValues.TryGetValue(QueryRemoveParameterKey, out var removeQueryParameter))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 1);
            }
            else
            {
                return false;
            }

            return true;
        }

        public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(QueryValueParameterKey, out var queryValueParameter))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                if (transformValues.TryGetValue(AppendKey, out var appendValue))
                {
                    context.AddQueryValue(queryValueParameter, appendValue, append: true);
                }
                else if (transformValues.TryGetValue(SetKey, out var setValue))
                {
                    context.AddQueryValue(queryValueParameter, setValue, append: false);
                }
            }
            else if (transformValues.TryGetValue(QueryRouteParameterKey, out var queryRouteParameter))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                if (transformValues.TryGetValue(AppendKey, out var routeValueKeyAppend))
                {
                    context.AddQueryRouteValue(queryRouteParameter, routeValueKeyAppend, append: true);
                }
                else if (transformValues.TryGetValue(SetKey, out var routeValueKeySet))
                {
                    context.AddQueryRouteValue(queryRouteParameter, routeValueKeySet, append: false);
                }
                else
                {
                    throw new NotSupportedException(string.Join(";", transformValues.Keys));
                }
            }
            else if (transformValues.TryGetValue(QueryRemoveParameterKey, out var removeQueryParameter))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
                context.AddQueryRemoveKey(removeQueryParameter);
            }
            else
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Extensions for adding query transforms to the ProxyRoute or TransformBuilderContext.
    /// </summary>
    public static class QueryTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform that will append or set the query parameter from the given value.
        /// </summary>
        public static ProxyRoute WithTransformQueryValue(this ProxyRoute proxyRoute, string queryKey, string value, bool append = true)
        {
            var type = append ? QueryTransformFactory.AppendKey : QueryTransformFactory.SetKey;
            return proxyRoute.WithTransform(transform =>
            {
                transform[QueryTransformFactory.QueryValueParameterKey] = queryKey;
                transform[type] = value;
            });
        }

        /// <summary>
        /// Adds the transform that will append or set the query parameter from the given value.
        /// </summary>
        public static TransformBuilderContext AddQueryValue(this TransformBuilderContext context, string queryKey, string value, bool append = true)
        {
            context.RequestTransforms.Add(new QueryParameterFromStaticTransform(
                append ? QueryStringTransformMode.Append : QueryStringTransformMode.Set,
                queryKey, value));
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform that will append or set the query parameter from a route value.
        /// </summary>
        public static ProxyRoute WithTransformQueryRouteValue(this ProxyRoute proxyRoute, string queryKey, string routeValueKey, bool append = true)
        {
            var type = append ? QueryTransformFactory.AppendKey : QueryTransformFactory.SetKey;
            return proxyRoute.WithTransform(transform =>
            {
                transform[QueryTransformFactory.QueryRouteParameterKey] = queryKey;
                transform[type] = routeValueKey;
            });
        }

        /// <summary>
        /// Adds the transform that will append or set the query parameter from a route value.
        /// </summary>
        public static TransformBuilderContext AddQueryRouteValue(this TransformBuilderContext context, string queryKey, string routeValueKey, bool append = true)
        {
            context.RequestTransforms.Add(new QueryParameterRouteTransform(
                append ? QueryStringTransformMode.Append : QueryStringTransformMode.Set,
                queryKey, routeValueKey));
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform that will remove the given query key.
        /// </summary>
        public static ProxyRoute WithTransformQueryRemoveKey(this ProxyRoute proxyRoute, string queryKey)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[QueryTransformFactory.QueryRemoveParameterKey] = queryKey;
            });
        }

        /// <summary>
        /// Adds the transform that will remove the given query key.
        /// </summary>
        public static TransformBuilderContext AddQueryRemoveKey(this TransformBuilderContext context, string queryKey)
        {
            context.RequestTransforms.Add(new QueryParameterRemoveTransform(queryKey));
            return context;
        }
    }
}
