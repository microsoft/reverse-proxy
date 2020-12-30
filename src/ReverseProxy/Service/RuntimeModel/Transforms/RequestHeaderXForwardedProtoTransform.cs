// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Proto header with the request's original url scheme.
    /// </summary>
    public class RequestHeaderXForwardedProtoTransform : RequestTransform
    {
        public RequestHeaderXForwardedProtoTransform(string name, bool append)
        {
            Name = name ?? throw new System.ArgumentNullException(nameof(name));
            Append = append;
        }

        internal string Name { get; }
        internal bool Append { get; }

        /// <inheritdoc/>
        public override Task ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var existingValues = TakeHeader(context, Name);

            var scheme = context.HttpContext.Request.Scheme;

            if (Append)
            {
                var values = StringValues.Concat(existingValues, scheme);
                AddHeader(context, Name, values);
            }
            else
            {
                // Set
                AddHeader(context, Name, scheme);
            }

            return Task.CompletedTask;
        }
    }
}
