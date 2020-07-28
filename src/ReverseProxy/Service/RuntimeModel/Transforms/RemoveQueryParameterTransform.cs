// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class RemoveQueryParameterTransform : RequestParametersTransform
    {
        private readonly string _key;

        public RemoveQueryParameterTransform(string key)
        {
            _key = key;
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            context.Query.RemoveQueryParameter(_key);
        }
    }
}
