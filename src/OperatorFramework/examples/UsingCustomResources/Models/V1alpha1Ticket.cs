// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Newtonsoft.Json;

namespace UsingCustomResources.Models
{
    /// <summary>
    /// A Ticket represents a tracking record created in a work item database. For example, a
    /// microservice which has observed a low-priority condition that requires human interventions could create
    /// Ticket to be routed to the appropriate on-call staff. Is not intended to replace metric-based alerts
    /// for real-time notification.
    /// </summary>
    [KubernetesEntity(ApiVersion = KubeApiVersion, Group = KubeGroup, Kind = KubeKind, PluralName = "tickets")]
    public class V1alpha1Ticket : IKubernetesObject<V1ObjectMeta>, ISpec<V1alpha1TicketSpec>, IStatus<V1alpha1TicketStatus>
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
        public const string KubeKind = "Ticket";

        [JsonProperty("apiVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("metadata")]
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// Spec contains the desired state of a work item that should be created in a related
        /// tracking database, e.g. GitHub Issues or AzureDevOps WorkItems.
        /// </summary>
        [JsonProperty("spec")]
        public V1alpha1TicketSpec Spec { get; set; }

        /// <summary>
        /// Status contains the current state of the work item as it stands in the tracking database.
        /// </summary>
        [JsonProperty("status")]
        public V1alpha1TicketStatus Status { get; set; }
    }

    /// <summary>
    /// Spec contains the desired state of a work item that should be created in a related
    /// tracking database, e.g. GitHub Issues or AzureDevOps WorkItems.
    /// </summary>
    public class V1alpha1TicketSpec
    {
        /// <summary>
        /// ProviderClass must match the Metadata.Name of a TicketProvider in the cluster
        /// in order to be processed.
        /// </summary>
        [JsonProperty("providerClass")]
        public string ProviderClass { get; set; }

        /// <summary>
        /// Severity is a system-specific value, e.g. high, low, or a string representation of a numeric value.
        /// </summary>
        [JsonProperty("severity")]
        public string Severity { get; set; }

        /// <summary>
        /// Contains the summary of the issue that is being reported.
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }
    }

    /// <summary>
    /// Status contains the current state of the work item as it stands in the tracking database.
    /// </summary>
    public class V1alpha1TicketStatus
    {
        /// <summary>
        /// Unique ticket identifier assigned by provider backend system.
        /// </summary>
        [JsonProperty("uniqueId")]
        public string UniqueId { get; set; }

        /// <summary>
        /// A human-readable state value of the ticket determined by the work item tracking provider.
        /// </summary>
        [JsonProperty("workflowState")]
        public string WorkflowState { get; set; }

        /// <summary>
        /// Browser location which may be used to navigate to ticket on the
        /// provider's user interface.
        /// </summary>
        [JsonProperty("webUrl")]
        public string WebUrl { get; set; }

        /// <summary>
        /// Email address or other information to contact the person assigned to this
        /// ticket directly.
        /// </summary>
        [JsonProperty("contactInfo")]
        public string ContactInfo { get; set; }
    }
}
