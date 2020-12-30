// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-For header with the previous clients's IP address.
    /// </summary>
    public class RequestHeaderXForwardedForTransform : RequestTransform
    {
        /// <summary>
        /// Creates a new transform.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="append">Indicates if the new value should append to or replace an existing header.</param>
        public RequestHeaderXForwardedForTransform(string name, bool append)
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

            var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();

            if (remoteIp == null)
            {
                if (Append && !string.IsNullOrEmpty(existingValues))
                {
                    AddHeader(context, Name, existingValues);
                }
            }
            else if (Append)
            {
                var values = StringValues.Concat(existingValues, remoteIp);
                AddHeader(context, Name, values);
            }
            else
            {
                // Set
                AddHeader(context, Name, remoteIp);
            }

            return Task.CompletedTask;
        }
    }
}
