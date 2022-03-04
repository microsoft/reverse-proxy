// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.ResourceKinds;
using NJsonSchema;
using System;

namespace Microsoft.Kubernetes.ResourceKindProvider.OpenApi;

public class OpenApiResourceKindElement : IResourceKindElement
{
    private readonly OpenApiResourceKind _resourceKind;
    private readonly JsonSchema _jsonSchema;
    private readonly Lazy<IResourceKindElement> _collectionElementType;


    public OpenApiResourceKindElement(
        OpenApiResourceKind resourceKind,
        JsonSchema jsonSchema,
        ElementMergeStrategy mergeStrategy,
        string mergeKey = default)
    {
        _collectionElementType = new Lazy<IResourceKindElement>(BindCollectionElementType);
        _resourceKind = resourceKind;
        _jsonSchema = jsonSchema;
        MergeStrategy = mergeStrategy;
        MergeKey = mergeKey;
    }

    public ElementMergeStrategy MergeStrategy { get; }

    public string MergeKey { get; }

    public IResourceKindElement GetPropertyElementType(string name)
    {
        // TODO: Cache properties by name

        if (_jsonSchema.ActualProperties.TryGetValue(name, out var property))
        {
            return _resourceKind.BindElement(property.ActualSchema);
        }

        return DefaultResourceKindElement.Unknown;
    }

    public IResourceKindElement GetCollectionElementType() => _collectionElementType.Value;

    private IResourceKindElement BindCollectionElementType()
    {
        return MergeStrategy switch
        {
            // array strategies
            ElementMergeStrategy.MergeListOfObject => _resourceKind.BindElement(_jsonSchema.Item.ActualSchema),
            ElementMergeStrategy.MergeListOfPrimative => _resourceKind.BindElement(_jsonSchema.Item.ActualSchema),
            ElementMergeStrategy.ReplaceListOfObject => _resourceKind.BindElement(_jsonSchema.Item.ActualSchema),
            ElementMergeStrategy.ReplaceListOfPrimative => _resourceKind.BindElement(_jsonSchema.Item.ActualSchema),

            // dictionary strategy
            ElementMergeStrategy.MergeMap => _resourceKind.BindElement(_jsonSchema.AdditionalPropertiesSchema.ActualSchema),

            // non-collection strategies
            ElementMergeStrategy.MergeObject => DefaultResourceKindElement.Unknown,
            ElementMergeStrategy.ReplacePrimative => DefaultResourceKindElement.Unknown,
            ElementMergeStrategy.Unknown => DefaultResourceKindElement.Unknown,

            // unexpected enum value
            _ => throw new InvalidOperationException($"Merge strategy '${MergeStrategy}' does not support GetCollectionElementType.")
        };
    }
}
