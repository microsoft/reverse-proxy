// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// A request transform that runs the given Func.
    /// </summary>
    public class RequestFuncTransform : RequestTransform
    {
        private readonly Func<RequestTransformContext, Task> _func;

        public RequestFuncTransform(Func<RequestTransformContext, Task> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        /// <inheritdoc/>
        public override Task ApplyAsync(RequestTransformContext context)
        {
            return _func(context);
        }
    }
}
