// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Config
{
    internal class RequestHeadersTransformFactory : ITransformFactory
    {
        internal static readonly string RequestHeadersCopyKey = "RequestHeadersCopy";
        internal static readonly string RequestHeaderOriginalHostKey = "RequestHeaderOriginalHost";
        internal static readonly string RequestHeaderKey = "RequestHeader";
        internal static readonly string AppendKey = "Append";
        internal static readonly string SetKey = "Set";

        public bool Validate(TransformValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(RequestHeadersCopyKey, out var copyHeaders))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 1);
                if (!bool.TryParse(copyHeaders, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for RequestHeaderCopy: {copyHeaders}. Expected 'true' or 'false'"));
                }
            }
            else if (transformValues.TryGetValue(RequestHeaderOriginalHostKey, out var originalHost))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 1);
                if (!bool.TryParse(copyHeaders, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for RequestHeaderOriginalHost: {originalHost}. Expected 'true' or 'false'"));
                }
            }
            else if (transformValues.TryGetValue(RequestHeaderKey, out var headerName))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 2);
                if (!transformValues.TryGetValue(SetKey, out var _) && !transformValues.TryGetValue(AppendKey, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected parameters for RequestHeader: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'"));
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
            if (transformValues.TryGetValue(RequestHeadersCopyKey, out var copyHeaders))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
                context.CopyRequestHeaders = bool.Parse(copyHeaders);
            }
            else if (transformValues.TryGetValue(RequestHeaderOriginalHostKey, out var originalHost))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
                context.UseOriginalHost = bool.Parse(originalHost);
            }
            else if (transformValues.TryGetValue(RequestHeaderKey, out var headerName))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                if (transformValues.TryGetValue(SetKey, out var setValue))
                {
                    context.RequestTransforms.Add(new RequestHeaderValueTransform(headerName, setValue, append: false));
                }
                else if (transformValues.TryGetValue(AppendKey, out var appendValue))
                {
                    context.RequestTransforms.Add(new RequestHeaderValueTransform(headerName, appendValue, append: true));
                }
                else
                {
                    throw new ArgumentException($"Unexpected parameters for RequestHeader: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'");
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
