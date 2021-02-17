// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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

        protected static TransformBuilderContext ValidateAndBuild(ProxyRoute proxyRoute, ITransformFactory factory, IServiceProvider serviceProvider = null)
        {
            var transformValues = Assert.Single(proxyRoute.Transforms);

            var validationContext = new TransformRouteValidationContext { Route = proxyRoute };
            Assert.True(factory.Validate(validationContext, transformValues));
            Assert.Empty(validationContext.Errors);

            var builderContext = CreateBuilderContext(serviceProvider);
            Assert.True(factory.Build(builderContext, transformValues));

            return builderContext;
        }
    }
}
