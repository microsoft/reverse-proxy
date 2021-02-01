// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    public abstract class TransformExtentionsTestsBase
    {
        protected static ProxyRoute CreateProxyRoute(bool suppressDefaults = false)
        {
            if (suppressDefaults)
            {
                return new ProxyRoute
                {
                    // With defaults turned off.
                    Transforms = new List<IReadOnlyDictionary<string, string>>()
                    {
                        new Dictionary<string, string>()
                        {
                            { "RequestHeaderOriginalHost", "true" }
                        },
                        new Dictionary<string, string>()
                        {
                            { "X-Forwarded", "" }
                        }
                    }
                };
            }

            return new ProxyRoute();
        }

        protected static TransformValidationContext CreateValidationContext(ProxyRoute route, IServiceProvider services = null) => new()
        {
            Route = route,
            Services = services,
            Errors = new List<Exception>(),
        };

        protected static TransformBuilderContext CreateBuilderContext(ProxyRoute route, IServiceProvider services = null) => new()
        {
            Route = route,
            Services = services,
        };

        protected static void Validate(ITransformFactory factory, ProxyRoute proxyRoute, IReadOnlyDictionary<string, string> transformValues)
        {
            var validationContext = CreateValidationContext(proxyRoute);
            Assert.True(factory.Validate(validationContext, transformValues));
            Assert.Empty(validationContext.Errors);
        }
    }
}
