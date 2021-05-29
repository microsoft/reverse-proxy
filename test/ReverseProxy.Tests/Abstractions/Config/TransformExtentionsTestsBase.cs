// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;
using Yarp.ReverseProxy.Discovery;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    public abstract class TransformExtentionsTestsBase
    {
        protected static TransformBuilderContext CreateBuilderContext(IServiceProvider services = null) => new()
        {
            Route = new RouteConfig(),
            Services = services,
        };

        protected static TransformBuilderContext ValidateAndBuild(RouteConfig routeConfig, ITransformFactory factory, IServiceProvider serviceProvider = null)
        {
            var transformValues = Assert.Single(routeConfig.Transforms);

            var validationContext = new TransformRouteValidationContext { Route = routeConfig };
            Assert.True(factory.Validate(validationContext, transformValues));
            Assert.Empty(validationContext.Errors);

            var builderContext = CreateBuilderContext(serviceProvider);
            Assert.True(factory.Build(builderContext, transformValues));

            return builderContext;
        }
    }
}
