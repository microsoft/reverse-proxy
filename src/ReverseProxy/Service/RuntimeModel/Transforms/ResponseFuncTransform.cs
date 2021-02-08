// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// A response transform that runs the given Func.
    /// </summary>
    public class ResponseFuncTransform : ResponseTransform
    {
        private readonly Func<ResponseTransformContext, ValueTask> _func;

        public ResponseFuncTransform(Func<ResponseTransformContext, ValueTask> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(ResponseTransformContext context)
        {
            return _func(context);
        }
    }
}
