// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Service.Model.Transforms
{
    /// <summary>
    /// A request transform that runs the given Func.
    /// </summary>
    public class RequestFuncTransform : RequestTransform
    {
        private readonly Func<RequestTransformContext, ValueTask> _func;

        public RequestFuncTransform(Func<RequestTransformContext, ValueTask> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            return _func(context);
        }
    }
}
