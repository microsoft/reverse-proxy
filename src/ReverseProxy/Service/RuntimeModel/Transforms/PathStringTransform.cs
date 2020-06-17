// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Modifies the proxy request Path with the given value.
    /// </summary>
    internal class PathStringTransform : RequestParametersTransform
    {
        private readonly PathTransformMode _mode;
        private readonly PathString _value;

        public PathStringTransform(PathTransformMode mode, PathString value)
        {
            _mode = mode;
            _value = value;
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            switch (_mode)
            {
                case PathTransformMode.Set:
                    context.Path = _value;
                    break;
                case PathTransformMode.Prefix:
                    context.Path = _value + context.Path;
                    break;
                case PathTransformMode.RemovePrefix:
                    context.Path = context.Path.StartsWithSegments(_value, out var remainder) ? remainder : context.Path;
                    break;
                default:
                    throw new NotImplementedException(_mode.ToString());
            }
        }

        public enum PathTransformMode
        {
            Set,
            Prefix,
            RemovePrefix,
        }
    }
}
