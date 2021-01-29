// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// A response trailers transform that runs the given Func.
    /// </summary>
    public class ResponseTrailersFuncTransform : ResponseTrailersTransform
    {
        private readonly Func<ResponseTrailersTransformContext, Task> _func;

        public ResponseTrailersFuncTransform(Func<ResponseTrailersTransformContext, Task> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        /// <inheritdoc/>
        public override Task ApplyAsync(ResponseTrailersTransformContext context)
        {
            return _func(context);
        }
    }
}
