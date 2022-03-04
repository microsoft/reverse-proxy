// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms;

internal sealed class ResponseTransformFactory : ITransformFactory
{
    internal static readonly string ResponseHeadersCopyKey = "ResponseHeadersCopy";
    internal static readonly string ResponseTrailersCopyKey = "ResponseTrailersCopy";
    internal static readonly string ResponseHeaderKey = "ResponseHeader";
    internal static readonly string ResponseTrailerKey = "ResponseTrailer";
    internal static readonly string ResponseHeaderRemoveKey = "ResponseHeaderRemove";
    internal static readonly string ResponseTrailerRemoveKey = "ResponseTrailerRemove";
    internal static readonly string ResponseHeadersAllowedKey = "ResponseHeadersAllowed";
    internal static readonly string ResponseTrailersAllowedKey = "ResponseTrailersAllowed";
    internal static readonly string WhenKey = "When";
    internal static readonly string AppendKey = "Append";
    internal static readonly string SetKey = "Set";

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(ResponseHeadersCopyKey, out var copyHeaders))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            if (!bool.TryParse(copyHeaders, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected value for ResponseHeadersCopy: {copyHeaders}. Expected 'true' or 'false'"));
            }
        }
        else if (transformValues.TryGetValue(ResponseTrailersCopyKey, out copyHeaders))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            if (!bool.TryParse(copyHeaders, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected value for ResponseTrailersCopy: {copyHeaders}. Expected 'true' or 'false'"));
            }
        }
        else if (transformValues.TryGetValue(ResponseHeaderKey, out var _))
        {
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 3);
                if (!Enum.TryParse<ResponseCondition>(whenValue, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for ResponseHeader:When: {whenValue}. Expected 'Always', 'Success', or 'Failure'"));
                }
            }
            else
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            }

            if (!transformValues.TryGetValue(SetKey, out var _) && !transformValues.TryGetValue(AppendKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for ResponseHeader: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'"));
            }
        }
        else if (transformValues.TryGetValue(ResponseTrailerKey, out var _))
        {
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 3);
                if (!Enum.TryParse<ResponseCondition>(whenValue, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for ResponseTrailer:When: {whenValue}. Expected 'Always', 'Success', or 'Failure'"));
                }
            }
            else
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
            }

            if (!transformValues.TryGetValue(SetKey, out var _) && !transformValues.TryGetValue(AppendKey, out var _))
            {
                context.Errors.Add(new ArgumentException($"Unexpected parameters for ResponseTrailer: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'"));
            }
        }
        else if (transformValues.TryGetValue(ResponseHeaderRemoveKey, out var _))
        {
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
                if (!Enum.TryParse<ResponseCondition>(whenValue, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for ResponseHeaderRemove:When: {whenValue}. Expected 'Always', 'Success', or 'Failure'"));
                }
            }
            else
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            }
        }
        else if (transformValues.TryGetValue(ResponseTrailerRemoveKey, out var _))
        {
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 2);
                if (!Enum.TryParse<ResponseCondition>(whenValue, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for ResponseTrailerRemove:When: {whenValue}. Expected 'Always', 'Success', or 'Failure'"));
                }
            }
            else
            {
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            }
        }
        else if (transformValues.TryGetValue(ResponseHeadersAllowedKey, out var _))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
        }
        else if (transformValues.TryGetValue(ResponseTrailersAllowedKey, out var _))
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
        if (transformValues.TryGetValue(ResponseHeadersCopyKey, out var copyHeaders))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.CopyResponseHeaders = bool.Parse(copyHeaders);
        }
        else if (transformValues.TryGetValue(ResponseTrailersCopyKey, out copyHeaders))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.CopyResponseTrailers = bool.Parse(copyHeaders);
        }
        else if (transformValues.TryGetValue(ResponseHeaderKey, out var responseHeaderName))
        {
            var condition = ResponseCondition.Success;
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 3);
                condition = Enum.Parse<ResponseCondition>(whenValue, ignoreCase: true);
            }
            else
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            }

            if (transformValues.TryGetValue(SetKey, out var setValue))
            {
                context.AddResponseHeader(responseHeaderName, setValue, append: false, condition);
            }
            else if (transformValues.TryGetValue(AppendKey, out var appendValue))
            {
                context.AddResponseHeader(responseHeaderName, appendValue, append: true, condition);
            }
            else
            {
                throw new ArgumentException($"Unexpected parameters for ResponseHeader: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'");
            }
        }
        else if (transformValues.TryGetValue(ResponseTrailerKey, out var responseTrailerName))
        {
            var condition = ResponseCondition.Success;
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 3);
                condition = Enum.Parse<ResponseCondition>(whenValue, ignoreCase: true);
            }
            else
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
            }

            if (transformValues.TryGetValue(SetKey, out var setValue))
            {
                context.AddResponseTrailer(responseTrailerName, setValue, append: false, condition);
            }
            else if (transformValues.TryGetValue(AppendKey, out var appendValue))
            {
                context.AddResponseTrailer(responseTrailerName, appendValue, append: true, condition);
            }
            else
            {
                throw new ArgumentException($"Unexpected parameters for ResponseTrailer: {string.Join(';', transformValues.Keys)}. Expected 'Set' or 'Append'");
            }
        }
        else if (transformValues.TryGetValue(ResponseHeaderRemoveKey, out var removeResponseHeaderName))
        {
            var condition = ResponseCondition.Success;
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                condition = Enum.Parse<ResponseCondition>(whenValue, ignoreCase: true);
            }
            else
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            }

            context.AddResponseHeaderRemove(removeResponseHeaderName, condition);
        }
        else if (transformValues.TryGetValue(ResponseTrailerRemoveKey, out var removeResponseTrailerName))
        {
            var condition = ResponseCondition.Success;
            if (transformValues.TryGetValue(WhenKey, out var whenValue))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 2);
                condition = Enum.Parse<ResponseCondition>(whenValue, ignoreCase: true);
            }
            else
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            }

            context.AddResponseTrailerRemove(removeResponseTrailerName, condition);
        }
        else if (transformValues.TryGetValue(ResponseHeadersAllowedKey, out var allowedHeaders))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var headersList = allowedHeaders.Split(';', options: StringSplitOptions.RemoveEmptyEntries
#if NET
                | StringSplitOptions.TrimEntries
#endif
                );
            context.AddResponseHeadersAllowed(headersList);
        }
        else if (transformValues.TryGetValue(ResponseTrailersAllowedKey, out var allowedTrailers))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            var headersList = allowedTrailers.Split(';', options: StringSplitOptions.RemoveEmptyEntries
#if NET
                | StringSplitOptions.TrimEntries
#endif
                );
            context.AddResponseTrailersAllowed(headersList);
        }
        else
        {
            return false;
        }

        return true;
    }
}
