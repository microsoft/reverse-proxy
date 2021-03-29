// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace BasicOperator.Models
{
    [KubernetesEntity(ApiVersion = KubeApiVersion, Group = KubeGroup, Kind = KubeKind, PluralName = "helloworlds")]
    public class V1alpha1HelloWorld : IKubernetesObject<V1ObjectMeta>, ISpec<V1alpha1HelloWorldSpec>, IStatus<V1alpha1HelloWorldStatus>
    {
        public const string KubeApiVersion = "v1alpha1";
        public const string KubeGroup = "basic-operator.example.io";
        public const string KubeKind = "HelloWorld";

        /// <inheritdoc/>
        [JsonProperty("apiVersion")]
        public string ApiVersion { get; set; }

        /// <inheritdoc/>
        [JsonProperty("kind")]
        public string Kind { get; set; }

        /// <inheritdoc/>
        [JsonProperty("metadata")]
        public V1ObjectMeta Metadata { get; set; }

        [JsonProperty("spec")]
        public V1alpha1HelloWorldSpec Spec { get; set; }

        [JsonProperty("status")]
        public V1alpha1HelloWorldStatus Status { get; set; }
    }

    public class V1alpha1HelloWorldSpec
    {
        /// <summary>
        /// Select a kuard image label - will be "blue" by default
        /// </summary>
        [JsonProperty("kuardLabel")]
        public string KuardLabel { get; set; }

        [JsonProperty("createServiceAccount")]
        public bool? CreateServiceAccount { get; set; }

        [JsonProperty("createLoadBalancer")]
        public bool? CreateLoadBalancer { get; set; }

        /// <summary>
        /// Gets or sets nodeSelector is a selector which must be true for the pod to fit
        /// on a node.Selector which must match a node's labels for the pod to be scheduled
        /// on that node.More info: https://kubernetes.io/docs/concepts/configuration/assign-pod-node/
        /// </summary>
        [JsonProperty("nodeSelector")]
#pragma warning disable CA2227 // Collection properties should be read only
        public IDictionary<string, string> NodeSelector { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }

    public class V1alpha1HelloWorldStatus
    {
    }
}
