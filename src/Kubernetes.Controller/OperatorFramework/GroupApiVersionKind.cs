// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using System;
using System.Reflection;

namespace Microsoft.Kubernetes;

public struct GroupApiVersionKind : IEquatable<GroupApiVersionKind>
{
    public GroupApiVersionKind(string group, string apiVersion, string kind, string pluralName)
    {
        Group = group;
        ApiVersion = apiVersion;
        GroupApiVersion = string.IsNullOrEmpty(Group) ? apiVersion : $"{group}/{apiVersion}";
        Kind = kind;
        PluralName = pluralName;
    }

    public string Group { get; }

    public string ApiVersion { get; }

    public string GroupApiVersion { get; }

    public string Kind { get; }

    public string PluralName { get; set; }

    public string PluralNameGroup => string.IsNullOrEmpty(Group) ? PluralName : $"{PluralName}.{Group}";

    public static GroupApiVersionKind From<TResource>() => From(typeof(TResource));

    public static GroupApiVersionKind From(Type resourceType)
    {
        var entity = resourceType.GetTypeInfo().GetCustomAttribute<KubernetesEntityAttribute>();

        return new GroupApiVersionKind(
            group: entity.Group,
            apiVersion: entity.ApiVersion,
            kind: entity.Kind,
            pluralName: entity.PluralName);
    }

    public override bool Equals(object obj)
    {
        return obj is GroupApiVersionKind kind && Equals(kind);
    }

    public bool Equals(GroupApiVersionKind other)
    {
        return Group == other.Group &&
               ApiVersion == other.ApiVersion &&
               Kind == other.Kind &&
               PluralName == other.PluralName;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Group, ApiVersion, Kind, PluralName);
    }

    public override string ToString()
    {
        return $"{Kind}.{GroupApiVersion}";
    }

    public static bool operator ==(GroupApiVersionKind left, GroupApiVersionKind right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GroupApiVersionKind left, GroupApiVersionKind right)
    {
        return !(left == right);
    }
}
