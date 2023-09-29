// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Sets or appends simple request header values.
/// </summary>
public class RequestHeaderValueTransform : RequestHeaderTransform
{
    public RequestHeaderValueTransform(string headerName, string value, bool append) : base(headerName, append)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal string Value { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (Append)
        {
            var existingValues = TakeHeader(context, HeaderName);
            var values = StringValues.Concat(existingValues, Value);
            AddHeader(context, HeaderName, values);
        }
        else
        {
            // Set
            RemoveHeader(context, HeaderName);
            AddHeader(context, HeaderName, Value);
        }

        return default;
    }

    /// <inheritdoc/>
    protected override string GetValue(RequestTransformContext context)
    {
        return Value;
    }
}
