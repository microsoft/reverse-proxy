// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes.Client;
using Microsoft.Kubernetes.Operator.Generators;
using Microsoft.Kubernetes.ResourceKinds;
using Microsoft.Kubernetes.Resources;
using Microsoft.Rest;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA2213 // Disposable fields should be disposed

namespace Microsoft.Kubernetes.Operator.Reconcilers;

/// <summary>
/// Class OperatorReconciler.
/// Implements the <see cref="IOperatorReconciler{TResource}" />.
/// </summary>
/// <typeparam name="TResource">The type of the t resource.</typeparam>
/// <seealso cref="IOperatorReconciler{TResource}" />
public class OperatorReconciler<TResource> : IOperatorReconciler<TResource>
    where TResource : class, IKubernetesObject<V1ObjectMeta>
{
    private readonly IResourceSerializers _resourceSerializers;
    private readonly IAnyResourceKind _resourceClient;
    private readonly IOperatorGenerator<TResource> _generator;
    private readonly IResourceKindManager _resourceKindManager;
    private readonly IResourcePatcher _resourcePatcher;
    private readonly ILogger<OperatorReconciler<TResource>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperatorReconciler{TResource}" /> class.
    /// </summary>
    /// <param name="resourceSerializers">The resource serializers.</param>
    /// <param name="resourcePatcher">The patch generator.</param>
    /// <param name="generator">The generator.</param>
    /// <param name="client">The client.</param>
    /// <param name="logger">The logger.</param>
    public OperatorReconciler(
        IResourceSerializers resourceSerializers,
        IKubernetes client,
        IOperatorGenerator<TResource> generator,
        IResourceKindManager resourceKindManager,
        IResourcePatcher resourcePatcher,
        ILogger<OperatorReconciler<TResource>> logger)
    {
        _resourceSerializers = resourceSerializers;
        _resourceClient = client.AnyResourceKind();
        _generator = generator;
        _resourceKindManager = resourceKindManager;
        _resourcePatcher = resourcePatcher;
        _logger = logger;
    }

    /// <summary>
    /// Enum UpdateType provides a dimension value for logs and counters.
    /// </summary>
    public enum UpdateType
    {
        /// <summary>
        /// A resource is being created.
        /// </summary>
        Creating,

        /// <summary>
        /// A resource is being deleted.
        /// </summary>
        Deleting,

        /// <summary>
        /// A resource is being patched.
        /// </summary>
        Patching,
    }

    /// <inheritdoc/>
    public async Task<ReconcileResult> ReconcileAsync(ReconcileParameters<TResource> parameters, CancellationToken cancellationToken)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (parameters.Resource is null)
        {
            // Custom resource has been deleted - k8s garbage collection will do the rest of the work
            return default;
        }

        var resource = parameters.Resource;

        var liveResources = parameters.RelatedResources;

        try
        {
            _logger.LogDebug(
                new EventId(1, "ReconcileStarting"),
                "Reconcile starting for {ItemKind}/{ItemName}.{ItemNamespace}",
                resource.Kind,
                resource.Name(),
                resource.Namespace());

            var generatedResources = await _generator.GenerateAsync(resource);

            if (generatedResources.ShouldReconcile == false)
            {
                // the resource is not in a state which can be reconciled
                // return default results and let the next informer notification
                // cause a reconciliation
                return default;
            }

            foreach (var generatedResource in generatedResources.Resources)
            {
                var generatedKey = GroupKindNamespacedName.From(generatedResource);
                if (!liveResources.TryGetValue(generatedKey, out var liveResource))
                {
                    var ownerReference = new V1OwnerReference(
                        apiVersion: resource.ApiVersion,
                        kind: resource.Kind,
                        name: resource.Name(),
                        uid: resource.Uid());

                    await CreateGeneratedResourceAsync(generatedResource, ownerReference, cancellationToken);
                }
                else
                {
                    var patch = await CalculateJsonPatchDocumentAsync(generatedResource, liveResource);

                    if (patch.Operations.Any())
                    {
                        try
                        {
                            await PatchResourceAsync(generatedResource, liveResource, patch, cancellationToken);
                        }
                        catch (ReconcilerException ex) when (ex.Status.Reason == "Invalid" && (ex.Status.Details?.Causes?.Any() ?? false))
                        {
                            _logger.LogWarning(
                                new EventId(7, "PatchDeleteFallback"),
                                "Deleting existing resource due to patch failure: {PatchMessage}",
                                ex.Status.Message);

                            await DeleteLiveResourceAsync(liveResource, cancellationToken);
                            return default;
                        }
                    }
                }
            }

            var generatedResourceKeys = generatedResources.Resources.ToDictionary(GroupKindNamespacedName.From);
            foreach (var (liveResourceKey, liveResource) in liveResources)
            {
                if (!generatedResourceKeys.ContainsKey(liveResourceKey))
                {
                    // existing resource has an ownerReference which is no longer generated by this operator
                    // it's deleted here because k8s garbage collection won't know to remove it
                    await DeleteLiveResourceAsync(liveResource, cancellationToken);
                }
            }

            return default;
        }
        finally
        {
            _logger.LogDebug(
                new EventId(2, "ReconcileComplete"),
                "Reconcile completed for {ItemKind}/{ItemName}.{ItemNamespace}",
                resource.Kind,
                resource.Name(),
                resource.Namespace());
        }
    }

    private async Task CreateGeneratedResourceAsync(
        IKubernetesObject<V1ObjectMeta> generatedResource,
        V1OwnerReference ownerReference,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                new EventId(3, "CreatingResource"),
                "{UpdateType} resource {ItemKind}/{ItemName}.{ItemNamespace}",
                UpdateType.Creating,
                generatedResource.Kind,
                generatedResource.Name(),
                generatedResource.Namespace());

            generatedResource.SetAnnotation(
                "kubectl.kubernetes.io/last-applied-configuration",
                _resourceSerializers.SerializeJson(generatedResource));

            generatedResource.AddOwnerReference(ownerReference);

            var kubernetesEntity = generatedResource.GetType().GetCustomAttribute<KubernetesEntityAttribute>();

            using var resultResource = await _resourceClient.CreateAnyResourceKindWithHttpMessagesAsync(
                body: generatedResource,
                group: generatedResource.ApiGroup(),
                version: generatedResource.ApiGroupVersion(),
                namespaceParameter: generatedResource.Namespace() ?? string.Empty,
                plural: kubernetesEntity?.PluralName,
                cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            if (TryNewReconcilerException(ex, out var reconcilerException))
            {
                throw reconcilerException;
            }
            throw;
        }
    }

    private async Task DeleteLiveResourceAsync(
        IKubernetesObject<V1ObjectMeta> existingResource,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            new EventId(4, "DeletingResource"),
            "{UpdateType} resource {ItemKind}/{ItemName}.{ItemNamespace}",
            UpdateType.Deleting,
            existingResource.Kind,
            existingResource.Name(),
            existingResource.Namespace());

        var kubernetesEntity = existingResource.GetType().GetCustomAttribute<KubernetesEntityAttribute>();

        var deleteOptions = new V1DeleteOptions(
            apiVersion: V1DeleteOptions.KubeApiVersion,
            kind: V1DeleteOptions.KubeKind,
            preconditions: new V1Preconditions(
                resourceVersion: existingResource.ResourceVersion()));

        using var response = await _resourceClient.DeleteAnyResourceKindWithHttpMessagesAsync(
            group: existingResource.ApiGroup(),
            version: existingResource.ApiGroupVersion(),
            namespaceParameter: existingResource.Namespace() ?? string.Empty,
            plural: kubernetesEntity?.PluralName,
            name: existingResource.Name(),
            body: deleteOptions,
            cancellationToken: cancellationToken);
    }

    private async Task PatchResourceAsync(
        IKubernetesObject<V1ObjectMeta> generatedResource,
        IKubernetesObject<V1ObjectMeta> liveResource,
        JsonPatchDocument patch,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                new EventId(5, "ConfiguringResource"),
                "{UpdateType} resource {ItemKind}/{ItemName}.{ItemNamespace}",
                UpdateType.Patching,
                generatedResource.Kind,
                generatedResource.Name(),
                generatedResource.Namespace());

            _logger.LogDebug(
                new EventId(6, "PatchDocument"),
                "Json patch contains {JsonPatch}",
                new LazySerialization(patch));

            // ensure PATCH is idempotent - will fail if cache out of date, will fail if applied more than once
            patch = patch.Test(
                "/metadata/resourceVersion",
                liveResource.Metadata.ResourceVersion);

            // lastly, update the json annotation which is the basis for subsequent patch generation
            patch = patch.Replace(
                "/metadata/annotations/kubectl.kubernetes.io~1last-applied-configuration",
                _resourceSerializers.SerializeJson(generatedResource));

            var kubernetesEntity = generatedResource.GetType().GetCustomAttribute<KubernetesEntityAttribute>();

            using var response = await _resourceClient.PatchAnyResourceKindWithHttpMessagesAsync(
                body: new V1Patch(patch, V1Patch.PatchType.JsonPatch),
                group: kubernetesEntity.Group,
                version: kubernetesEntity.ApiVersion,
                namespaceParameter: generatedResource.Namespace(),
                plural: kubernetesEntity.PluralName,
                name: generatedResource.Name(),
                cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            if (TryNewReconcilerException(ex, out var reconcilerException))
            {
                throw reconcilerException;
            }
            throw;
        }
    }

    private async Task<JsonPatchDocument> CalculateJsonPatchDocumentAsync(IKubernetesObject<V1ObjectMeta> applyResource, IKubernetesObject<V1ObjectMeta> liveResource)
    {
        var parameters = new CreatePatchParameters
        {
            ApplyResource = _resourceSerializers.Convert<object>(applyResource),
            LiveResource = _resourceSerializers.Convert<object>(liveResource),
        };

        var lastAppliedConfiguration = liveResource.GetAnnotation("kubectl.kubernetes.io/last-applied-configuration");
        if (!string.IsNullOrEmpty(lastAppliedConfiguration))
        {
            parameters.LastAppliedResource = _resourceSerializers.DeserializeJson<object>(lastAppliedConfiguration);
        }

        parameters.ResourceKind = await _resourceKindManager.GetResourceKindAsync(
            apiVersion: applyResource.ApiVersion,
            kind: applyResource.Kind).ConfigureAwait(false);

        return _resourcePatcher.CreateJsonPatch(parameters);
    }

    private bool TryNewReconcilerException(HttpOperationException innerException, out ReconcilerException reconcilerException)
    {
        try
        {
            var status = _resourceSerializers.DeserializeJson<V1Status>(innerException.Response.Content);
            if (status.Kind == V1Status.KubeKind)
            {
                reconcilerException = new ReconcilerException(status, innerException);
                return true;
            }
            reconcilerException = default;
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
#pragma warning restore CA1031 // Do not catch general exception types
        {
            // Don't allow deserialization errors to replace original exception 
            reconcilerException = default;
            return false;
        }
    }

    /// <summary>
    /// Struct LazySerialization enables the json string to be created only when debug logging is enabled.
    /// </summary>
    private struct LazySerialization
    {
        private readonly JsonPatchDocument _patch;

        /// <summary>
        /// Initializes a new instance of the <see cref="LazySerialization"/> struct.
        /// </summary>
        /// <param name="patch">The patch.</param>
        public LazySerialization(JsonPatchDocument patch)
        {
            _patch = patch;
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(_patch, Formatting.None);
        }
    }
}

