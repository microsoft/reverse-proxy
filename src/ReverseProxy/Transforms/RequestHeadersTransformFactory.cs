// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

internal sealed class RequestHeadersTransformFactory : ITransformFactory
{
    internal static readonly string RequestHeadersCopyKey = "RequestHeadersCopy";
    internal static readonly string RequestHeaderOriginalHostKey = "RequestHeaderOriginalHost";
    internal static readonly string RequestHeaderKey = "RequestHeader";
    internal static readonly string RequestHeaderRemoveKey = "RequestHeaderRemove";
    internal static readonly string RequestHeadersAllowedKey = "RequestHeadersAllowed";
    internal static readonly string AppendKey = "Append";
    internal static readonly string SetKey = "Set";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(RequestHeadersCopyKey, out var copyHeaders))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            if (!bool.TryParse(copyHeaders, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected value for RequestHeaderCopy: {copyHeaders}. Expected 'true' or 'false'"));
            }
        }
        else if (transformValues.TryGetValue(RequestHeaderOriginalHostKey, out var originalHost))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            if (!bool.TryParse(originalHost, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected value for RequestHeaderOriginalHost: {originalHost}. Expected 'true' or 'false'"));
            }
        }
        else if (transformValues.TryGetValue(RequestHeaderKey, out var _))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            if (!transformValues.TryGetValue(SetKey, out var _) && !transformValues.TryGetValue(AppendKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for RequestHeader: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'"));
            }
        }
        else if (transformValues.TryGetValue(RequestHeaderRemoveKey, out var _))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
        }
        else if (transformValues.TryGetValue(RequestHeadersAllowedKey, out var _))
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
        if (transformValues.TryGetValue(RequestHeadersCopyKey, out var copyHeaders))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.CopyRequestHeaders = bool.Parse(copyHeaders);
        }
        else if (transformValues.TryGetValue(RequestHeaderOriginalHostKey, out var originalHost))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.AddOriginalHost(bool.Parse(originalHost));
        }
        else if (transformValues.TryGetValue(RequestHeaderKey, out var headerName))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            if (transformValues.TryGetValue(SetKey, out var setValue))
            {
                context.AddRequestHeader(headerName, setValue, append: false);
            }
            else if (transformValues.TryGetValue(AppendKey, out var appendValue))
            {
                context.AddRequestHeader(headerName, appendValue, append: true);
            }
            else
            {
                throw new ArgumentException($"Unexpected parameters for RequestHeader: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'");
            }
        }
        else if (transformValues.TryGetValue(RequestHeaderRemoveKey, out var removeHeaderName))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.AddRequestHeaderRemove(removeHeaderName);
        }
        else if (transformValues.TryGetValue(RequestHeadersAllowedKey, out var allowedHeaders))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var headersList = allowedHeaders.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            context.AddRequestHeadersAllowed(headersList);
        }
        else
        {
            return false;
        }

        return true;
    }
}
