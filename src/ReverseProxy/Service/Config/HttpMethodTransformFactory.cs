// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions.Config;

namespace Microsoft.ReverseProxy.Service.Config
{
    internal class HttpMethodTransformFactory : ITransformFactory
    {
        internal static readonly string HttpMethodKey = "HttpMethod";
        internal static readonly string SetKey = "Set";

        public bool Validate(TransformValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(HttpMethodKey, out var _))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                if (!transformValues.TryGetValue(SetKey, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected parameters for HttpMethod: {string.Join(';', transformValues.Keys)}. Expected 'Set'"));
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(HttpMethodKey, out var fromHttpMethod))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                if (transformValues.TryGetValue(SetKey, out var toHttpMethod))
                {
                    context.ChangeHttpMethod(fromHttpMethod, toHttpMethod);
                }
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
