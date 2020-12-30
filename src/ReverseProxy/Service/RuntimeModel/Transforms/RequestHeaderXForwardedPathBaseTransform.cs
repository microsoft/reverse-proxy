// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-PathBase header with the request's original PathBase.
    /// </summary>
    public class RequestHeaderXForwardedPathBaseTransform : RequestTransform
    {

        public RequestHeaderXForwardedPathBaseTransform(string name, bool append)
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

            var pathBase = context.HttpContext.Request.PathBase;

            if (!pathBase.HasValue)
            {
                if (Append && !string.IsNullOrEmpty(existingValues))
                {
                    AddHeader(context, Name, existingValues);
                }
            }
            else if (Append)
            {
                var values = StringValues.Concat(existingValues, pathBase.ToUriComponent());
                AddHeader(context, Name, values);
            }
            else
            {
                // Set
                AddHeader(context, Name, pathBase.ToUriComponent());
            }

            return Task.CompletedTask;
        }
    }
}
