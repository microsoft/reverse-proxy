// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Host header with the request's original Host header.
    /// </summary>
    public class RequestHeaderXForwardedHostTransform : RequestTransform
    {
        /// <summary>
        /// Creates a new transform.
        /// </summary>
        /// <param name="append">Indicates if the new value should append to or replace an existing header.</param>
        public RequestHeaderXForwardedHostTransform(string name, bool append)
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

            var host = context.HttpContext.Request.Host;

            if (!host.HasValue)
            {
                if (Append && !string.IsNullOrEmpty(existingValues))
                {
                    AddHeader(context, Name, existingValues);
                }
            }
            else if (Append)
            {
                var values = StringValues.Concat(existingValues, host.ToUriComponent());
                AddHeader(context, Name, values);
            }
            else
            {
                // Set
                AddHeader(context, Name, host.ToUriComponent());
            }

            return Task.CompletedTask;
        }
    }
}
