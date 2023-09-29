using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

public abstract class RequestHeaderTransform : RequestTransform
{
    protected RequestHeaderTransform(string headerName, bool append)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        Append = append;
        HeaderName = headerName;
    }

    internal bool Append { get; }
    internal string HeaderName { get; }

    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var value = GetValue(context);
        if (value is null)
        {
            return default;
        }

        if (Append)
        {
            var existingValues = TakeHeader(context, HeaderName);
            var newValue = StringValues.Concat(existingValues, value);
            AddHeader(context, HeaderName, newValue);
        }
        else
        {
            RemoveHeader(context, HeaderName);
            AddHeader(context, HeaderName, value);
        }

        return default;
    }

    protected abstract string? GetValue(RequestTransformContext context);
}

