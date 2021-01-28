// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using System;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extension methods for <see cref="TransformBuilderContext"/>.
    /// </summary>
    public static class TransformBuilderContextFuncExtensions
    {
        /// <summary>
        /// Adds a transform Func that runs on each request for the given route.
        /// </summary>
        public static TransformBuilderContext AddRequestTransform(this TransformBuilderContext context, Func<RequestTransformContext, Task> func)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (func is null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            context.RequestTransforms.Add(new RequestFuncTransform(func));
            return context;
        }
    }
}
