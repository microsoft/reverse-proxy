// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Newtonsoft.Json;

namespace Microsoft.Kubernetes.CustomResources;

/// <summary>
/// TypicalResource doc comment
/// </summary>
[KubernetesEntity(ApiVersion = KubeApiVersion, Group = KubeGroup, Kind = KubeKind, PluralName = "typicals")]
public class TypicalResource : IKubernetesObject<V1ObjectMeta>, ISpec<TypicalResourceSpec>, IStatus<TypicalResourceStatus>
{
    public const string KubeApiVersion = "v1alpha1";
    public const string KubeGroup = "test-group";
    public const string KubeKind = "Typical";

    [JsonProperty("apiVersion")]
    public string ApiVersion { get; set; }

    [JsonProperty("kind")]
    public string Kind { get; set; }

    [JsonProperty("metadata")]
    public V1ObjectMeta Metadata { get; set; }

    /// <summary>
    /// Spec doc comment
    /// </summary>
    [JsonProperty("spec")]
    public TypicalResourceSpec Spec { get; set; }

    /// <summary>
    /// Status doc comment
    /// </summary>
    [JsonProperty("status")]
    public TypicalResourceStatus Status { get; set; }
}

public class TypicalResourceSpec
{

}

public class TypicalResourceStatus
{

}
