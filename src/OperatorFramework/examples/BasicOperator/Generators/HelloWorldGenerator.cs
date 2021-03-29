// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BasicOperator.Models;
using k8s;
using k8s.Models;
using Microsoft.Kubernetes.Operator.Generators;
using Microsoft.Kubernetes.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BasicOperator.Generators
{
    public class HelloWorldGenerator : IOperatorGenerator<V1alpha1HelloWorld>
    {
        private readonly IResourceSerializers _serializers;
        private readonly string _managedBy;
        private readonly string _version;

        public HelloWorldGenerator(IResourceSerializers serializers)
        {
            _serializers = serializers;

            _managedBy = typeof(HelloWorldGenerator).Assembly
                .GetCustomAttributes(typeof(AssemblyTitleAttribute))
                .Cast<AssemblyTitleAttribute>()
                .Single()
                .Title;

            _version = typeof(HelloWorldGenerator).Assembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute))
                .Cast<AssemblyInformationalVersionAttribute>()
                .Single()
                .InformationalVersion;
        }

        public Task<GenerateResult> GenerateAsync(V1alpha1HelloWorld helloWorld)
        {
            if (helloWorld is null)
            {
                throw new ArgumentNullException(nameof(helloWorld));
            }

            var result = new GenerateResult();

            // create deployment
            var deployment = Add(CreateDeployment(helloWorld));

            // create service
            Add(CreateService(helloWorld, deployment));

            // create serviceaccount if needed
            if (helloWorld.Spec.CreateServiceAccount ?? false)
            {
                Add(CreateServiceAccount(helloWorld, deployment));
            }

            SetMetadataLabels(result.Resources, new Dictionary<string, string>
            {
                { "app.kubernetes.io/managed-by", _managedBy },
                { "app.kubernetes.io/version", _version },
            });

            result.ShouldReconcile = true;
            return Task.FromResult(result);

            // nested function to add resources easily
            T Add<T>(T t) where T : IKubernetesObject<V1ObjectMeta>
            {
                result.Resources.Add(t);
                return t;
            }
        }

        private V1Deployment CreateDeployment(V1alpha1HelloWorld helloWorld)
        {
            var deployment = _serializers.DeserializeYaml<V1Deployment>($@"
apiVersion: apps/v1
kind: Deployment
metadata:
    name: {helloWorld.Name()}
    namespace: {helloWorld.Namespace()}
spec:
    selector:
        matchLabels:
            app.kubernetes.io/instance: {helloWorld.Name()}
            app.kubernetes.io/component: kuard
    template:
        metadata:
            labels:
                app.kubernetes.io/instance: {helloWorld.Name()}
                app.kubernetes.io/component: kuard
        spec:
            containers:
            -   name: kuard
                image: gcr.io/kuar-demo/kuard-amd64:{helloWorld?.Spec?.KuardLabel ?? "blue"}
                ports:
                -   name: http
                    containerPort: 8080
            serviceAccountName: default
");

            deployment.Spec.Template.Spec.NodeSelector = helloWorld.Spec.NodeSelector;

            return deployment;
        }

        private V1Service CreateService(V1alpha1HelloWorld helloWorld, V1Deployment deployment)
        {
            var service = _serializers.DeserializeYaml<V1Service>($@"
apiVersion: v1
kind: Service
metadata:
    name: {helloWorld.Name()}
    namespace: {helloWorld.Namespace()}
spec:
    ports:
        -   name: http
            port: 80
            targetPort: http
");

            service.Spec.Type = (helloWorld.Spec.CreateLoadBalancer ?? false) ? "LoadBalancer" : "ClusterIP";
            service.Spec.Selector = deployment.Spec.Selector.MatchLabels;

            return service;
        }

        private V1ServiceAccount CreateServiceAccount(V1alpha1HelloWorld helloWorld, V1Deployment deployment)
        {
            var serviceAccount = _serializers.DeserializeYaml<V1ServiceAccount>($@"
apiVersion: v1
kind: ServiceAccount
metadata:
    name: {helloWorld.Name()}
    namespace: {helloWorld.Namespace()}
");

            deployment.Spec.Template.Spec.ServiceAccountName = serviceAccount.Name();

            return serviceAccount;
        }

        private static void SetMetadataLabels(IEnumerable<IKubernetesObject<V1ObjectMeta>> resources, IEnumerable<KeyValuePair<string, string>> labels)
        {
            foreach (var resource in resources)
            {
                foreach (var (key, value) in labels)
                {
                    resource.SetLabel(key, value);
                }

                if (resource is V1Deployment deployment)
                {
                    var podLabels = deployment.Spec.Template.Metadata.EnsureLabels();

                    foreach (var (key, value) in labels)
                    {
                        podLabels[key] = value;
                    }
                }
            }
        }
    }
}
