// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Newtonsoft.Json;

namespace UsingCustomResources.Models
{
    /// <summary>
    /// TicketProvider contains the configuration information for handling an class of Ticket resources.
    /// The Ticket.spec.providerClass property should match exactly one TicketProvider.metadata.name to be processed.
    /// </summary>
    [KubernetesEntity(ApiVersion = KubeApiVersion, Group = KubeGroup, Kind = KubeKind, PluralName = "ticketproviders")]
    public class V1alpha1TicketProvider : IKubernetesObject<V1ObjectMeta>, ISpec<V1alpha1TicketProviderSpec>, IStatus<V1alpha1TicketProviderStatus>
    {
        /// <summary>
        /// The API Version this Kubernetes type belongs to.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// The Group this Kubernetes type belongs to.
        /// </summary>
        public const string KubeGroup = "ticketing.example.io";

        /// <summary>
        /// The Kubernetes named schema this object is based on.
        /// </summary>
        public const string KubeKind = "TicketProvider";

        [JsonProperty("apiVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("metadata")]
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// Spec is the configured state of the provider. It includes the provider type and any provider-specific
        /// properties and connection credentials.
        /// </summary>
        [JsonProperty("spec")]
        public V1alpha1TicketProviderSpec Spec { get; set; }

        /// <summary>
        /// Status is the current state of the provider.
        /// </summary>
        [JsonProperty("status")]
        public V1alpha1TicketProviderStatus Status { get; set; }
    }

    /// <summary>
    /// Spec is the configured state of the provider. It includes the provider type and any provider-specific
    /// properties and connection credentials.
    /// </summary>
    public class V1alpha1TicketProviderSpec
    {
        /// <summary>
        /// Identifies which backend work item system tracking system is
        /// in effect. This imaginary example supports `azure` and `github` as
        /// the backend for ticket tracking.
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Settings and credentials specific to the Azure DevOps work item tracking system.
        /// </summary>
        [JsonProperty("azure")]
        public V1alpha1TicketProviderSpecAzure Azure { get; set; }

        /// <summary>
        /// Settings and credentials specific to the GitHub issue tracking system.
        /// </summary>
        [JsonProperty("github")]
        public V1alpha1TicketProviderSpecGitHub GitHub { get; set; }
    }

    /// <summary>
    /// Settings and credentials specific to the Azure DevOps work item tracking system.
    /// </summary>
    public class V1alpha1TicketProviderSpecAzure
    {
        /// <summary>
        /// The Azure DevOps organization account name.
        /// </summary>
        [JsonProperty("organization")]
        public string Organization { get; set; }

        /// <summary>
        /// The Azure DevOps project name used for Work Item tracking.
        /// </summary>
        [JsonProperty("project")]
        public string Project { get; set; }

        /// <summary>
        /// The Azure DevOps area path for ticket creation.
        /// </summary>
        [JsonProperty("areaPath")]
        public string AreaPath { get; set; }

        /// <summary>
        /// The Kubernetes Secret metadata.name containing connection credentials.
        /// </summary>
        [JsonProperty("secretName")]
        public string SecretName { get; set; }
    }

    /// <summary>
    /// Settings and credentials specific to the GitHub issue tracking system.
    /// </summary>
    public class V1alpha1TicketProviderSpecGitHub
    {
        /// <summary>
        /// The GitHub organiation name.
        /// </summary>
        [JsonProperty("organization")]
        public string Organization { get; set; }

        /// <summary>
        /// The GitHub repository name used for Issue tracking.
        /// </summary>
        [JsonProperty("repository")]
        public string Repository { get; set; }

        /// <summary>
        /// The Kubernetes Secret metadata.name containing connection credentials.
        /// </summary>
        [JsonProperty("secretName")]
        public string SecretName { get; set; }
    }

    /// <summary>
    /// Status is the current state of the provider.
    /// </summary>
    public class V1alpha1TicketProviderStatus
    {
        /// <summary>
        /// Online is `true` when the backend provider is configured correctly
        /// and connected.
        /// </summary>
        [JsonProperty("online")]
        public bool? Online { get; set; }

        /// <summary>
        /// When Online is `false` Message contains the most recent error description
        /// or exception message.
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
