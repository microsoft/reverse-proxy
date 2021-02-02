// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    public abstract class TransformExtentionsTestsBase
    {
        protected static TransformBuilderContext CreateBuilderContext(IServiceProvider services = null) => new()
        {
            Route = new ProxyRoute(),
            Services = services,
        };

        protected static void Validate(ITransformFactory factory, ProxyRoute proxyRoute, IReadOnlyDictionary<string, string> transformValues)
        {
            var validationContext = new TransformValidationContext { Route = proxyRoute };
            Assert.True(factory.Validate(validationContext, transformValues));
            Assert.Empty(validationContext.Errors);
        }

        protected static TransformBuilderContext ValidateAndBuild(ProxyRoute proxyRoute, ITransformFactory factory, IServiceProvider serviceProvider = null)
        {
            var transformValues = Assert.Single(proxyRoute.Transforms);
            Validate(factory, proxyRoute, transformValues);

            var builderContext = CreateBuilderContext(serviceProvider);
            Assert.True(factory.Build(builderContext, transformValues));

            return builderContext;
        }
    }
}
