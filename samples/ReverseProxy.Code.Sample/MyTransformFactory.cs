// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions.Config;

namespace Microsoft.ReverseProxy.Sample
{
    internal class MyTransformFactory : ITransformFactory
    {
        public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            return false;
        }

        public bool Validate(TransformValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            return false;
        }
    }
}
