// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Kubernetes;

/// <summary>
/// Struct GroupKindNamespacedName is a value that acts as a dictionary key. It is a comparable
/// combination of a Kubernetes resource's apiGroup and kind in addition to the metadata namespace and name.
/// Implements the <see cref="IEquatable{T}" />.
/// </summary>
/// <seealso cref="IEquatable{T}" />
public struct GroupKindNamespacedName : IEquatable<GroupKindNamespacedName>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupKindNamespacedName"/> struct.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="namespacedName">Name of the namespaced.</param>
    public GroupKindNamespacedName(string group, string kind, NamespacedName namespacedName)
    {
        Group = group;
        Kind = kind;
        NamespacedName = namespacedName;
    }

    /// <summary>
    /// Gets the group.
    /// </summary>
    /// <value>The group.</value>
    public string Group { get; }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    /// <value>The kind.</value>
    public string Kind { get; }

    /// <summary>
    /// Gets the name of the namespaced.
    /// </summary>
    /// <value>The name of the namespaced.</value>
    public NamespacedName NamespacedName { get; }

    /// <summary>
    /// Implements the == operator.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The result of the operator.</returns>
    public static bool operator ==(GroupKindNamespacedName left, GroupKindNamespacedName right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Implements the != operator.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The result of the operator.</returns>
    public static bool operator !=(GroupKindNamespacedName left, GroupKindNamespacedName right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Gets key values from the specified resource.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns>GroupKindNamespacedName.</returns>
    public static GroupKindNamespacedName From(IKubernetesObject<V1ObjectMeta> resource)
    {
        if (resource is null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        return new GroupKindNamespacedName(
            resource.ApiGroup(),
            resource.Kind,
            NamespacedName.From(resource));
    }

    public override bool Equals(object obj)
    {
        return obj is GroupKindNamespacedName name && Equals(name);
    }

    public bool Equals([AllowNull] GroupKindNamespacedName other)
    {
        return Group == other.Group &&
               Kind == other.Kind &&
               NamespacedName.Equals(other.NamespacedName);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Group, Kind, NamespacedName);
    }
}
