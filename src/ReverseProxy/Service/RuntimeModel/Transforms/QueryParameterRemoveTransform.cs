// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// A request transform that removes the given query parameter.
    /// </summary>
    public class QueryParameterRemoveTransform : RequestTransform
    {
        public QueryParameterRemoveTransform(string key)
        {
            Key = key;
        }

        internal string Key { get; }

        /// <inheritdoc/>
        public override void Apply(RequestTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            context.Query.Collection.Remove(Key);
        }
    }
}
