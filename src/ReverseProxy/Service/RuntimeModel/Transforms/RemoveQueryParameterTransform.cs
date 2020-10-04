// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class RemoveQueryParameterTransform : RequestParametersTransform
    {
        public RemoveQueryParameterTransform(string key)
        {
            Key = key;
        }

        internal string Key { get; }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            context.Query.Collection.Remove(Key);
        }
    }
}
