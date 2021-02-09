// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Abstractions.Config;

namespace Microsoft.ReverseProxy.Service.Config
{
    internal class ActionTransformProvider : ITransformProvider
    {
        private readonly Action<TransformBuilderContext> _action;

        public ActionTransformProvider(Action<TransformBuilderContext> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void Apply(TransformBuilderContext transformBuildContext)
        {
            _action(transformBuildContext);
        }

        public void ValidateRoute(TransformValidationContext context)
        {
        }

        public void ValidateCluster(TransformValidationContext context)
        {
        }
    }
}
