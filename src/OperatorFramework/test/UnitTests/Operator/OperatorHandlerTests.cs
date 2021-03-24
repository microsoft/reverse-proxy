// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Kubernetes.Controller.Informers;
using Microsoft.Kubernetes.CustomResources;
using Microsoft.Kubernetes.Fakes;
using Microsoft.Kubernetes.Operator.Caches;
using Microsoft.Kubernetes.Operator.Generators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Operator
{
    [TestClass]
    public class OperatorHandlerTests
    {

        [TestMethod]
        public void NotifyWithPrimaryResourceCausesCacheEntryAndQueueItem()
        {
            // arrange
            var generator = Mock.Of<IOperatorGenerator<TypicalResource>>();
            var typicalInformer = new FakeResourceInformer<TypicalResource>();
            var podInformer = new FakeResourceInformer<V1Pod>();
            var addCalls = new List<NamespacedName>();
            var queue = new FakeQueue<NamespacedName>
            {
                OnAdd = addCalls.Add,
            };
            var cache = new OperatorCache<TypicalResource>();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services
                        .AddLogging()
                        .AddKubernetesOperatorRuntime()
                        .AddOperator<TypicalResource>(op =>
                        {
                            op.WithRelatedResource<V1Pod>();
                            op.Configure(options => options.NewRateLimitingQueue = _ => queue);
                        })
                        .AddSingleton(generator)
                        .AddSingleton<IResourceInformer<TypicalResource>>(typicalInformer)
                        .AddSingleton<IResourceInformer<V1Pod>>(podInformer)
                        .AddSingleton<IOperatorCache<TypicalResource>>(cache);
                })
                .Build();

            // act
            var handler = host.Services.GetRequiredService<IOperatorHandler<TypicalResource>>();

            var typical = new TypicalResource
            {
                ApiVersion = $"{TypicalResource.KubeGroup}/{TypicalResource.KubeApiVersion}",
                Kind = TypicalResource.KubeKind,
                Metadata = new V1ObjectMeta(
                    name: "test-name",
                    namespaceProperty: "test-namespace")
            };

            var unrelatedPod = new V1Pod
            {
                ApiVersion = TypicalResource.KubeApiVersion,
                Kind = TypicalResource.KubeKind,
                Metadata = new V1ObjectMeta(
                    name: "test-unrelated",
                    namespaceProperty: "test-namespace")
            };

            var relatedPod = new V1Pod(

                apiVersion: TypicalResource.KubeApiVersion,
                kind: TypicalResource.KubeKind,
                metadata: new V1ObjectMeta(
                    name: "test-related",
                    namespaceProperty: "test-namespace",
                    ownerReferences: new[]
                    {
                        new V1OwnerReference(
                            uid: typical.Uid(),
                            apiVersion: typical.ApiVersion,
                            kind: typical.Kind,
                            name: typical.Name())
                    }));

            typicalInformer.Callback(WatchEventType.Added, typical);
            podInformer.Callback(WatchEventType.Added, unrelatedPod);
            podInformer.Callback(WatchEventType.Added, relatedPod);

            // assert
            var expectedName = new NamespacedName("test-namespace", "test-name");
            addCalls.ShouldBe(new[] { expectedName, expectedName });

            cache.TryGetWorkItem(expectedName, out var cacheItem).ShouldBeTrue();

            cacheItem.Resource.ShouldBe(typical);

            var related = cacheItem.Related.ShouldHaveSingleItem();
            related.Key.ShouldBe(GroupKindNamespacedName.From(relatedPod));
            related.Value.ShouldBe(relatedPod);
        }
    }
}
