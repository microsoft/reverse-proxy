// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Resources.Models
{
    public struct PatchOperation : IEquatable<PatchOperation>
    {
        private static readonly IEqualityComparer<JToken> _tokenEqualityComparer = new JTokenEqualityComparer();

        [JsonProperty("op")]
        public string Op { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("value")]
        public JToken Value { get; set; }

        public override bool Equals(object obj)
        {
            return obj is PatchOperation operation && Equals(operation);
        }

        public bool Equals(PatchOperation other)
        {
            return Op == other.Op &&
                   Path == other.Path &&
                   _tokenEqualityComparer.Equals(Value, other.Value);
        }

        public static bool operator ==(PatchOperation left, PatchOperation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PatchOperation left, PatchOperation right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"{Op} {Path} {Value?.GetType()?.Name} {Value}";
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Op, Path, _tokenEqualityComparer.GetHashCode(Value));
        }
    }
}
