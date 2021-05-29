// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Service.Model.Transforms
{
    /// <summary>
    /// A request transform that removes the given query parameter.
    /// </summary>
    public class QueryParameterRemoveTransform : RequestTransform
    {
        public QueryParameterRemoveTransform(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));
            }

            Key = key;
        }

        internal string Key { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            context.Query.Collection.Remove(Key);

            return default;
        }
    }
}
