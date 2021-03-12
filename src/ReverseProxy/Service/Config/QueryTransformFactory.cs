// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Abstractions.Config;

namespace Yarp.ReverseProxy.Service.Config
{
    internal class QueryTransformFactory : ITransformFactory
    {
        internal static readonly string QueryValueParameterKey = "QueryValueParameter";
        internal static readonly string QueryRouteParameterKey = "QueryRouteParameter";
        internal static readonly string QueryRemoveParameterKey = "QueryRemoveParameter";
        internal static readonly string AppendKey = "Append";
        internal static readonly string SetKey = "Set";

        public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(QueryValueParameterKey, out var queryValueParameter))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
                if (!transformValues.TryGetValue(AppendKey, out var _) && !transformValues.TryGetValue(SetKey, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected parameters for QueryValueParameter: {string.Join(';', transformValues.Keys)}. Expected 'Append' or 'Set'."));
                }
            }
            else if (transformValues.TryGetValue(QueryRouteParameterKey, out var queryRouteParameter))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
                if (!transformValues.TryGetValue(AppendKey, out var _) && !transformValues.TryGetValue(SetKey, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected parameters for QueryRouteParameter: {string.Join(';', transformValues.Keys)}. Expected 'Append' or 'Set'."));
                }
            }
            else if (transformValues.TryGetValue(QueryRemoveParameterKey, out var removeQueryParameter))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
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
                else
                {
                    throw new NotSupportedException(string.Join(";", transformValues.Keys));
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
}
