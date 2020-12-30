// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple request header values.
    /// </summary>
    public class RequestHeaderValueTransform : RequestTransform
    {
        public RequestHeaderValueTransform(string name, string value, bool append)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Append = append;
        }

        internal string Name { get; }

        internal string Value { get; }

        internal bool Append { get; }

        /// <inheritdoc/>
        public override Task ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var existingValues = TakeHeader(context, Name);

            if (Append)
            {
                var values = StringValues.Concat(existingValues, Value);
                AddHeader(context, Name, values);
            }
            else if (!string.IsNullOrEmpty(Value))
            {
                // Set
                AddHeader(context, Name, Value);
            }

            return Task.CompletedTask;
        }
    }
}
