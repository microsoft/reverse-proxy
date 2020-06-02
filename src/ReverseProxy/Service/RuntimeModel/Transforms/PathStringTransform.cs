// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class PathStringTransform : RequestParametersTransform
    {
        public PathStringTransform(TransformMode mode, bool transformPathBase, PathString value)
        {
            Mode = mode;
            TransformPathBase = transformPathBase;
            Value = value;
        }

        public PathString Value { get; }

        public TransformMode Mode { get; }

        public bool TransformPathBase { get; }

        public override void Apply(RequestParametersTransformContext context)
        {
            var input = TransformPathBase ? context.PathBase : context.Path;
            PathString result;
            switch (Mode)
            {
                case TransformMode.Set:
                    result = Value;
                    break;
                case TransformMode.Prepend:
                    result = Value + input;
                    break;
                case TransformMode.Append:
                    result = input + Value;
                    break;
                case TransformMode.RemovePrefix:
                    input.StartsWithSegments(Value, out result);
                    break;
                case TransformMode.RemoveEnd: // TODO:
                default:
                    throw new NotImplementedException(Mode.ToString());
            }

            if (TransformPathBase)
            {
                context.PathBase = result;
            }
            else
            {
                context.Path = result;
            }
        }

        public enum TransformMode
        {
            Set,
            Prepend,
            Append,
            RemovePrefix,
            RemoveEnd,
        }
    }
}
