// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Sets or appends simple request header values.
/// </summary>
public class RequestHeaderValueTransform : RequestTransform
{
    public RequestHeaderValueTransform(string headerName, string value, bool append)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        HeaderName = headerName;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Append = append;
    }

    internal string HeaderName { get; }

    internal string Value { get; }

    internal bool Append { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var existingValues = TakeHeader(context, HeaderName);

        if (Append)
        {
            var values = StringValues.Concat(existingValues, Value);
            AddHeader(context, HeaderName, values);
        }
        else
        {
            // Set
            AddHeader(context, HeaderName, Value);
        }

        return default;
    }
}
