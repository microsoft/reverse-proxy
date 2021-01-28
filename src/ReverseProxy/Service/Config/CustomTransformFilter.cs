// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Abstractions.Config;

namespace Microsoft.ReverseProxy.Service.Config
{
    internal class CustomTransformFilter : ITransformFilter
    {
        private readonly Action<TransformBuilderContext> _action;

        public CustomTransformFilter(Action<TransformBuilderContext> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void Apply(TransformBuilderContext transformBuildContext)
        {
            _action(transformBuildContext);
        }
    }
}
