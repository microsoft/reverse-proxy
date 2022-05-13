// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Yarp.Kubernetes.Tests.TestCluster.Models;

public class ResourceObject : IKubernetesObject<V1ObjectMeta>
{
    [JsonProperty("apiVersion")]
    public string ApiVersion { get; set; }

    [JsonProperty("kind")]
    public string Kind { get; set; }

    [JsonProperty("metadata")]
    public V1ObjectMeta Metadata { get; set; }

    [JsonExtensionData]
#pragma warning disable CA2227 // Collection properties should be read only
    public IDictionary<string, JToken> AdditionalData { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
}
