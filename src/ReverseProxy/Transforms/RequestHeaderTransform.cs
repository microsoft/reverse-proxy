using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

public enum RequestHeaderTransformMode
{
    Append,
    Set
}

public abstract class RequestHeaderTransform : RequestTransform
{
    protected RequestHeaderTransform(RequestHeaderTransformMode mode, string headerName)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        Mode = mode;
        HeaderName = headerName;
    }

    internal RequestHeaderTransformMode Mode { get; }
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

        switch (Mode)
        {
            case RequestHeaderTransformMode.Append:
                var existingValues = TakeHeader(context, HeaderName);
                var newValue = StringValues.Concat(existingValues, value);
                AddHeader(context, HeaderName, newValue);
                break;
            case RequestHeaderTransformMode.Set:
                RemoveHeader(context, HeaderName);
                AddHeader(context, HeaderName, value);
                break;
            default:
                throw new NotImplementedException(Mode.ToString());
        }

        return default;
    }

    protected abstract string? GetValue(RequestTransformContext context);
}

