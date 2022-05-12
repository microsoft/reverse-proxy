// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.Resources;

/// <summary>
/// Interface IResourceSerializers provides common methods to convert between objects, json, and yaml
/// representations of resource objects.
/// </summary>
public interface IResourceSerializers
{
    /// <summary>
    /// Converts a given object to a different resource type.
    /// </summary>
    /// <typeparam name="TResource">The resource model to target.</typeparam>
    /// <param name="resource">The source object to convert.</param>
    /// <returns>TResource.</returns>
    TResource Convert<TResource>(object resource);

    /// <summary>
    /// Deserializes from yaml string.
    /// </summary>
    /// <typeparam name="TResource">The resource model to deserialize.</typeparam>
    /// <param name="yaml">The yaml content to parse.</param>
    /// <returns>TResource.</returns>
    TResource DeserializeYaml<TResource>(string yaml);

    /// <summary>
    /// Deserializes from json string.
    /// </summary>
    /// <typeparam name="TResource">The resource model to deserialize.</typeparam>
    /// <param name="json">The json content to parse.</param>
    /// <returns>T.</returns>
    TResource DeserializeJson<TResource>(string json);

    /// <summary>
    /// Serializes to json string.
    /// </summary>
    /// <param name="resource">The resource object to serialize.</param>
    /// <returns>System.String.</returns>
    string SerializeJson(object resource);
}
