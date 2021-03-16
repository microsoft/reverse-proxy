// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Kubernetes.Resources;
using Microsoft.Kubernetes.Testing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Testing
{
    public class TestCluster : ITestCluster
    {
        private readonly IResourceSerializers _serializers;

        public IList<ResourceObject> Resources { get; } = new List<ResourceObject>();

        public TestCluster(IOptions<TestClusterOptions> options, IResourceSerializers serializers)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _serializers = serializers ?? throw new ArgumentNullException(nameof(serializers));

            foreach (var resource in options.Value.InitialResources)
            {
                Resources.Add(_serializers.Convert<ResourceObject>(resource));
            }
        }

        public virtual Task UnhandledRequest(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public virtual Task<ListResult> ListResourcesAsync(string group, string version, string plural, ListParameters parameters)
        {
            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException($"'{nameof(version)}' cannot be null or empty", nameof(version));
            }

            if (string.IsNullOrEmpty(plural))
            {
                throw new ArgumentException($"'{nameof(plural)}' cannot be null or empty", nameof(plural));
            }

            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return Task.FromResult(new ListResult
            {
                ResourceVersion = parameters.ResourceVersion,
                Continue = null,
                Items = Resources.ToArray(),
            });
        }
    }
}
