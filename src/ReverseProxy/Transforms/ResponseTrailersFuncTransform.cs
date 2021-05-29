// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Service.Model.Transforms
{
    /// <summary>
    /// A response trailers transform that runs the given Func.
    /// </summary>
    public class ResponseTrailersFuncTransform : ResponseTrailersTransform
    {
        private readonly Func<ResponseTrailersTransformContext, ValueTask> _func;

        public ResponseTrailersFuncTransform(Func<ResponseTrailersTransformContext, ValueTask> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(ResponseTrailersTransformContext context)
        {
            return _func(context);
        }
    }
}
