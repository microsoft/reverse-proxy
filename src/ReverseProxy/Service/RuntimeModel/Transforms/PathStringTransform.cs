// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Modifies the proxy request Path with the given value.
    /// </summary>
    public class PathStringTransform : RequestParametersTransform
    {
        public PathStringTransform(PathTransformMode mode, PathString value)
        {
            Mode = mode;
            Value = value;
        }

        internal PathString Value { get; }

        internal PathTransformMode Mode { get; }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            switch (Mode)
            {
                case PathTransformMode.Set:
                    context.Path = Value;
                    break;
                case PathTransformMode.Prefix:
                    context.Path = Value + context.Path;
                    break;
                case PathTransformMode.RemovePrefix:
                    context.Path = context.Path.StartsWithSegments(Value, out var remainder) ? remainder : context.Path;
                    break;
                default:
                    throw new NotImplementedException(Mode.ToString());
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
